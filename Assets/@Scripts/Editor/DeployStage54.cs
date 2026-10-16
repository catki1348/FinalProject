using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Claude Sonnet 5.5, 2026-10-16. Deploys MapDesign_Stage5-4.json (demon-castle final trial) into Stage5.unity Section_4.
public static class DeployStage54
{
    const string Base = "Assets/@ExternalAssets/@Prefab/";
    const string Folder = Base + "Stage5_4/";
    const string Trap = Base + "Trap/Themed/Stage5_DemonCastle/";
    const string Json = "@Edit/MapDesign_Stage5-4.json";
    const float OriginX = 1536f;       // design x 0; left edge (origin-10) meets Boundary_3_4 (1526)
    const float BoundaryRight = 2136f;
    const float MinY = -12f, MaxY = 24f;

    [Serializable] public class Point { public float x, y; }
    [Serializable] public class Platform { public string id, kind, mode, timer; public int part; public float width, thickness, speed, startDelay, endpointWait, fallAt; public Point[] points; }
    [Serializable] public class TrapData { public string kind, host; public float[] launcher, @switch, direction, destination, bounds; public float warning, cooldown, speed, damage, lifetime; }
    [Serializable] public class Monster { public string id, pattern, activation, host; public int part; public float x, y, patrolHalfWidth; }
    [Serializable] public class FallRun { public float timerStartX; }
    [Serializable] public class Segment { public int part; public FallRun fallRun; }
    [Serializable] public class Design { public Platform[] platforms; public TrapData[] traps; public Monster[] monsters; public float[] goal; }

