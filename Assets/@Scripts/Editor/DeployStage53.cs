using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Claude Sonnet 5.5, 2026-10-16. Deploys MapDesign_Stage5-3.json (inherit + fuse: aerial rage corridor) into Stage5.unity Section_3.
public static class DeployStage53
{
    const string Base = "Assets/@ExternalAssets/@Prefab/";
    const string Folder = Base + "Stage5_3/";
    const string Trap = Base + "Trap/Themed/Stage5_DemonCastle/";
    const string Json = "@Edit/MapDesign_Stage5-3.json";
    const float OriginX = 926f;   // design x 0; left edge (origin-10) meets Boundary_2_3 (916)
    const float BoundaryRight = 1526f;
    const float MinY = -12f, MaxY = 25f;

    [Serializable] public class Point { public float x, y; public Vector2 V => new Vector2(x, y); }
    [Serializable] public class Platform { public string id, kind; public int part; public float width, thickness, speed, startDelay, endpointWait; public Point[] points; }
    [Serializable] public class Trap_ { public string kind, host, target, destination; public float warning, cooldown, projectileSpeed, damage, lifetime; }
    [Serializable] public class Monster { public string id, pattern, drops, activation; public int part; public float x, y, activationX; }
    [Serializable] public class Design { public Platform[] platforms; public Trap_[] traps; public Monster[] monsters; public float[] goal; }

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

