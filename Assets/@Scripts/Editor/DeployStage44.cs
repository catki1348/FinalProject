using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. Repeatable deployment of the reviewed 4-4 concept.
public static class DeployStage44
{
    const string Base = "Assets/@ExternalAssets/@Prefab/";
    const string Folder = Base + "Stage4_4/";
    [Serializable] class PlatformData { public string id; public float[] center, size; public int fallAtSeconds; }
    [Serializable] class MonsterData { public int group; public float x,y,activateAtX; }
    [Serializable] class Design { public string schema; public PlatformData[] platforms; public MonsterData[] monsterProposals; }

    // Persist every override explicitly so reopen/domain reload cannot lose authored references.
    static void Set(UnityEngine.Object target, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target); edit(so); so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    // Edit the reusable variant original before instantiating any repeated scene objects.
    static GameObject Variant(string name, string source, Action<GameObject> configure)
    {
        string path = Folder + name + ".prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        var go = exists ? PrefabUtility.LoadPrefabContents(path) : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try { configure(go); return PrefabUtility.SaveAsPrefabAsset(go, path); }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(go); else UnityEngine.Object.DestroyImmediate(go); }
    }

    // The two endpoint pieces differ only in width; both still fall on their assigned second.
    static GameObject SupportPrefab(int width)
    {
        return Variant("P_Stage44_Falling_W" + width, Base + "Platform_Stage4_Dungeon.prefab", go => {
            foreach (var b in go.GetComponents<PlatformBehaviour>()) UnityEngine.Object.DestroyImmediate(b);
            if (!go.GetComponent<PlatformCore>()) go.AddComponent<PlatformCore>();
            if (!go.GetComponent<SequentialFallingPlatform>()) go.AddComponent<SequentialFallingPlatform>();
            go.layer = LayerMask.NameToLayer("Ground");
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/@ExternalAssets/Tiles/Platform_Stage4_Dungeon_Tiled.png");
            sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(width,1); sr.color = new Color(.8f,.85f,1);
            var box = go.GetComponent<BoxCollider2D>(); box.autoTiling=false; box.size=sr.size; box.offset=Vector2.zero;
            string materialPath=Folder+"M_Stage44_Frictionless.physicsMaterial2D";
            var material=AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
            if(material==null) { material=new PhysicsMaterial2D("M_Stage44_Frictionless");AssetDatabase.CreateAsset(material,materialPath); }
            material.friction=0;material.bounciness=0;EditorUtility.SetDirty(material);box.sharedMaterial=material;
            var effector=go.GetComponent<PlatformEffector2D>();
            if(effector) UnityEngine.Object.DestroyImmediate(effector);
            box.usedByEffector=false;
            go.transform.localScale=Vector3.one;
        });
    }

    // Ordinary monsters get the general drop table, not the recovery/fury zone overrides.
    static GameObject MonsterPrefab(Transform player)
    {
        int attack = new SerializedObject(player.GetComponent<PlayerStats>()).FindProperty("baseAttack").intValue;
        var equipment = new SerializedObject(player.GetComponent<PlayerEquipment>());
        foreach(string slot in new[]{"defaultMainWeapon","defaultSubEquipment"}) {
            var item=equipment.FindProperty(slot).objectReferenceValue as ItemData;
            if(item!=null) attack+=item.AttackBonus;
        }
        int hp=2*Mathf.Max(1,attack-10);
        return Variant("P_Stage44_FlyingChaser", Base+"Stage4_2/P_Stage42_FlyingChaser.prefab", go=>{
            Set(go.GetComponent<MonsterStats>(),so=>so.FindProperty("maxHealth").intValue=hp);
            Set(go.GetComponent<FlyingChasePattern>(),so=>{
                so.FindProperty("moveSpeed").floatValue=9;
                so.FindProperty("detectionSize").vector2Value=new Vector2(24,10);
                so.FindProperty("playerLayer").intValue=1<<player.gameObject.layer;
                so.FindProperty("path").objectReferenceValue=null;
            });
            Set(go.GetComponent<MonsterMovementBounds>(),so=>so.FindProperty("areaOrigin").objectReferenceValue=null);
            var items=new[]{"P_Item_SpeedBoost","P_Item_HealSmall","P_Item_HealLarge","P_Item_Rearm"};
            var chances=new[]{1f,.1f,.1f,.05f}; var conditions=new[]{0,0,1,2};
            Set(go.GetComponent<MonsterItemDropper>(),so=>{
                var entries=so.FindProperty("entries"); entries.arraySize=4;
                for(int i=0;i<4;i++) {
                    var e=entries.GetArrayElementAtIndex(i);
                    var item=AssetDatabase.LoadAssetAtPath<GameObject>(Base+items[i]+".prefab");
                    if(item==null) throw new InvalidOperationException("Missing ordinary drop: "+items[i]);
                    e.FindPropertyRelative("prefab").objectReferenceValue=item;
                    e.FindPropertyRelative("chance").floatValue=chances[i];
                    e.FindPropertyRelative("condition").enumValueIndex=conditions[i];
                    e.FindPropertyRelative("lowHealthMultiplier").floatValue=i==1?2:1;
                }
            });
        });
    }