    static void Set(UnityEngine.Object target, Action<SerializedObject> configure)
    {
        var so = new SerializedObject(target); configure(so); so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    static GameObject Variant(string name, string source, Action<GameObject> configure)
    {
        string path = Folder + name + ".prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        var go = exists ? PrefabUtility.LoadPrefabContents(path) : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try { configure(go); return PrefabUtility.SaveAsPrefabAsset(go, path); }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(go); else UnityEngine.Object.DestroyImmediate(go); }
    }

    static PlatformPath MakePath(Transform parent, string name, Point[] pts, float dy)
    {
        var root = new GameObject(name).transform; root.SetParent(parent, false); var path = root.gameObject.AddComponent<PlatformPath>();
        for (int i = 0; i < pts.Length; i++) { var p = new GameObject("Point_" + i).transform; p.SetParent(root, false); p.localPosition = new Vector3(pts[i].x, pts[i].y + dy, 0); }
        return path;
    }

    // Time-driven support: the clock lives in TimedRunManager, so no contact trigger exists here.
    static GameObject FallingPrefab()
    {
        return Variant("P_Stage54_Falling", Base + "Stage5_2/P_Stage52_normal.prefab", go =>
        {
            if (!go.GetComponent<SequentialFallingPlatform>()) go.AddComponent<SequentialFallingPlatform>();
            var effector = go.GetComponent<PlatformEffector2D>(); if (effector) UnityEngine.Object.DestroyImmediate(effector);
            var box = go.GetComponent<BoxCollider2D>(); box.usedByEffector = false;
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(Base + "Stage4_4/M_Stage44_Frictionless.physicsMaterial2D");
            if (material) box.sharedMaterial = material;
            go.GetComponent<SpriteRenderer>().color = new Color(.85f, .75f, 1f);
        });
    }

    [MenuItem("Tools/Deploy/Stage 5-4 Demon Castle Trial")]
    public static void Deploy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop play mode");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Stage5.unity") throw new InvalidOperationException("Stage5 must be the active scene");
        var d = JsonUtility.FromJson<Design>(File.ReadAllText(Json));
        if (d.platforms.Length != 58 || d.traps.Length != 4 || d.monsters.Length != 7) throw new InvalidOperationException("Unexpected 5-4 design counts");
        var map = new Dictionary<string, Transform>();
        foreach (var r in scene.GetRootGameObjects()) foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(t.name)) map.Add(t.name, t);
        var section = map["Section_4"]; var start = map["ZoneStartPoint_5-4"]; var end = map["ZoneEndPoint_5-4"];
        var stage = map["StageManager"].GetComponent<StageManager>();
        var player = (Transform)new SerializedObject(stage).FindProperty("player").objectReferenceValue;
        var speedBuff = player.GetComponent<PlayerSpeedBuff>();
        if (!speedBuff) throw new InvalidOperationException("Player lacks PlayerSpeedBuff");
        if (map["Boundary_4_5"].position.x > BoundaryRight - 1f) throw new InvalidOperationException("Already deployed");
        float left = map["Boundary_3_4"].position.x;
        if (Mathf.Abs(OriginX - 10f - left) > .01f) throw new InvalidOperationException("5-3 must be deployed first (Boundary_3_4 expected at " + (OriginX - 10f) + ")");

        // 1. area box, boundaries and fall line
        map["Boundary_4_5"].position = new Vector3(BoundaryRight, map["Boundary_4_5"].position.y, 0);
        var remove = new List<GameObject>(); foreach (Transform c in section) if (c != start && c != end) remove.Add(c.gameObject);
        foreach (var go in remove) UnityEngine.Object.DestroyImmediate(go);
        section.position = new Vector3((left + BoundaryRight) / 2, (MinY + MaxY) / 2, 0);
        var zone = section.GetComponent<CameraBoundsZone>();
        Set(zone, so => { so.FindProperty("size").vector2Value = new Vector2(BoundaryRight - left, MaxY - MinY); so.FindProperty("orthographicSize").floatValue = 5; });
        var fall = map["OutOfBounds_5-3_5-4"]; var fallBox = fall.GetComponent<BoxCollider2D>();
        float fl = map["Boundary_2_3"].position.x;
        fall.position = new Vector3((fl + BoundaryRight) / 2, -20, 0); Set(fallBox, so => so.FindProperty("m_Size").vector2Value = new Vector2(BoundaryRight - fl, 1));
        // boss area stays centred under the widened upper row (MapEdit 19)
        float upperLeft = map["Section_1"].GetComponent<CameraBoundsZone>().WorldBounds.min.x;
        map["Section_5"].position = new Vector3((upperLeft + BoundaryRight) / 2, map["Section_5"].position.y, 0);

        // 2. platforms
        var root = new GameObject("Generated_5-4").transform; root.SetParent(section, false); root.position = new Vector3(OriginX, 0, 0);
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder(Base.TrimEnd('/'), "Stage5_4");
        var prefabs = new Dictionary<string, GameObject>();
        var falling = new Dictionary<string, List<(SequentialFallingPlatform p, float t)>>();
        int dmgIndex = 0;
        foreach (var p in d.platforms)
        {
            string kind = p.kind == "Disappear" ? "disappearing" : p.kind == "SequentialFalling" ? "falling" : p.kind.ToLowerInvariant();
            float thick = p.thickness > 0 ? p.thickness : .5f;
            if (!prefabs.ContainsKey(kind)) prefabs[kind] = kind == "falling" ? FallingPrefab() : AssetDatabase.LoadAssetAtPath<GameObject>(Base + "Stage5_2/P_Stage52_" + kind + ".prefab");
            if (!prefabs[kind]) throw new InvalidOperationException("Missing prefab for " + kind);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind], root); go.name = "Platform_5-4_" + p.id;
            go.transform.localPosition = new Vector3(p.points[0].x, p.points[0].y - thick / 2, 0);
            Set(go.GetComponent<SpriteRenderer>(), so => so.FindProperty("m_Size").vector2Value = new Vector2(p.width, thick));
            Set(go.GetComponent<BoxCollider2D>(), so => { so.FindProperty("m_Size").vector2Value = new Vector2(p.width, thick); so.FindProperty("m_Offset").vector2Value = Vector2.zero; });
            go.transform.localScale = Vector3.one;
            if (kind == "moving")
            {
                bool once = p.mode == "OnceOnBoard";
                var path = MakePath(root, "Path_5-4_" + p.id, p.points, -thick / 2);
                Set(go.GetComponent<MovingPlatform>(), so =>
                {
                    so.FindProperty("path").objectReferenceValue = path; so.FindProperty("speed").floatValue = p.speed;
                    so.FindProperty("startDelay").floatValue = p.startDelay; so.FindProperty("endpointWait").floatValue = p.endpointWait;
                    so.FindProperty("loopMode").enumValueIndex = once ? 2 : 1; so.FindProperty("startOnFirstBoard").boolValue = once;
                    so.FindProperty("offscreenSpeedMultiplier").floatValue = 1;
                });
                go.transform.position = path.GetPoint(0);
            }
            else if (kind == "damage")
            {
                int idx = dmgIndex++;
                Set(go.GetComponent<DamageFoothold>(), so =>
                {
                    so.FindProperty("safeDuration").floatValue = 2.4f; so.FindProperty("activeDuration").floatValue = 1.6f;
                    so.FindProperty("damageDelay").floatValue = .5f; so.FindProperty("damageAmount").intValue = 8; so.FindProperty("damageInterval").floatValue = .5f;
                    so.FindProperty("cycleOffset").floatValue = idx * .3f;
                    so.FindProperty("damageEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ParticleSystem>(Base + "P_DamageFootholdEffect.prefab");
                });
            }
            else if (kind == "blink") Set(go.GetComponent<BlinkingFoothold>(), so => { so.FindProperty("visibleDuration").floatValue = 2.4f; so.FindProperty("invisibleDuration").floatValue = 1.2f; });
            else if (kind == "disappearing") Set(go.GetComponent<DisappearingFoothold>(), so => { so.FindProperty("vanishDelay").floatValue = .9f; so.FindProperty("respawnDelay").floatValue = 2f; });
            else if (kind == "falling")
            {
                if (!falling.ContainsKey(p.timer)) falling[p.timer] = new List<(SequentialFallingPlatform, float)>();
                falling[p.timer].Add((go.GetComponent<SequentialFallingPlatform>(), p.fallAt));
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        var hostById = d.platforms.ToDictionary(p => p.id);

        // 3. start / end markers
        float feet = player.position.y - player.GetComponent<Collider2D>().bounds.min.y;
        start.position = new Vector3(OriginX, d.platforms[0].points[0].y + feet + .03f, 0);
        end.position = new Vector3(OriginX + d.goal[0], d.goal[1] + 1, 0);
        Set(end.GetComponent<BoxCollider2D>(), so => { so.FindProperty("m_Size").vector2Value = new Vector2(.8f, 6); so.FindProperty("m_IsTrigger").boolValue = true; });
        PrefabUtility.RecordPrefabInstancePropertyModifications(start); PrefabUtility.RecordPrefabInstancePropertyModifications(end);

        // 4. traps
        var cameraFollow = Camera.main.GetComponent<PlayerCameraFollow>();
        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(Trap + "P_Trap_Projectile_Stage5_DemonCastle.prefab");
        foreach (var t in d.traps)
        {
            if (t.kind == "ArrowSwitch")
            {
                var sw = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Trap + "P_Trap_Switch_Stage5_DemonCastle.prefab"), root);
                sw.name = "Trap_5-4_" + t.host + "_Switch"; sw.transform.position = new Vector3(OriginX + t.@switch[0], hostById[t.host].points[0].y + .02f, 0);  // mounted ON the surface (the design's +0.7 left it floating)
                Set(sw.GetComponent<SwitchTrigger>(), so => so.FindProperty("cooldown").floatValue = t.cooldown);
                var launcher = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Trap + "P_Trap_Launcher_Stage5_DemonCastle.prefab"), root);
                launcher.name = "Trap_5-4_" + t.host + "_Launcher"; launcher.transform.position = new Vector3(OriginX + t.launcher[0], t.launcher[1], 0);
                launcher.transform.localScale = new Vector3(.6f, .6f, 1);
                var pl = launcher.GetComponent<ProjectileLauncher>();
                Set(pl, so =>
                {
                    so.FindProperty("switchTrigger").objectReferenceValue = sw.GetComponent<SwitchTrigger>();
                    so.FindProperty("projectilePrefab").objectReferenceValue = projectile.GetComponent<TrapProjectile>();
                    so.FindProperty("launchDirection").vector2Value = new Vector2(t.direction[0], t.direction[1]); so.FindProperty("windupSeconds").floatValue = t.warning;
                });
                StandardizeArrowTrapPrefabs.ConfigureInstance(pl, launcher.transform.position);
            }
            else
            {
                // zone sensor that returns a fallen player to the high rest platform without resetting anything else
                var go = new GameObject("Trap_5-4_FallRecovery"); go.transform.SetParent(root, false);
                go.transform.position = new Vector3(OriginX + (t.bounds[0] + t.bounds[1]) / 2, (t.bounds[2] + t.bounds[3]) / 2, 0);
                var box = go.AddComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(t.bounds[1] - t.bounds[0], t.bounds[3] - t.bounds[2]);
                var dest = new GameObject("RecoveryDestination").transform; dest.SetParent(go.transform, false);
                dest.position = new Vector3(OriginX + t.destination[0], t.destination[1], 0);
                var tp = go.AddComponent<TeleportTrap>();
                Set(tp, so =>
                {
                    so.FindProperty("oneShot").boolValue = false; so.FindProperty("retriggerCooldown").floatValue = t.cooldown;
                    so.FindProperty("destination").objectReferenceValue = dest; so.FindProperty("destinationZone").objectReferenceValue = zone;
                    so.FindProperty("cameraFollow").objectReferenceValue = cameraFollow; so.FindProperty("penaltyDamage").intValue = 0;
                    so.FindProperty("teleportSfx").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/@ExternalAssets/Sound/SFX_Trap_Teleport.wav");
                });
            }
        }

        // 5. monsters
        var patrol = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "Stage4_1/P_Stage41_PatrolPattern.prefab");
        var chaser = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "Stage4_4/P_Stage44_FlyingChaser.prefab");
        if (!patrol || !chaser) throw new InvalidOperationException("Monster prefabs missing");
        var encounters = new Dictionary<string, GameObject>();
        foreach (var key in d.monsters.Where(m => m.activation != null && m.activation != "").Select(m => m.activation).Distinct())
        { var g = new GameObject("Encounter_5-4_" + key); g.transform.SetParent(root, false); encounters[key] = g; }
        foreach (var m in d.monsters)
        {
            bool isPatrol = m.pattern == "PatrolPattern";
            var parent = isPatrol ? root : encounters[m.activation].transform;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(isPatrol ? patrol : chaser, parent); go.name = "Monster_5-4_" + m.id;
            go.GetComponent<MonsterScaleSetter>().ApplyScale(player);
            go.transform.position = new Vector3(OriginX + m.x, m.y, 0);
            if (isPatrol)
            {
                Set(go.GetComponent<PatrolPattern>(), so => so.FindProperty("areaSize").vector2Value = new Vector2(m.patrolHalfWidth * 2, 2));
                Physics2D.SyncTransforms();
                float surface = hostById[m.host].points[0].y;
                go.transform.position += Vector3.up * (surface + .02f - go.GetComponent<Collider2D>().bounds.min.y);
            }
            else
            {
                float surface = m.part == 4 ? 8.8f : 11.4f;
                Set(go.GetComponent<MonsterMovementBounds>(), so =>
                {
                    so.FindProperty("areaOrigin").objectReferenceValue = root; so.FindProperty("min").vector2Value = new Vector2(-4, surface - 2.8f);
                    so.FindProperty("max").vector2Value = new Vector2(604, surface + 5.2f); so.FindProperty("constrainY").boolValue = true;
                });
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        foreach (var g in encounters.Values) g.SetActive(false);

        // 6. two independent falling runs; speed buff only inside each run's own range
        float p3k = hostById["P3_K"].points[0].x, p4End = hostById["P4_Run_15"].points[0].x + hostById["P4_Run_15"].width / 2;
        float entry = hostById["P5_RunEntry"].points[0].x, p5End = hostById["P5_Run_6"].points[0].x + hostById["P5_Run_6"].width / 2;
        float clock4 = hostById["P3_K"].points[0].x + hostById["P3_K"].width / 2, clock5 = entry + hostById["P5_RunEntry"].width / 2;
        var manager = root.gameObject.AddComponent<TimedRunManager>();
        var spec = new[] { ("P4_Run", p3k, clock4, p4End + .5f), ("P5_Run", entry, clock5, p5End + 4f) };
        Set(manager, so =>
        {
            so.FindProperty("stage").objectReferenceValue = stage; so.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerHealth>();
            so.FindProperty("speed").objectReferenceValue = speedBuff; so.FindProperty("startPoint").objectReferenceValue = start;
            so.FindProperty("requiredArea").intValue = 4; so.FindProperty("minDistance").floatValue = -8; so.FindProperty("maxDistance").floatValue = 612;
            var runs = so.FindProperty("runs"); runs.arraySize = spec.Length;
            for (int i = 0; i < spec.Length; i++)
            {
                var e = runs.GetArrayElementAtIndex(i); var list = falling[spec[i].Item1];
                e.FindPropertyRelative("buffStart").floatValue = spec[i].Item2; e.FindPropertyRelative("clockStart").floatValue = spec[i].Item3; e.FindPropertyRelative("buffEnd").floatValue = spec[i].Item4;
                var plats = e.FindPropertyRelative("platforms"); var times = e.FindPropertyRelative("fallAt"); plats.arraySize = times.arraySize = list.Count;
                for (int k = 0; k < list.Count; k++) { plats.GetArrayElementAtIndex(k).objectReferenceValue = list[k].p; times.GetArrayElementAtIndex(k).floatValue = list[k].t; }
                e.FindPropertyRelative("encounter").objectReferenceValue = encounters[spec[i].Item1];
            }
        });

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Stage5-4 deployed: " + d.platforms.Length + " platforms, " + d.traps.Length + " traps, " + d.monsters.Length + " monsters, 2 timed runs");
    }
}
