using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Claude Sonnet 5.5, 2026-10-16. Deploys the 2-1 redesign (MapDesign_Stage2-1.md / _Redesign.json) into Stage2.unity.
// 17 flat platforms (7 normal / 5 damage / 5 blinking), one shared PlatformCycleManager clock, later areas shifted right.
// Menu: Tools/Deploy/Stage 2-1 Redesign. Idempotency guard: refuses to run when Boundary_1_2 has already moved.
public static class DeployStage21Redesign
{
    const string ScenePath = "Assets/Scenes/Stage2.unity";
    const string Folder = "Assets/@ExternalAssets/@Prefab/Stage2_1/";
    const string BasePrefab = "Assets/@ExternalAssets/@Prefab/Platform_Stage2_Jungle.prefab";
    const float OldRight = 50f;      // Boundary_1_2 before this deployment
    const float Origin = 10f;        // world x of design x 0
    const float NewRight = 500f;     // design bounds -10..490 -> world 0..500
    [Serializable] public class P { public string id, name, type; public float x, surfaceY, width, thickness; }
    [Serializable] public class Pt { public float x, surfaceY; }
    [Serializable] public class Bounds { public float minX, maxX, minY, maxY; }
    [Serializable] public class Design { public P[] platforms; public Pt start, goal; public Bounds bounds; }

    // Records the change on prefab instances so it is not lost when the prefab resynchronizes.
    static void Set(UnityEngine.Object target, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target); edit(so); so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    // 0.5-high tile cut from the interior of the jungle texture (same crop as 2-2, scaled to the 0.5 thickness).
    static Sprite Tile()
    {
        string path = Folder + "T_Stage21_Platform.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (tile) return tile;
        var source = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/@ExternalAssets/Tiles/Platform_Stage2_Jungle.png");
        tile = Sprite.Create(source.texture, new Rect(280, 264, 400, 400), new Vector2(.5f, .5f), 400 / 0.5f, 0, SpriteMeshType.FullRect, Vector4.zero);
        tile.name = "T_Stage21_Platform"; AssetDatabase.CreateAsset(tile, path); return tile;
    }

    // One prefab per behaviour; Damage and Blinking are externally controlled by the shared clock.
    static GameObject PlatformPrefab(string kind, Sprite tile)
    {
        string path = Folder + "P_Stage21_" + kind + ".prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        var go = exists ? PrefabUtility.LoadPrefabContents(path) : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab));
        try
        {
            go.name = "P_Stage21_" + kind;
            foreach (var b in go.GetComponents<PlatformBehaviour>()) UnityEngine.Object.DestroyImmediate(b);
            if (!go.GetComponent<PlatformCore>()) go.AddComponent<PlatformCore>();
            if (kind == "Damage") go.AddComponent<DamageFoothold>();
            if (kind == "Blinking") go.AddComponent<BlinkingFoothold>();
            go.layer = LayerMask.NameToLayer("Ground");
            go.transform.localScale = Vector3.one;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = tile; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous; sr.size = new Vector2(3, .5f);
            var box = go.GetComponent<BoxCollider2D>(); box.autoTiling = false; box.size = sr.size; box.offset = Vector2.zero;
            if (go.TryGetComponent<Rigidbody2D>(out var body)) { body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation; }
            if (kind == "Damage") Set(go.GetComponent<DamageFoothold>(), so =>
            {
                so.FindProperty("externallyControlled").boolValue = true;
                so.FindProperty("damageDelay").floatValue = .5f; so.FindProperty("damageAmount").intValue = 8; so.FindProperty("damageInterval").floatValue = .5f;
                so.FindProperty("damageEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/@ExternalAssets/@Prefab/P_DamageFootholdEffect.prefab");
            });
            if (kind == "Blinking") Set(go.GetComponent<BlinkingFoothold>(), so => { so.FindProperty("externallyControlled").boolValue = true; so.FindProperty("warningDuration").floatValue = .8f; });
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(go); else UnityEngine.Object.DestroyImmediate(go); }
    }

