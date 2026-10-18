using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Claude Sonnet 5.5, 2026-10-16. Deploys the 5-5 boss arena (MapDesign_Stage5-5_BossArena.md) and the playable final boss into Stage5.unity.
//  - arena prefab P_Stage5_DemonArena: floor 40x1 (instance of the Stage5 platform prefab P_Stage52_normal), walls at +-20.25 (0.5 x 14), fall trigger at y=-10 (relative to the floor top)
//  - boss prefab P_Boss_Stage5_TwinBladeOverlord: test prefab visuals + Boss (HP 320) + Stage5BossController
//  - Section_5: camera zone 44x18 / ortho 5.5, player start x-16, boss x+14, exit marker x+16 (activates when the boss dies)
// Menu: Tools/Deploy/Stage 5-5 Boss Arena. Refuses to run twice (Arena_5-5 already present).
public static class DeployStage55
{
    const string ScenePath = "Assets/Scenes/Stage5.unity";
    const string Folder = "Assets/@ExternalAssets/@Prefab/Stage5_5/";
    const float FloorTopY = -210f;

    static void Set(UnityEngine.Object target, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target); edit(so); so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    // Floor, walls and fall trigger as one prefab (same approach as the boss arenas of 3-5 / 4-5).
    static GameObject ArenaPrefab()
    {
        string path = Folder + "P_Stage5_DemonArena.prefab";
        var root = new GameObject("P_Stage5_DemonArena");
        try
        {
            int ground = LayerMask.NameToLayer("Ground");
            // Floor = an instance of the Stage5 platform prefab (nested prefab), not a hand-built object (rule 45); only size/position are overrides.
            var floorSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@ExternalAssets/@Prefab/Stage5_2/P_Stage52_normal.prefab");
            if (!floorSource) throw new InvalidOperationException("Stage5 platform prefab P_Stage52_normal is missing");
            var floor = (GameObject)PrefabUtility.InstantiatePrefab(floorSource, root.transform); floor.name = "Floor";
            floor.transform.localPosition = new Vector3(0, -0.25f, 0);   // 0.5 thick like every Stage5 platform; top stays at y 0
            Set(floor.GetComponent<SpriteRenderer>(), so => so.FindProperty("m_Size").vector2Value = new Vector2(40, 0.785f));   // tile height incl. spikes
            Set(floor.GetComponent<BoxCollider2D>(), so => so.FindProperty("m_Size").vector2Value = new Vector2(40, 0.5f));
            foreach (int side in new[] { -1, 1 })
            {
                var wall = new GameObject(side < 0 ? "Wall_Left" : "Wall_Right"); wall.transform.SetParent(root.transform, false); wall.layer = ground;
                wall.transform.localPosition = new Vector3(side * 20.25f, 7f, 0);
                var wb = wall.AddComponent<BoxCollider2D>(); wb.size = new Vector2(0.5f, 14f);
            }
            var oob = new GameObject("OutOfBounds"); oob.transform.SetParent(root.transform, false); oob.transform.localPosition = new Vector3(0, -10f, 0);
            var ob = oob.AddComponent<BoxCollider2D>(); ob.size = new Vector2(46, 2); ob.isTrigger = true; oob.AddComponent<OutOfBoundsTrigger>();
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // Game boss = the approved test visuals (sprites, attack playback, danger boxes) + real health and controller.
    static GameObject BossPrefab()
    {
        string path = Folder + "P_Boss_Stage5_TwinBladeOverlord.prefab";
        var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@ExternalAssets/@Prefab/P_Stage5BossTest.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        try
        {
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "P_Boss_Stage5_TwinBladeOverlord";
            go.layer = 9;   // enemy layer used by the other bosses (the player's attack overlaps this layer)
            var runner = go.GetComponent<Stage5BossTest.Stage5BossComboRunner>(); if (runner) UnityEngine.Object.DestroyImmediate(runner);
            var body = go.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            var col = go.AddComponent<BoxCollider2D>(); col.size = new Vector2(2.4f, 3.6f); col.offset = new Vector2(0, 1.8f);
            var boss = go.AddComponent<Boss>(); Set(boss, so => so.FindProperty("maxHealth").intValue = 320);
            var player = go.GetComponent<Stage5BossTest.Stage5BossAttackPlayer>();
            var visual = (SpriteRenderer)new SerializedObject(player).FindProperty("visual").objectReferenceValue;
            var ctrl = go.AddComponent<Stage5BossController>();
            Set(ctrl, so => { so.FindProperty("attackPlayer").objectReferenceValue = player; so.FindProperty("visual").objectReferenceValue = visual; });
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [MenuItem("Tools/Deploy/Stage 5-5 Boss Arena")]
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
        if (map.ContainsKey("Arena_5-5")) throw new InvalidOperationException("5-5 arena already deployed");
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/@ExternalAssets/@Prefab", "Stage5_5");
        var arenaPrefab = ArenaPrefab(); var bossPrefab = BossPrefab();

        var section = map["Section_5"]; var start = map["ZoneStartPoint_5-5"];
        float cx = section.position.x;
        foreach (var n in new[] { "Platform_Stage5_Hellscape_5-5", "Boss_5-5", "OutOfBounds_5-5" }) UnityEngine.Object.DestroyImmediate(map[n].gameObject);

        // camera zone: centre (0,+5) above the floor top, size 44x18, orthographic 5.5
        section.position = new Vector3(cx, FloorTopY + 5f, 0);
        Set(section.GetComponent<CameraBoundsZone>(), so => { so.FindProperty("size").vector2Value = new Vector2(44, 18); so.FindProperty("orthographicSize").floatValue = 5.5f; });

        var arena = (GameObject)PrefabUtility.InstantiatePrefab(arenaPrefab, section); arena.name = "Arena_5-5";
        arena.transform.position = new Vector3(cx, FloorTopY, 0);

        var boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, section); boss.name = "Boss_5-5";
        boss.transform.position = new Vector3(cx + 14f, FloorTopY, 0);
        Set(boss.GetComponent<Stage5BossController>(), so => { so.FindProperty("minX").floatValue = cx - 19f; so.FindProperty("maxX").floatValue = cx + 19f; });

        start.position = new Vector3(cx - 16f, FloorTopY + 0.03f, 0);
        var exitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@ExternalAssets/@Prefab/P_Stage4_BossExit.prefab");
        var exit = (GameObject)PrefabUtility.InstantiatePrefab(exitPrefab, section); exit.name = "BossZoneEndPoint_5-5";
        exit.transform.position = new Vector3(cx + 16f, FloorTopY + 1f, 0);
        Debug.Log("Stage5-5 deployed: arena 40x1, camera 44x18, boss HP 320, exit at x+16");
    }
}
