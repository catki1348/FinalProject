using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Codex, model unknown, 2026-10-15. Latest item wiring and Fury Run deployment.
public static class DeployItemZones
{
    const string Base = "Assets/@ExternalAssets/@Prefab/";
    const string Folder = Base + "Stage4_3_Fury/";
    [Serializable] class GroupData { public int group, count; public float startX, endX; }
    [Serializable] class Data { public string schema; public GroupData[] groups; }

    // Apply serialized values and persist prefab-instance overrides explicitly.
    public static void Set(UnityEngine.Object target, Action<SerializedObject> configure)
    {
        var so = new SerializedObject(target); configure(so); so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    // Update reusable originals first; never instantiate a partially configured item/monster.
    static GameObject Variant(string name, string source, Action<GameObject> configure)
    {
        string path = Folder + name + ".prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        var go = exists ? PrefabUtility.LoadPrefabContents(path)
            : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try { configure(go); return PrefabUtility.SaveAsPrefabAsset(go, path); }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(go); else UnityEngine.Object.DestroyImmediate(go); }
    }

    // Explicit drop sets avoid accidentally inheriting recovery items into the Fury Run.
    static void Drops(GameObject go, GameObject[] items, float[] chances, int[] conditions, float[] low)
    {
        var dropper = go.GetComponent<MonsterItemDropper>() ?? go.AddComponent<MonsterItemDropper>();
        Set(dropper, so => {
            so.FindProperty("lowHealthThreshold").floatValue = .3f;
            var entries = so.FindProperty("entries"); entries.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) {
                var e = entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("prefab").objectReferenceValue = items[i];
                e.FindPropertyRelative("chance").floatValue = chances[i];
                e.FindPropertyRelative("condition").enumValueIndex = conditions[i];
                e.FindPropertyRelative("lowHealthMultiplier").floatValue = low[i];
            }
        });
    }

    // Keep the HUD structure as one reusable prefab and bind scene-local player references.
    static GameObject HudPrefab()
    {
        var go = new GameObject("P_ItemBuffHUD", typeof(Canvas), typeof(CanvasScaler));
        try {
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.GetComponent<Canvas>().sortingOrder = 60;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var text = new GameObject("BuffText", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(go.transform, false);
            var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1); rect.anchoredPosition = new Vector2(-28, -160); rect.sizeDelta = new Vector2(620, 100);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 28;
            text.alignment = TextAnchor.UpperRight; text.color = new Color(1, .8f, .35f); text.raycastTarget = false;
            text.text = ""; text.enabled = false;
            var hud = go.AddComponent<ItemBuffHUD>(); Set(hud, so => so.FindProperty("label").objectReferenceValue = text);
            return PrefabUtility.SaveAsPrefabAsset(go, Folder + "P_ItemBuffHUD.prefab");
        } finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    // Add independent buff state to the source player; shared equipment stats remain intact.
    static void PreparePlayer(string path)
    {
        var go = PrefabUtility.LoadPrefabContents(path);
        try {
            if (!go.GetComponent<PlayerSpeedBuff>()) go.AddComponent<PlayerSpeedBuff>();
            if (!go.GetComponent<PlayerFuryBuff>()) go.AddComponent<PlayerFuryBuff>();
            PrefabUtility.SaveAsPrefabAsset(go, path);
        } finally { PrefabUtility.UnloadPrefabContents(go); }
    }

    static Dictionary<string, Transform> Objects(UnityEngine.SceneManagement.Scene scene)
    {
        var result = new Dictionary<string, Transform>();
        foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (!result.ContainsKey(t.name)) result.Add(t.name, t);
        return result;
    }

    [MenuItem("Tools/Deploy/Item Zones 3-1 4-1 4-2 4-3")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play before deployment.");
        var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (current.isDirty) throw new InvalidOperationException("Preserve current unsaved scene before deployment.");
        var data = JsonUtility.FromJson<Data>(File.ReadAllText("@Edit/MapDesign_Stage4-3_Fury.json"));
        if (data.schema != "stage4_3_fury_v1" || data.groups.Length != 6) throw new InvalidOperationException("Unexpected Fury schema");
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder(Base.TrimEnd('/'), "Stage4_3_Fury");
        PreparePlayer(Base + "Player_Warrior.prefab");
        var speed = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "P_Item_SpeedBoost.prefab");
        var heal = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "P_Item_HealSmall.prefab");
        var large = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "P_Item_HealLarge.prefab");
        var rearm = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "P_Item_Rearm.prefab");
        if (!speed || !heal || !large || !rearm) throw new InvalidOperationException("Required drop prefabs missing");
        var fury = Variant("P_Item_Fury", Base + "P_Item_SpeedBoost.prefab", go => {
            UnityEngine.Object.DestroyImmediate(go.GetComponent<SpeedBoostItem>());
            if (!go.GetComponent<FuryItem>()) go.AddComponent<FuryItem>();
            go.GetComponent<SpriteRenderer>().color = new Color(1, .25f, .12f);
            Set(go.GetComponent<ItemLifetime>(), so => so.FindProperty("lifetime").floatValue = 20);
            Set(go.GetComponent<FuryItem>(), so => so.FindProperty("pickupSfx").objectReferenceValue =
                new SerializedObject(speed.GetComponent<SpeedBoostItem>()).FindProperty("pickupSfx").objectReferenceValue);
        });
        // Stage4-1 uses the default general-monster table, distinct from the runway exceptions.
        var variants = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Base + "Stage4_1" })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset.GetComponent<MonsterStats>() == null) continue;
            variants.Add(path);
            var go = PrefabUtility.LoadPrefabContents(path);
            try { Drops(go, new[] {speed,heal,large,rearm}, new[]{1f,.1f,.1f,.05f}, new[]{0,0,1,2}, new[]{1f,2f,1f,1f}); PrefabUtility.SaveAsPrefabAsset(go,path); }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        // Stage4-2 keeps both guaranteed drops. Existing item components already own the new effects.
        string runwayMonster = Base + "Stage4_2/P_Stage42_FlyingChaser.prefab";
        var runner = PrefabUtility.LoadPrefabContents(runwayMonster);
        try { Drops(runner,new[]{heal,speed},new[]{1f,1f},new[]{0,0},new[]{1f,1f}); PrefabUtility.SaveAsPrefabAsset(runner,runwayMonster); }
        finally { PrefabUtility.UnloadPrefabContents(runner); }
        var hudPrefab = HudPrefab();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage4.unity");
        var map = Objects(scene);
        var stage = map["StageManager"].GetComponent<StageManager>();
        var player = (Transform)new SerializedObject(stage).FindProperty("player").objectReferenceValue;
        Transform section = map["Section_3"], start = map["ZoneStartPoint_4-3"], end = map["ZoneEndPoint_4-3"];
        float origin = start.position.x;
        // The new 488-wide strip fits inside the existing 4-3 area; 4-4/4-5 stay in place.
        var remove = new List<GameObject>();
        foreach (Transform t in section) if (t != start && t != end) remove.Add(t.gameObject);
        foreach(var go in remove) UnityEngine.Object.DestroyImmediate(go);
        section.position = new Vector3(origin + 240, 2, 0);
        var zone = section.GetComponent<CameraBoundsZone>();
        Set(zone, so => {
            so.FindProperty("size").vector2Value = new Vector2(496,20);
            so.FindProperty("orthographicSize").floatValue = 5;
            so.FindProperty("leftBoundary").objectReferenceValue = map["Boundary_2_3"];
            so.FindProperty("rightBoundary").objectReferenceValue = map["Boundary_3_4"];
        });
        var body = player.GetComponent<Collider2D>();
        start.position = new Vector3(origin, player.position.y - body.bounds.min.y + .03f, 0);
        end.position = new Vector3(origin + 480, 3, 0);
        Set(end.GetComponent<BoxCollider2D>(), so => { so.FindProperty("m_Size").vector2Value=new Vector2(1,16); so.FindProperty("m_IsTrigger").boolValue=true; });
        PrefabUtility.RecordPrefabInstancePropertyModifications(start); PrefabUtility.RecordPrefabInstancePropertyModifications(end);
        var floor = Variant("P_Stage43_NormalRunway", Base+"Platform_Stage4_Dungeon.prefab", go=>{
            foreach(var b in go.GetComponents<PlatformBehaviour>()) UnityEngine.Object.DestroyImmediate(b);
            if (!go.GetComponent<PlatformCore>()) go.AddComponent<PlatformCore>();
            go.layer=LayerMask.NameToLayer("Ground"); go.transform.localScale=Vector3.one;
            var sr=go.GetComponent<SpriteRenderer>(); sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/@ExternalAssets/Tiles/Platform_Stage4_Dungeon_Tiled.png");
            sr.drawMode=SpriteDrawMode.Tiled; sr.tileMode=SpriteTileMode.Continuous; sr.size=new Vector2(488,1); sr.color=Color.white;
            var box=go.GetComponent<BoxCollider2D>(); box.autoTiling=false; box.size=sr.size; box.offset=Vector2.zero;
            go.transform.localScale=Vector3.one;
        });
        var monster = Variant("P_Stage43_FuryChaser", runwayMonster, go=>{
            Drops(go,new[]{fury,speed},new[]{1f,1f},new[]{0,0},new[]{1f,1f});
        });
        var generated = new GameObject("Generated_4-3_Fury").transform; generated.SetParent(section,false); generated.position=new Vector3(origin,0,0);
        var ground=(GameObject)PrefabUtility.InstantiatePrefab(floor,generated); ground.transform.localPosition=new Vector3(240,-.5f,0);
        var manager=generated.gameObject.AddComponent<RunwayEncounterManager>();
        var roots=new GameObject[6];
        for(int g=0;g<6;g++) {
            var group=new GameObject("Encounter_4-3_G"+(g+1).ToString("D2")); group.transform.SetParent(generated,false); roots[g]=group;
            int count=data.groups[g].count;
            for(int i=0;i<count;i++) {
                var m=(GameObject)PrefabUtility.InstantiatePrefab(monster,group.transform);
                m.name="Monster_4-3_G"+(g+1).ToString("D2")+"_"+(i+1).ToString("D2");
                m.GetComponent<MonsterScaleSetter>().ApplyScale(player);
                float x=data.groups[g].startX+8+(count>1?64f*i/(count-1):0);
                m.transform.position=new Vector3(origin+x,i%2==0?1.1f:1.4f,0);
                Set(m.GetComponent<MonsterMovementBounds>(),so=>{
                    so.FindProperty("areaOrigin").objectReferenceValue=generated;
                    so.FindProperty("min").vector2Value=new Vector2(-4,.1f);
                    so.FindProperty("max").vector2Value=new Vector2(484,2.2f);
                    so.FindProperty("constrainY").boolValue=true;
                });
                PrefabUtility.RecordPrefabInstancePropertyModifications(m.transform);
            }
            group.SetActive(false);
        }
        Set(manager,so=>{
            so.FindProperty("stage").objectReferenceValue=stage; so.FindProperty("player").objectReferenceValue=player.GetComponent<PlayerHealth>();
            so.FindProperty("startPoint").objectReferenceValue=start; so.FindProperty("requiredArea").intValue=3;
            so.FindProperty("fury").objectReferenceValue=player.GetComponent<PlayerFuryBuff>();
            var groups=so.FindProperty("groups"); groups.arraySize=6;
            for(int i=0;i<6;i++) { var entry=groups.GetArrayElementAtIndex(i); entry.FindPropertyRelative("distance").floatValue=data.groups[i].startX; entry.FindPropertyRelative("root").objectReferenceValue=roots[i]; }
        });
        var bg=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base+"Stage4_1/P_Stage41_Background.prefab"),generated);
        bg.name="Background_4-3"; Set(bg.GetComponent<ZoneBackground>(),so=>so.FindProperty("zone").objectReferenceValue=zone);
        BindHUD(scene,player,hudPrefab);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        // 3-1 has no monsters in its design; do not invent encounters merely to provide drops.
        scene=EditorSceneManager.OpenScene("Assets/Scenes/Stage3.unity"); map=Objects(scene);
        stage=map["StageManager"].GetComponent<StageManager>(); player=(Transform)new SerializedObject(stage).FindProperty("player").objectReferenceValue;
        BindHUD(scene,player,hudPrefab); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene("Assets/Scenes/Stage4.unity");
        Debug.Log("Item zones deployed: general 4-1 table, 4-2 paired drops, 4-3 Fury Run 105 enemies, shared speed/fury HUD.");
    }

    // Re-running the deployment replaces only this tool's HUD instance.
    static void BindHUD(UnityEngine.SceneManagement.Scene scene,Transform player,GameObject prefab)
    {
        foreach(var root in scene.GetRootGameObjects()) if(root.GetComponent<ItemBuffHUD>()!=null) UnityEngine.Object.DestroyImmediate(root);
        var ui=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        Set(ui.GetComponent<ItemBuffHUD>(),so=>{
            so.FindProperty("speed").objectReferenceValue=player.GetComponent<PlayerSpeedBuff>();
            so.FindProperty("fury").objectReferenceValue=player.GetComponent<PlayerFuryBuff>();
        });
    }
}