    [MenuItem("Tools/Deploy/Stage 4-4 Super Run")]
    public static void Deploy()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!="Assets/Scenes/Stage4.unity") throw new InvalidOperationException("Stage4 edit mode required");
        var data=JsonUtility.FromJson<Design>(File.ReadAllText("@Edit/MapDesign_Stage4-4.json"));
        if(data.schema!="stage4_4_super_run_v1" || data.platforms.Length!=150 || data.monsterProposals.Length!=30) throw new InvalidOperationException("Unreviewed design data");
        var map=new Dictionary<string,Transform>();
        foreach(var root in scene.GetRootGameObjects()) foreach(var t in root.GetComponentsInChildren<Transform>(true)) if(!map.ContainsKey(t.name)) map.Add(t.name,t);
        var section=map["Section_4"]; var start=map["ZoneStartPoint_4-4"]; var end=map["ZoneEndPoint_4-4"];
        var stage=map["StageManager"].GetComponent<StageManager>();
        var player=(Transform)new SerializedObject(stage).FindProperty("player").objectReferenceValue;
        var speed=player.GetComponent<PlayerSpeedBuff>() ?? player.gameObject.AddComponent<PlayerSpeedBuff>();
        float origin=map["Boundary_3_4"].position.x+20;
        var remove=new List<GameObject>();
        foreach(Transform t in section) if(t!=start && t!=end) remove.Add(t.gameObject);
        foreach(var go in remove) UnityEngine.Object.DestroyImmediate(go);
        float right=origin+1208;
        map["Boundary_4_5"].position=new Vector3(right,0,0);
        section.position=new Vector3((map["Boundary_3_4"].position.x+right)*.5f,-2,0);
        var zone=section.GetComponent<CameraBoundsZone>();
        Set(zone,so=>{
            so.FindProperty("size").vector2Value=new Vector2(right-map["Boundary_3_4"].position.x,36);
            so.FindProperty("orthographicSize").floatValue=5;
            so.FindProperty("leftBoundary").objectReferenceValue=map["Boundary_3_4"];
            so.FindProperty("rightBoundary").objectReferenceValue=map["Boundary_4_5"];
        });
        var body=player.GetComponent<Collider2D>();
        start.position=new Vector3(origin,player.position.y-body.bounds.min.y+.03f,0);
        end.position=new Vector3(origin+1200,3,0);
        Set(end.GetComponent<BoxCollider2D>(),so=>{so.FindProperty("m_Size").vector2Value=new Vector2(1,16);so.FindProperty("m_IsTrigger").boolValue=true;});
        PrefabUtility.RecordPrefabInstancePropertyModifications(start); PrefabUtility.RecordPrefabInstancePropertyModifications(end);
        if(!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder(Base.TrimEnd('/'),"Stage4_4");
        var middle=SupportPrefab(8); var edge=SupportPrefab(12); var monster=MonsterPrefab(player);
        var generated=new GameObject("Generated_4-4_SuperRun").transform; generated.SetParent(section,false); generated.position=new Vector3(origin,0,0);
        // Merge intact supports into one physical surface; each disabled input disappears on fall.
        var surfaceRoot=new GameObject("FallingSupports").transform;surfaceRoot.SetParent(generated,false);
        surfaceRoot.gameObject.layer=LayerMask.NameToLayer("Ground");
        surfaceRoot.gameObject.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
        var composite=surfaceRoot.gameObject.AddComponent<CompositeCollider2D>();
        composite.geometryType=CompositeCollider2D.GeometryType.Polygons;
        composite.generationType=CompositeCollider2D.GenerationType.Synchronous;
        composite.sharedMaterial=AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(Folder+"M_Stage44_Frictionless.physicsMaterial2D");
        var supports=new SequentialFallingPlatform[150];
        for(int i=0;i<150;i++) {
            var p=data.platforms[i];
            if(p.fallAtSeconds!=i+1) throw new InvalidOperationException("Nonsequential fall data");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(i==0||i==149?edge:middle,surfaceRoot);
            go.name="Platform_4-4_"+p.id+"_Falling"; go.transform.localPosition=new Vector3(p.center[0],p.center[1],0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform); supports[i]=go.GetComponent<SequentialFallingPlatform>();
            var supportBox=go.GetComponent<BoxCollider2D>();supportBox.compositeOperation=Collider2D.CompositeOperation.Merge;
            PrefabUtility.RecordPrefabInstancePropertyModifications(supportBox);
        }
        var groups=new GameObject[15];
        for(int i=0;i<15;i++) {groups[i]=new GameObject("Encounter_4-4_G"+(i+1).ToString("D2"));groups[i].transform.SetParent(generated,false);}
        foreach(var m in data.monsterProposals) {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(monster,groups[m.group-1].transform);
            go.name="Monster_4-4_G"+m.group.ToString("D2")+"_X"+m.x;
            go.GetComponent<MonsterScaleSetter>().ApplyScale(player);
            go.transform.localPosition=new Vector3(m.x,m.y,0);
            Set(go.GetComponent<MonsterMovementBounds>(),so=>{
                so.FindProperty("areaOrigin").objectReferenceValue=generated;
                so.FindProperty("min").vector2Value=new Vector2(-4,.1f);
                so.FindProperty("max").vector2Value=new Vector2(1204,3.2f);
                so.FindProperty("constrainY").boolValue=true;
            });
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        foreach(var group in groups) group.SetActive(false);
        Set(generated.gameObject.AddComponent<SuperRunManager>(),so=>{
            so.FindProperty("stage").objectReferenceValue=stage;so.FindProperty("player").objectReferenceValue=player.GetComponent<PlayerHealth>();
            so.FindProperty("speed").objectReferenceValue=speed;so.FindProperty("startPoint").objectReferenceValue=start;
            var a=so.FindProperty("platforms");a.arraySize=150;for(int i=0;i<150;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=supports[i];
            a=so.FindProperty("encounters");a.arraySize=15;for(int i=0;i<15;i++){var e=a.GetArrayElementAtIndex(i);e.FindPropertyRelative("distance").floatValue=i*80;e.FindPropertyRelative("root").objectReferenceValue=groups[i];}
        });
        var bg=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base+"Stage4_1/P_Stage41_Background.prefab"),generated);
        bg.name="Background_4-4"; Set(bg.GetComponent<ZoneBackground>(),so=>so.FindProperty("zone").objectReferenceValue=zone);
        bg.transform.position=new Vector3(origin+600,-2,10);
        // Center the existing boss arena beneath the now-wider upper row, preserving its children.
        float left=map["Section_1"].GetComponent<CameraBoundsZone>().WorldBounds.min.x;
        var boss=map["Section_5"];boss.position=new Vector3((left+right)*.5f,boss.position.y,boss.position.z);
        var fall=map["OutOfBoundsTrigger"];var fallBox=fall.GetComponent<BoxCollider2D>();
        float fallLeft=fallBox.bounds.min.x;
        fall.position=new Vector3((fallLeft+right)*.5f,fall.position.y,0);fallBox.size=new Vector2(right-fallLeft,fallBox.size.y);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Stage4-4 deployed: 150 falling supports, 30 monsters, route "+origin+" -> "+(origin+1200));
    }
}