    [MenuItem("Tools/Deploy/Stage 5-3 Aerial Rage Corridor")]
    public static void Deploy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop play mode");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Stage5.unity") throw new InvalidOperationException("Stage5 must be the active scene");
        var d = JsonUtility.FromJson<Design>(File.ReadAllText(Json));
        if (d.platforms.Length != 45 || d.monsters.Length != 54 || d.traps.Length != 7) throw new InvalidOperationException("Unexpected 5-3 design counts");
        var map = new Dictionary<string, Transform>();
        foreach (var r in scene.GetRootGameObjects()) foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(t.name)) map.Add(t.name, t);
        var section = map["Section_3"]; var start = map["ZoneStartPoint_5-3"]; var end = map["ZoneEndPoint_5-3"];
        var stage = map["StageManager"].GetComponent<StageManager>();
        var player = (Transform)new SerializedObject(stage).FindProperty("player").objectReferenceValue;
        if (!player.GetComponent<PlayerFuryBuff>() || !player.GetComponent<PlayerSpeedBuff>()) throw new InvalidOperationException("Player lacks buff components");
        float oldRight = map["Boundary_3_4"].position.x;
        if (oldRight > BoundaryRight - 1f) throw new InvalidOperationException("Already deployed");
        float delta = BoundaryRight - oldRight;

        // 1. make room: 5-4 and its boundaries move right, fall line covers the longer 5-3/5-4 span
        foreach (var n in new[] { "Section_4", "Boundary_3_4", "Boundary_4_5" }) map[n].position += Vector3.right * delta;
        var fall = map["OutOfBounds_5-3_5-4"]; var fallBox = fall.GetComponent<BoxCollider2D>();
        float fl = map["Boundary_2_3"].position.x, fr = map["Boundary_4_5"].position.x;
        fall.position = new Vector3((fl + fr) / 2, -20, 0); Set(fallBox, so => so.FindProperty("m_Size").vector2Value = new Vector2(fr - fl, 1));

        // 2. clear placeholder floor, set the camera zone
        var remove = new List<GameObject>(); foreach (Transform c in section) if (c != start && c != end) remove.Add(c.gameObject);
        foreach (var go in remove) UnityEngine.Object.DestroyImmediate(go);
        float left = map["Boundary_2_3"].position.x;
        section.position = new Vector3((left + BoundaryRight) / 2, (MinY + MaxY) / 2, 0);
        var zone = section.GetComponent<CameraBoundsZone>();
        Set(zone, so => { so.FindProperty("size").vector2Value = new Vector2(BoundaryRight - left, MaxY - MinY); so.FindProperty("orthographicSize").floatValue = 5; });

        // 3. platforms (reuse the 5-2 hellscape variants)
        var root = new GameObject("Generated_5-3").transform; root.SetParent(section, false); root.position = new Vector3(OriginX, 0, 0);
        var hostById = new Dictionary<string, (Platform p, GameObject go)>();
        var prefabs = new Dictionary<string, GameObject>();
        int dmgIndex = 0;
        foreach (var p in d.platforms)
        {
            string kind = p.kind.ToLowerInvariant();
            if (!prefabs.ContainsKey(kind)) prefabs[kind] = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "Stage5_2/P_Stage52_" + kind + ".prefab");
            if (!prefabs[kind]) throw new InvalidOperationException("Missing prefab for " + kind);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind], root); go.name = "Platform_5-3_" + p.id;
            go.transform.localPosition = new Vector3(p.points[0].x, p.points[0].y - p.thickness / 2, 0);
            Set(go.GetComponent<SpriteRenderer>(), so => so.FindProperty("m_Size").vector2Value = new Vector2(p.width, p.thickness));
            Set(go.GetComponent<BoxCollider2D>(), so => { so.FindProperty("m_Size").vector2Value = new Vector2(p.width, p.thickness); so.FindProperty("m_Offset").vector2Value = Vector2.zero; });
            go.transform.localScale = Vector3.one;
            if (kind == "moving")
            {
                var path = MakePath(root, "Path_5-3_" + p.id, p.points, -p.thickness / 2);
                Set(go.GetComponent<MovingPlatform>(), so =>
                {
                    so.FindProperty("path").objectReferenceValue = path; so.FindProperty("speed").floatValue = p.speed;
                    so.FindProperty("startDelay").floatValue = p.startDelay; so.FindProperty("endpointWait").floatValue = p.endpointWait;
                    so.FindProperty("loopMode").enumValueIndex = 1; so.FindProperty("offscreenSpeedMultiplier").floatValue = 1;
                });
                go.transform.position = path.GetPoint(0);
            }
            else if (kind == "damage")
            {
                int idx = dmgIndex++;
                Set(go.GetComponent<DamageFoothold>(), so =>
                {
                    so.FindProperty("safeDuration").floatValue = 2.5f; so.FindProperty("activeDuration").floatValue = 1.5f;
                    so.FindProperty("damageDelay").floatValue = .5f; so.FindProperty("damageAmount").intValue = 8; so.FindProperty("damageInterval").floatValue = .5f;
                    so.FindProperty("cycleOffset").floatValue = idx * .3f;
                    so.FindProperty("damageEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ParticleSystem>(Base + "P_DamageFootholdEffect.prefab");
                });
            }
            else if (kind == "blink")
                Set(go.GetComponent<BlinkingFoothold>(), so => { so.FindProperty("visibleDuration").floatValue = 2f; so.FindProperty("invisibleDuration").floatValue = 1f; });
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            hostById[p.id] = (p, go);
        }

        // 4. start / end markers
        var first = d.platforms[0];
        float feet = player.position.y - player.GetComponent<Collider2D>().bounds.min.y;
        start.position = new Vector3(OriginX, first.points[0].y + feet + .03f, 0);
        end.position = new Vector3(OriginX + d.goal[0], d.goal[1] + 1, 0);
        Set(end.GetComponent<BoxCollider2D>(), so => { so.FindProperty("m_Size").vector2Value = new Vector2(.8f, 6); so.FindProperty("m_IsTrigger").boolValue = true; });
        PrefabUtility.RecordPrefabInstancePropertyModifications(start); PrefabUtility.RecordPrefabInstancePropertyModifications(end);

        // 5. traps (arrow switch + launcher at the next platform's left edge, teleport sensor at host centre; same layout as 3-3)
        var startPoint = start.GetComponent<ZoneStartPoint>(); var cameraFollow = Camera.main.GetComponent<PlayerCameraFollow>();
        // 5-3 arrows use the 3-3 values (speed 6, life 0.8, damage 8); the shared Stage5 prefab holds the 5-4 values (14/2/15).
        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "P_Trap_Projectile_Stage5_3.prefab");
        if (!projectile) throw new InvalidOperationException("Create Stage5_3/P_Trap_Projectile_Stage5_3 first (speed 6, damage 8, life 0.8)");
        foreach (var t in d.traps)
        {
            var host = hostById[t.host]; float top = host.p.points[0].y;
            if (t.kind == "ArrowSwitch")
            {
                var target = hostById[t.target]; float tTop = target.p.points[0].y;
                var sw = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Trap + "P_Trap_Switch_Stage5_DemonCastle.prefab"), root);
                sw.name = "Trap_5-3_" + t.host + "_Switch";
                sw.transform.position = new Vector3(OriginX + host.p.points[0].x + host.p.width / 2 - 1.5f, top + .02f, 0); // switch mounted ON the surface (3-1 fix); +0.3 left it floating
                Set(sw.GetComponent<SwitchTrigger>(), so => so.FindProperty("cooldown").floatValue = t.cooldown);
                var launcher = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Trap + "P_Trap_Launcher_Stage5_DemonCastle.prefab"), root);
                launcher.name = "Trap_5-3_" + t.host + "_Launcher";
                launcher.transform.position = new Vector3(OriginX + target.p.points[0].x - target.p.width / 2 - .3f, Mathf.Max(top, tTop) + .8f, 0); // muzzle 0.3 in FRONT of the next platform (3-3 / 3-1 data)
                launcher.transform.localScale = new Vector3(.6f, .6f, 1);
                var pl = launcher.GetComponent<ProjectileLauncher>();
                Set(pl, so =>
                {
                    so.FindProperty("switchTrigger").objectReferenceValue = sw.GetComponent<SwitchTrigger>();
                    so.FindProperty("projectilePrefab").objectReferenceValue = projectile.GetComponent<TrapProjectile>();
                    so.FindProperty("launchDirection").vector2Value = new Vector2(-1, 0); so.FindProperty("windupSeconds").floatValue = t.warning;
                });
                StandardizeArrowTrapPrefabs.ConfigureInstance(pl, launcher.transform.position);
            }
            else
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Trap + "P_Trap_Teleport_Stage5_DemonCastle.prefab"), root);
                go.name = "Trap_5-3_" + t.host + "_TeleportSwitch";
                go.transform.position = new Vector3(OriginX + host.p.points[0].x, top + .02f, 0);
                Set(go.GetComponent<SwitchTrigger>(), so => so.FindProperty("cooldown").floatValue = t.cooldown);
                Set(go.GetComponent<SwitchTeleportTrap>(), so =>
                {
                    so.FindProperty("switchTrigger").objectReferenceValue = go.GetComponent<SwitchTrigger>();
                    so.FindProperty("startPoint").objectReferenceValue = startPoint; so.FindProperty("destinationZone").objectReferenceValue = zone;
                    so.FindProperty("cameraFollow").objectReferenceValue = cameraFollow; so.FindProperty("penaltyDamage").intValue = 0;
                });
            }
        }

        // 6. monsters in distance-gated groups (re-uses RunwayEncounterManager; fury is area-wide in 5-3)
        var drops = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "Stage4_3_Fury/P_Stage43_FuryChaser.prefab");
        var flyer = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "Stage5_2/P_Stage52_FlyingPathPatrolPattern_Default.prefab");
        if (!drops || !flyer) throw new InvalidOperationException("Monster prefabs missing");
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder(Base.TrimEnd('/'), "Stage5_3");
        var chaser = Variant("P_Stage53_FuryChaser", Base + "Stage4_3_Fury/P_Stage43_FuryChaser.prefab", go => { });
        float GateX(Monster m)
        {
            if (m.activation == null || m.activation == "") return m.activationX;
            return hostById[m.activation].p.points[0].x - 8f;
        }
        var gateKeys = d.monsters.Select(m => (float)Math.Round(GateX(m), 1)).Distinct().OrderBy(x => x).ToArray();
        var groupRoots = new Dictionary<float, Transform>();
        foreach (var k in gateKeys) { var g = new GameObject("Encounter_5-3_X" + k.ToString("F0")); g.transform.SetParent(root, false); groupRoots[k] = g.transform; }
        foreach (var m in d.monsters)
        {
            float key = (float)Math.Round(GateX(m), 1);
            bool path = m.pattern == "FlyingPathPatrol";
            var go = (GameObject)PrefabUtility.InstantiatePrefab(path ? flyer : chaser, groupRoots[key]); go.name = "Monster_5-3_" + m.id;
            go.GetComponent<MonsterScaleSetter>().ApplyScale(player);
            go.transform.position = new Vector3(OriginX + m.x, m.y, 0);
            if (path)
            {
                var pattern = go.GetComponent(go.GetComponent<IMonsterPattern>().GetType());
                Set(pattern, so =>
                {
                    var pr = so.FindProperty("playerLayer"); if (pr != null) pr.intValue = 1 << player.gameObject.layer;
                    so.FindProperty("path").objectReferenceValue = MakePath(root, "Path_" + m.id, new[] { new Point { x = m.x - 2, y = m.y }, new Point { x = m.x + 2, y = m.y } }, 0);
                });
            }
            else
            {
                float surface = m.part == 4 ? 6.2f : 11.4f;
                Set(go.GetComponent<MonsterMovementBounds>(), so =>
                {
                    so.FindProperty("areaOrigin").objectReferenceValue = root; so.FindProperty("min").vector2Value = new Vector2(-4, surface + .1f);
                    so.FindProperty("max").vector2Value = new Vector2(604, surface + 2.4f); so.FindProperty("constrainY").boolValue = true;
                });
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }
        foreach (var g in groupRoots.Values) g.gameObject.SetActive(false);
        var manager = root.gameObject.AddComponent<RunwayEncounterManager>();
        Set(manager, so =>
        {
            so.FindProperty("stage").objectReferenceValue = stage; so.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerHealth>();
            so.FindProperty("startPoint").objectReferenceValue = start; so.FindProperty("requiredArea").intValue = 3;
            so.FindProperty("fury").objectReferenceValue = player.GetComponent<PlayerFuryBuff>();
            so.FindProperty("minDistance").floatValue = -4; so.FindProperty("maxDistance").floatValue = 604;
            var groups = so.FindProperty("groups"); groups.arraySize = gateKeys.Length;
            for (int i = 0; i < gateKeys.Length; i++) { var e = groups.GetArrayElementAtIndex(i); e.FindPropertyRelative("distance").floatValue = gateKeys[i]; e.FindPropertyRelative("root").objectReferenceValue = groupRoots[gateKeys[i]].gameObject; }
        });

        // 7. buff HUD
        bool hasHud = false; foreach (var r in scene.GetRootGameObjects()) if (r.GetComponent<ItemBuffHUD>() != null) hasHud = true;
        if (!hasHud)
        {
            var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base + "Stage4_3_Fury/P_ItemBuffHUD.prefab"), scene);
            Set(ui.GetComponent<ItemBuffHUD>(), so => { so.FindProperty("speed").objectReferenceValue = player.GetComponent<PlayerSpeedBuff>(); so.FindProperty("fury").objectReferenceValue = player.GetComponent<PlayerFuryBuff>(); });
        }

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Stage5-3 deployed: " + d.platforms.Length + " platforms, " + d.traps.Length + " traps, " + d.monsters.Length + " monsters in " + gateKeys.Length + " gates; 5-4 shifted +" + delta);
    }
}