    [MenuItem("Tools/Deploy/Stage 2-1 Redesign")]
    public static void Deploy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop play mode before deployment");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (scene.isDirty) throw new InvalidOperationException("Save current work before switching scenes");
            scene = EditorSceneManager.OpenScene(ScenePath);
        }
        Build(scene);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }

    // Also callable on an isolated copy of the scene for validation.
    public static void Build(Scene scene)
    {
        var d = JsonUtility.FromJson<Design>(File.ReadAllText("@Edit/MapDesign_Stage2-1_Redesign.json"));
        if (d.platforms.Length != 17) throw new InvalidOperationException("Unexpected 2-1 platform count");
        var map = new Dictionary<string, Transform>();
        foreach (var r in scene.GetRootGameObjects()) foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(t.name)) map.Add(t.name, t);
        if (map["Boundary_1_2"].position.x > OldRight + 1f) throw new InvalidOperationException("Already deployed (Boundary_1_2 moved)");
        float delta = NewRight - map["Boundary_1_2"].position.x;

        // 1. shift later areas right so the 480-unit 2-1 fits; the boss zone stays centred under areas 1-4
        foreach (var n in new[] { "Section_2", "Section_3", "Section_4", "Boundary_2_3", "Boundary_3_4", "Boundary_4_5", "WorldBackground_2-2", "WorldBackground_2-3", "WorldBackground_2-4" })
            map[n].position += Vector3.right * delta;
        foreach (var n in new[] { "Section_5", "WorldBackground_2-5" }) map[n].position += Vector3.right * (delta / 2f);
        map["Boundary_1_2"].position = new Vector3(NewRight, map["Boundary_1_2"].position.y, 0);

        // 2. keep player / start / end / zone background, drop the old six-platform test build
        var section = map["Section_1"];
        var player = map["Player_Warrior"]; var start = map["ZoneStartPoint_2-1"]; var end = map["ZoneEndPoint_2-1"]; var stage = map["StageManager"].GetComponent<StageManager>();
        var keep = new HashSet<Transform> { player, start, end, map["ZoneBackground_2-1"], map["CameraBoundsZone_2-1"] };
        var remove = new List<GameObject>();
        foreach (Transform t in section) if (!keep.Contains(t)) remove.Add(t.gameObject);
        foreach (var go in remove) UnityEngine.Object.DestroyImmediate(go);

        // 3. camera zone: world x 0..500, y -8..10 (children are positioned in world space afterwards)
        float cy = (d.bounds.minY + d.bounds.maxY) * .5f;
        section.position = new Vector3(NewRight * .5f, cy, 0);
        Set(section.GetComponent<CameraBoundsZone>(), so => so.FindProperty("size").vector2Value = new Vector2(NewRight, d.bounds.maxY - d.bounds.minY));
        map["ZoneBackground_2-1"].position = new Vector3(NewRight * .5f, cy, map["ZoneBackground_2-1"].position.z);
        map["WorldBackground_2-1"].position = new Vector3(NewRight * .5f, cy, map["WorldBackground_2-1"].position.z);
        var wbSr = map["WorldBackground_2-1"].GetComponent<SpriteRenderer>();
        if (wbSr) { var s = wbSr.sprite.bounds.size; float k = Mathf.Max(NewRight / s.x, (d.bounds.maxY - d.bounds.minY) / s.y); map["WorldBackground_2-1"].localScale = new Vector3(k, k, 1); }
        map["CameraBoundsZone_2-1"].position = new Vector3(NewRight * .5f, cy, map["CameraBoundsZone_2-1"].position.z);
        Set(map["CameraBoundsZone_2-1"].GetComponent<BoxCollider2D>(), so => so.FindProperty("m_Size").vector2Value = new Vector2(NewRight, d.bounds.maxY - d.bounds.minY));
        // fall line just below the platforms (design: Y=-6); one trigger spans 2-1 and 2-2 as the old one did
        var oob = map["OutOfBoundsTrigger"];
        oob.localScale = new Vector3((622f + 34f) / 200f, 1, 1); oob.position = new Vector3((-30f + 656f) * .5f, -7f, 0);
        EditorUtility.SetDirty(oob);

        // 4. platforms
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/@ExternalAssets/@Prefab", "Stage2_1");
        var tile = Tile();
        var prefabs = new Dictionary<string, GameObject>();
        var root = new GameObject("Generated_2-1_Redesign").transform; root.SetParent(section, false); root.position = new Vector3(Origin, 0, 0);
        var damages = new List<DamageFoothold>(); var blinks = new List<BlinkingFoothold>();
        foreach (var p in d.platforms)
        {
            if (!prefabs.ContainsKey(p.type)) prefabs[p.type] = PlatformPrefab(p.type, tile);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[p.type], root);
            go.name = "Platform_2-1_" + p.id + "_" + p.name + "_" + p.type;
            go.transform.localPosition = new Vector3(p.x, p.surfaceY - p.thickness * .5f, 0); go.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            Set(go.GetComponent<SpriteRenderer>(), so => so.FindProperty("m_Size").vector2Value = new Vector2(p.width, p.thickness));
            Set(go.GetComponent<BoxCollider2D>(), so => { so.FindProperty("m_Size").vector2Value = new Vector2(p.width, p.thickness); so.FindProperty("m_Offset").vector2Value = Vector2.zero; });
            if (p.type == "Damage") damages.Add(go.GetComponent<DamageFoothold>());
            if (p.type == "Blinking") blinks.Add(go.GetComponent<BlinkingFoothold>());
        }

        // 5. player start and goal
        var col = player.GetComponent<Collider2D>();
        float feet = player.position.y - col.bounds.min.y;
        start.position = new Vector3(Origin + d.start.x + col.bounds.extents.x + 1f, d.start.surfaceY + feet + .03f, 0);
        player.position = start.position;
        end.position = new Vector3(Origin + d.goal.x, d.goal.surfaceY + 1.5f, 0);

        // 6. one shared clock for every damage / blink platform (starts when the area is entered)
        var manager = root.gameObject.AddComponent<PlatformCycleManager>();
        Set(manager, so =>
        {
            so.FindProperty("stage").objectReferenceValue = stage; so.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerHealth>();
            so.FindProperty("startPoint").objectReferenceValue = start; so.FindProperty("requiredArea").intValue = 1;
            var clocks = so.FindProperty("clocks"); clocks.arraySize = 1; var c = clocks.GetArrayElementAtIndex(0);
            c.FindPropertyRelative("enterDistance").floatValue = -1000f; c.FindPropertyRelative("exitDistance").floatValue = 1000f;
            var dArr = c.FindPropertyRelative("damages"); dArr.arraySize = damages.Count; for (int i = 0; i < damages.Count; i++) dArr.GetArrayElementAtIndex(i).objectReferenceValue = damages[i];
            var bArr = c.FindPropertyRelative("blinks"); bArr.arraySize = blinks.Count; for (int i = 0; i < blinks.Count; i++) bArr.GetArrayElementAtIndex(i).objectReferenceValue = blinks[i];
        });
        Debug.Log("Stage2-1 redesign deployed: " + d.platforms.Length + " platforms (" + damages.Count + " damage, " + blinks.Count + " blinking); later areas shifted +" + delta);
    }
}
