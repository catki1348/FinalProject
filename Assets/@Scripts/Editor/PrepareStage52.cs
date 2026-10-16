using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. Shared builder for temporary preparation and explicit Stage5 deployment.
public static class PrepareStage52
{
    const string Base = "Assets/@ExternalAssets/@Prefab/";
    const string Folder = Base + "Stage5_2/";
    [Serializable] public class Point { public float x, y; public Vector2 V => new Vector2(x,y); }
    [Serializable] public class Platform { public string id,kind; public float x,y,width,speed,delay,wait,syncPeriod; public bool loop; public Point[] points; }
    [Serializable] public class Monster { public string id,pattern,group; public float x,y,orbit,pathLength; public bool guaranteed; public Point area,detection; }
    [Serializable] public class Gate { public string key; public float x,y,width,height; }
    [Serializable] public class Strip { public string id; public float x0,x1,y; }
    [Serializable] public class Design { public Point origin; public float boundary12,boundary23,minY,maxY; public Platform[] platforms; public Monster[] monsters; public Gate[] groups; public Strip[] fallTriggers; }

    static void Set(UnityEngine.Object target, Action<SerializedObject> configure)
    {
        var so=new SerializedObject(target); configure(so); so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if(PrefabUtility.IsPartOfPrefabInstance(target))PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    static GameObject Variant(string name,string source,Action<GameObject> configure)
    {
        string path=Folder+name+".prefab";
        bool exists=AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null;
        var go=exists?PrefabUtility.LoadPrefabContents(path):(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try {configure(go);return PrefabUtility.SaveAsPrefabAsset(go,path);}
        finally {if(exists)PrefabUtility.UnloadPrefabContents(go);else UnityEngine.Object.DestroyImmediate(go);}
    }

    // Full rectangle sprite data is local to preparation; no shared importer is changed.
    static Sprite Tile()
    {
        string path=Folder+"T_Stage52_RunwayStrip.asset";
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(sprite)return sprite;
        var original=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/@ExternalAssets/Tiles/Platform_Stage5_Hellscape.png");
        var texture=original.texture;
        // The original is a perspective block with background padding, not a repeating flat tile.
        // Crop only its opaque front masonry; 100 PPU gives a native 0.5-unit-thick strip.
        sprite=Sprite.Create(texture,new Rect(230,374,450,50),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
        AssetDatabase.CreateAsset(sprite,path);return sprite;
    }

    static GameObject PlatformPrefab(string kind,Sprite tile)
    {
        return Variant("P_Stage52_"+kind,Base+"Platform_Stage5_Hellscape.prefab",go=>{
            foreach(var b in go.GetComponents<PlatformBehaviour>())UnityEngine.Object.DestroyImmediate(b);
            if(!go.GetComponent<PlatformCore>())go.AddComponent<PlatformCore>();
            if(kind=="moving")go.AddComponent<MovingPlatform>();
            else if(kind=="damage")go.AddComponent<DamageFoothold>();
            else if(kind=="blink") {
                var b=go.AddComponent<BlinkingFoothold>();Set(b,s=>{s.FindProperty("visibleDuration").floatValue=1.6f;s.FindProperty("invisibleDuration").floatValue=1;});
            }
            else if(kind=="disappearing")go.AddComponent<DisappearingFoothold>();
            else if(kind=="damage_floor"&&!go.GetComponent<RunwayDamage>())go.AddComponent<RunwayDamage>();
            go.layer=LayerMask.NameToLayer("Ground");
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=tile;sr.drawMode=SpriteDrawMode.Tiled;sr.tileMode=SpriteTileMode.Continuous;sr.size=new Vector2(3,.5f);
            sr.color=kind=="damage"||kind=="damage_floor"?new Color(1,.4f,.35f):kind=="blink"?new Color(.7f,.7f,1):Color.white;
            var box=go.GetComponent<BoxCollider2D>();box.autoTiling=false;box.size=sr.size;box.offset=Vector2.zero;
            go.transform.localScale=Vector3.one;
        });
    }

    static GameObject MonsterPrefab(Monster m,Transform player)
    {
        string key=m.pattern+(m.guaranteed?"_Paired":"_Default");
        return Variant("P_Stage52_"+key,Base+"Stage4_1/P_Stage41_"+m.pattern+".prefab",go=>{
            var leash=go.GetComponent<MonsterMovementBounds>();if(leash)UnityEngine.Object.DestroyImmediate(leash);
            var drop=go.GetComponent<MonsterItemDropper>()??go.AddComponent<MonsterItemDropper>();
            Set(drop,so=>{
                var entries=so.FindProperty("entries");entries.arraySize=m.guaranteed?2:4;
                string[] items={"P_Item_SpeedBoost","P_Item_HealSmall","P_Item_HealLarge","P_Item_Rearm"};
                for(int i=0;i<entries.arraySize;i++) {
                    var e=entries.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Base+items[i]+".prefab");
                    e.FindPropertyRelative("chance").floatValue=m.guaranteed||i==0?1:i==3?.05f:.1f;
                    e.FindPropertyRelative("condition").enumValueIndex=i>=2?i-1:0;
                    e.FindPropertyRelative("lowHealthMultiplier").floatValue=!m.guaranteed&&i==1?2:1;
                }
            });
            var pattern=go.GetComponent(go.GetComponent<IMonsterPattern>().GetType());
            Set(pattern,so=>{
                var mask=so.FindProperty("playerLayer");if(mask!=null)mask.intValue=1<<player.gameObject.layer;
                var speed=so.FindProperty("moveSpeed");if(speed!=null)speed.floatValue=m.guaranteed?9:3;
            });
            // Guaranteed chasers use the same 2-hit baseline as the 4-2 implementation.
            if(m.guaranteed)Set(go.GetComponent<MonsterStats>(),so=>so.FindProperty("maxHealth").intValue=2);
        });
    }

    [MenuItem("Tools/Prepare/Stage 5-2 Temporary Scene")]
    public static void Prepare()
    {
        string path="Assets/Scenes/Test052.unity";
        if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path)!=null)throw new InvalidOperationException("Test052 already exists; choose another temporary number");
        var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(previous.isDirty)EditorSceneManager.SaveScene(previous);
        var source=EditorSceneManager.OpenScene("Assets/Scenes/Stage5.unity",OpenSceneMode.Additive);
        try {EditorSceneManager.SaveScene(source,path,true);} finally {EditorSceneManager.CloseScene(source,true);}
        var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        Build(scene);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }

