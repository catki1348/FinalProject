using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Claude Sonnet 5.5, 2026-10-17. Deploys the 2-5 Stone Serpent arena (MapDesign_Stage2-5_BossArena.md) into Stage2.unity.
// Floor / steps are P_Stage21_Normal instances; the skyways use the one-way variant P_Stage25_Skyway.
// Menu: Tools/Deploy/Stage 2-5 Boss Arena. Safe to run again: it rebuilds the arena platforms by name.
public static class DeployStage25BossArena
{
    const string ScenePath = "Assets/Scenes/Stage2.unity";
    const string NormalPrefab = "Assets/@ExternalAssets/@Prefab/Stage2_1/P_Stage21_Normal.prefab";
    const string SkyPrefabPath = "Assets/@ExternalAssets/@Prefab/Stage2_1/P_Stage25_Skyway.prefab";

    // design numbers (local coordinates, floor top y = 0) - keep in sync with Generate_Stage2-5_BossArena.py
    public static float FloorHalf = 20f;
    public static float StepTop = 1.05f, StepWidth = 3.75f;
    public static float SkyTop = 2.1f, SkyThick = 0.5f, SkyInner = 8.25f, SkyOuter = 15.25f;
    public static float WallX = 20.25f, WallHeight = 14f;
    public static Vector2 Zone = new Vector2(44f, 16f);
    public static float Ortho = 5.5f, DetectRange = 9.5f;
    public static float PlayerStartX = -18f, BossX = 4f, EndX = 18f;

    static void Set(UnityEngine.Object target, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target); edit(so); so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    static void Place(Transform t, Vector3 localPos)
    {
        t.localPosition = localPos;
        EditorUtility.SetDirty(t);
        if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }

    static GameObject SkywayPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SkyPrefabPath);
        if (existing != null) return existing;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(NormalPrefab));
        try
        {
            inst.name = "P_Stage25_Skyway";
            if (!inst.TryGetComponent<PlatformEffector2D>(out var eff)) eff = inst.AddComponent<PlatformEffector2D>();
            eff.useOneWay = true; eff.surfaceArc = 170f; eff.useSideFriction = false; eff.useSideBounce = false;
            inst.GetComponent<BoxCollider2D>().usedByEffector = true;
            return PrefabUtility.SaveAsPrefabAsset(inst, SkyPrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(inst); }
    }

    // creates (or replaces) one arena platform: centre x, top y, width, thickness in Section_5 local space
    static void Platform(Transform section, GameObject prefab, string name, float cx, float top, float width, float thick)
    {
        var old = section.Find(name); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, section);
        go.name = name;
        Place(go.transform, new Vector3(cx, top - thick / 2f, 0f));
        go.transform.localScale = Vector3.one;
        Set(go.GetComponent<SpriteRenderer>(), so => { so.FindProperty("m_Size").vector2Value = new Vector2(width, thick); });
        Set(go.GetComponent<BoxCollider2D>(), so =>
        {
            so.FindProperty("m_AutoTiling").boolValue = false;
            so.FindProperty("m_Size").vector2Value = new Vector2(width, thick);
            so.FindProperty("m_Offset").vector2Value = Vector2.zero;
        });
    }

    [MenuItem("Tools/Deploy/Stage 2-5 Boss Arena")]
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

    public static void Build(Scene scene)
    {
        var map = new Dictionary<string, Transform>();
        foreach (var r in scene.GetRootGameObjects()) foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(t.name)) map.Add(t.name, t);
        var section = map["Section_5"];
        var normal = AssetDatabase.LoadAssetAtPath<GameObject>(NormalPrefab);
        var sky = SkywayPrefab();

        // 1. platforms: the old scaled 28-wide floor goes away
        if (map.TryGetValue("Platform_Stage2_Jungle_2-5", out var oldFloor)) UnityEngine.Object.DestroyImmediate(oldFloor.gameObject);
        Platform(section, normal, "Platform_Floor_2-5", 0f, 0f, FloorHalf * 2f, 1f);
        Platform(section, normal, "Platform_StepL_2-5", -FloorHalf + StepWidth / 2f, StepTop, StepWidth, StepTop);
        Platform(section, normal, "Platform_StepR_2-5", FloorHalf - StepWidth / 2f, StepTop, StepWidth, StepTop);
        float skyW = SkyOuter - SkyInner, skyC = (SkyOuter + SkyInner) / 2f;
        Platform(section, sky, "Platform_SkyL_2-5", -skyC, SkyTop, skyW, SkyThick);
        Platform(section, sky, "Platform_SkyR_2-5", skyC, SkyTop, skyW, SkyThick);

        // 2. walls, out-of-bounds, start / boss / end
        foreach (var side in new[] { -1, 1 })
        {
            var wall = map[side < 0 ? "ArenaWall_Left_2-5" : "ArenaWall_Right_2-5"];
            wall.localPosition = new Vector3(side * WallX, WallHeight / 2f - 1f, 0f);
            wall.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, WallHeight);
            EditorUtility.SetDirty(wall.gameObject);
        }
        var oob = map["OutOfBoundsTrigger_2-5"];
        Place(oob, new Vector3(0f, -10f, 0f));
        Set(oob.GetComponent<BoxCollider2D>(), so => so.FindProperty("m_Size").vector2Value = new Vector2(Zone.x, 2f));
        Place(map["ZoneStartPoint_2-5"], new Vector3(PlayerStartX, StepTop + 1f, 0f));
        Place(map["Boss_2-5"], new Vector3(BossX, 0.02f, 0f));
        Place(map["BossZoneEndPoint_2-5"], new Vector3(EndX, StepTop + 1f, 0f));
        Set(map["Boss_2-5"].GetComponent("StoneSerpentController"), so => so.FindProperty("detectRange").floatValue = DetectRange);

        // 3. camera zone + backgrounds
        Set(section.GetComponent("CameraBoundsZone"), so =>
        {
            so.FindProperty("size").vector2Value = Zone;
            so.FindProperty("orthographicSize").floatValue = Ortho;
        });
        FitBackground(map["ZoneBackground_2-5"], Zone.x + 4f, Zone.y + 4f);
        if (map.TryGetValue("WorldBackground_2-5", out var world)) FitBackground(world, Zone.x + 6f, Zone.y + 6f);
    }

    // Tiled background: widen the sprite size so the world extent covers w x h without changing its scale.
    static void FitBackground(Transform bg, float w, float h)
    {
        var sr = bg.GetComponent<SpriteRenderer>();
        Set(sr, so => so.FindProperty("m_Size").vector2Value = new Vector2(w / bg.lossyScale.x, h / bg.lossyScale.y));
    }
}