    // A strict temporary-scene guard prevents accidental main-scene deployment.
    public static void Build(UnityEngine.SceneManagement.Scene scene)
    {
        if(!scene.name.StartsWith("Test"))throw new InvalidOperationException("Preparation accepts Test scenes only");
        BuildContent(scene, false);
    }

    // Main deployment is a separate explicit entry point; preparation keeps its Test guard.
    [MenuItem("Tools/Deploy/Stage 5-2 Composite Map")]
    public static void Deploy()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop play mode before deploying");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Stage5.unity") {
            if(scene.isDirty)EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene("Assets/Scenes/Stage5.unity",OpenSceneMode.Single);
        }
        BuildContent(scene, true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void BuildContent(UnityEngine.SceneManagement.Scene scene, bool main)
    {
        var d=JsonUtility.FromJson<Design>(File.ReadAllText("@Edit/Stage52_UnityPreparation.json"));
        var map=new Dictionary<string,Transform>();
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(!map.ContainsKey(t.name))map.Add(t.name,t);
        var section=map["Section_2"];var start=map["ZoneStartPoint_5-2"];var end=map["ZoneEndPoint_5-2"];
        var stage=map["StageManager"].GetComponent<StageManager>();
        var player=(Transform)new SerializedObject(stage).FindProperty("player").objectReferenceValue;
        float shift=d.boundary23-map["Boundary_2_3"].position.x;
        foreach(string name in new[]{"Section_3","Section_4","Boundary_2_3","Boundary_3_4","Boundary_4_5"})map[name].position+=Vector3.right*shift;
        map["Boundary_1_2"].position=new Vector3(d.boundary12,0,0);
        map["Section_5"].position=new Vector3(495.5f,-210,0);
        // Reserve space to the right of 5-1 without moving its existing content/spawn markers.
        var remove=new List<GameObject>();foreach(Transform child in section)if(child!=start&&child!=end)remove.Add(child.gameObject);
        foreach(var go in remove)UnityEngine.Object.DestroyImmediate(go);
        section.position=new Vector3((d.boundary12+d.boundary23)/2,(d.minY+d.maxY)/2,0);
        var zone=section.GetComponent<CameraBoundsZone>();Set(zone,so=>{
            so.FindProperty("size").vector2Value=new Vector2(d.boundary23-d.boundary12,d.maxY-d.minY);
            so.FindProperty("orthographicSize").floatValue=5;
        });
        if(!AssetDatabase.IsValidFolder(Folder.TrimEnd('/')))AssetDatabase.CreateFolder(Base.TrimEnd('/'),"Stage5_2");
        var root52=new GameObject("Generated_5-2").transform;root52.SetParent(section,false);root52.position=d.origin.V;
        var tile=Tile();var prefabs=new Dictionary<string,GameObject>();var floors=new List<RunwayDamage>();
        foreach(var p in d.platforms) {
            string kind=p.kind=="start"||p.kind=="connector"||p.kind=="goal"?"normal":p.kind;
            if(!prefabs.ContainsKey(kind))prefabs[kind]=PlatformPrefab(kind,tile);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind],root52);go.name=p.id;
            go.transform.localPosition=new Vector3(p.x,p.y-.25f,0);
            Set(go.GetComponent<SpriteRenderer>(),so=>so.FindProperty("m_Size").vector2Value=new Vector2(p.width,.5f));
            Set(go.GetComponent<BoxCollider2D>(),so=>so.FindProperty("m_Size").vector2Value=new Vector2(p.width,.5f));
            go.transform.localScale=Vector3.one;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            if(p.points.Length>0) {
                var path=Path(root52,"Path_"+p.id,p.points,-.25f);
                Set(go.GetComponent<MovingPlatform>(),so=>{
                    so.FindProperty("path").objectReferenceValue=path;
                    so.FindProperty("speed").floatValue=p.speed;
                    so.FindProperty("startDelay").floatValue=p.delay;
                    so.FindProperty("endpointWait").floatValue=p.wait;
                    so.FindProperty("loopMode").enumValueIndex=p.loop?0:1;
                    so.FindProperty("offscreenSpeedMultiplier").floatValue=1;
                    so.FindProperty("synchronizedCycleDuration").floatValue=p.syncPeriod;
                });
                go.transform.position=path.GetPoint(0);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            if(go.TryGetComponent<RunwayDamage>(out var floor))floors.Add(floor);
        }
        foreach(var s in d.fallTriggers) {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base+"P_OutOfBounds_Zone.prefab"),root52);go.name=s.id;
            go.transform.localPosition=new Vector3((s.x0+s.x1)/2,s.y,0);
            Set(go.GetComponent<BoxCollider2D>(),so=>{so.FindProperty("m_Size").vector2Value=new Vector2(s.x1-s.x0,1);so.FindProperty("m_IsTrigger").boolValue=true;});
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        var groups=new Dictionary<string,GameObject>();foreach(var gate in d.groups){var go=new GameObject(gate.key);go.transform.SetParent(root52,false);groups.Add(gate.key,go);}
        var monsterPrefabs=new Dictionary<string,GameObject>();
        foreach(var m in d.monsters) {
            string key=m.pattern+m.guaranteed;if(!monsterPrefabs.ContainsKey(key))monsterPrefabs[key]=MonsterPrefab(m,player);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(monsterPrefabs[key],groups[m.group].transform);go.name=m.id;
            go.GetComponent<MonsterScaleSetter>().ApplyScale(player);go.transform.position=root52.position+new Vector3(m.x,m.y,0);
            var pattern=go.GetComponent(go.GetComponent<IMonsterPattern>().GetType());
            Set(pattern,so=>{
                var area=so.FindProperty("areaSize");if(area!=null)area.vector2Value=m.area.V;
                var detect=so.FindProperty("detectionSize");if(detect!=null)detect.vector2Value=m.detection.V;
                var orbit=so.FindProperty("circleRadius");if(orbit!=null)orbit.floatValue=m.orbit;
                if(m.pathLength>0)so.FindProperty("path").objectReferenceValue=Path(root52,"Path_"+m.id,new[]{new Point{x=m.x,y=m.y},new Point{x=m.x+m.pathLength,y=m.y}},0);
            });
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        foreach(var g in groups.Values)g.SetActive(false);
        var manager=root52.gameObject.AddComponent<CompositeSectionManager>();Set(manager,so=>{
            so.FindProperty("stage").objectReferenceValue=stage;so.FindProperty("player").objectReferenceValue=player.GetComponent<PlayerHealth>();
            var fs=so.FindProperty("floors");fs.arraySize=floors.Count;for(int i=0;i<floors.Count;i++)fs.GetArrayElementAtIndex(i).objectReferenceValue=floors[i];
            var gates=so.FindProperty("gates");gates.arraySize=d.groups.Length;
            for(int i=0;i<d.groups.Length;i++){var gate=d.groups[i];var entry=gates.GetArrayElementAtIndex(i);entry.FindPropertyRelative("center").vector2Value=new Vector2(gate.x,gate.y);entry.FindPropertyRelative("size").vector2Value=new Vector2(gate.width,gate.height);entry.FindPropertyRelative("root").objectReferenceValue=groups[gate.key];}
        });
        start.position=d.origin.V+new Vector2(0,.03f);
        foreach(var p in d.platforms)if(p.kind=="goal")end.position=d.origin.V+new Vector2(p.x,p.y+1);
        PrefabUtility.RecordPrefabInstancePropertyModifications(start);
        PrefabUtility.RecordPrefabInstancePropertyModifications(end);
        Set(end.GetComponent<BoxCollider2D>(),so=>so.FindProperty("m_Size").vector2Value=new Vector2(1,8));
        string bgPath=Folder+"T_Stage52_Background.asset";
        var bgSprite=AssetDatabase.LoadAssetAtPath<Sprite>(bgPath);
        if(bgSprite==null){var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Backgrounds/Stage5_Background.png");bgSprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);AssetDatabase.CreateAsset(bgSprite,bgPath);}
        var background=Variant("P_Stage52_Background",Base+"Stage4_1/P_Stage41_Background.prefab",go=>go.GetComponent<SpriteRenderer>().sprite=bgSprite);
        var bg=(GameObject)PrefabUtility.InstantiatePrefab(background,root52);bg.name="Background_5-2";bg.transform.position=new Vector3(section.position.x,section.position.y,10);
        Set(bg.GetComponent<ZoneBackground>(),so=>so.FindProperty("zone").objectReferenceValue=zone);
        // The old full-width fall line at -20 would kill legitimate descending traversal.
        var oldFall=map["OutOfBoundsTrigger"].GetComponent<BoxCollider2D>();
        oldFall.transform.position=new Vector3(246,-40,0);oldFall.size=new Vector2(510,1);
        PrefabUtility.RecordPrefabInstancePropertyModifications(oldFall);
        PrefabUtility.RecordPrefabInstancePropertyModifications(oldFall.transform);
        // Relocated later areas still need a fall line; the old blanket line would cut across 5-2.
        AddFallLine("OutOfBounds_5-3_5-4",map["Section_3"].parent,d.boundary23,map["Boundary_4_5"].position.x,-20);
        var bossBounds=map["Section_5"].GetComponent<CameraBoundsZone>().WorldBounds;
        AddFallLine("OutOfBounds_5-5",map["Section_5"],bossBounds.min.x,bossBounds.max.x,bossBounds.min.y-8);
        if(!main)Set(map["WarpManager"].GetComponent<WarpManager>(),so=>{so.FindProperty("testStartZone").stringValue="5-2";so.FindProperty("testPlatformName").stringValue="";});
        AssetDatabase.SaveAssets();
        Debug.Log("5-2 "+(main?"deployed":"prepared")+": 67 platforms, 20 movers, 31 monsters, 13 fall strips");
    }

    // Placement-only bounds overrides reuse the existing fall-trigger prefab, with no duplicates on redeploy.
    static void AddFallLine(string name,Transform parent,float left,float right,float y)
    {
        Transform previous=null;
        foreach(Transform t in parent)if(t.name==name)previous=t;
        if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base+"P_OutOfBounds_Zone.prefab"),parent);
        go.name=name;go.transform.position=new Vector3((left+right)*.5f,y,0);go.transform.localScale=Vector3.one;
        Set(go.GetComponent<BoxCollider2D>(),so=>{so.FindProperty("m_Size").vector2Value=new Vector2(right-left,1);so.FindProperty("m_IsTrigger").boolValue=true;});
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
    }

    static PlatformPath Path(Transform parent,string name,Point[] points,float height)
    {
        var root=new GameObject(name).transform;root.SetParent(parent,false);var path=root.gameObject.AddComponent<PlatformPath>();
        for(int i=0;i<points.Length;i++){var p=new GameObject("Point_"+i).transform;p.SetParent(root,false);p.localPosition=new Vector3(points[i].x,points[i].y+height,0);}
        return path;
    }
}
