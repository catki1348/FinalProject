using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Codex (GPT-6), 2026-10-15: deploys the five-segment 5-1 design without rebuilding 5-2.
// Claude Sonnet 5.5, 2026-10-16: v2 design (2-1 shared-clock damage/blink, 3-1 v4 projectile traps) - Blinking platforms, externally controlled damage, arrow traps, PlatformCycleManager.
public static class DeployStage51
{
    const string ScenePath = "Assets/Scenes/Stage5.unity";
    const string Folder = "Assets/@ExternalAssets/@Prefab/Stage5_1/";
    const string BasePrefab = "Assets/@ExternalAssets/@Prefab/Platform_Stage5_Hellscape.prefab";
    const float OriginX = 1;
    [Serializable] public class Point { public float x, y; }
    [Serializable] public class RectData { public float minX, minY, maxX, maxY; }
    [Serializable] public class Settings
    {
        public float speed, startDelay, waitAtPoint;
        public float damageDelay, damageAmount, damageInterval, safeDuration, activeDuration, cycleOffset;
        public float vanishDelay, respawnDelay;
        public string clockGroup; public bool externallyControlled; public float warningSeconds;
    }
    [Serializable] public class Platform
    {
        public string id, name, type;
        public int segment;
        public float x, surfaceY, width, thickness;
        public Point[] path;
        public Settings settings;
    }
    [Serializable] public class Trap
    {
        public string id, kind, host, target;
        public Point @switch, muzzle;
        public float[] direction;
        public float warningSeconds, cooldown, projectileSpeed, lifetime, damage;
    }
    [Serializable] public class Design { public Platform[] platforms; public Trap[] traps; public Point start, goal; public RectData bounds; }

    // Every direct prefab-instance assignment is recorded before serialized changes can resynchronize it.
    static void Set(UnityEngine.Object target, Action<SerializedObject> configure)
    {
        var so = new SerializedObject(target); configure(so); so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    // Stage5 platforms use the whole hellscape block (spikes included) like the lava/dungeon stages use theirs:
    // body = collision (0.5 thick at the transform centre), spikes rise above it as appearance only.
    static Sprite Tile()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/@ExternalAssets/Tiles/Platform_Stage5_Hellscape_Tiled.png");
        if (!sprite) throw new InvalidOperationException("Missing Stage5 block tile: Assets/@ExternalAssets/Tiles/Platform_Stage5_Hellscape_Tiled.png");
        return sprite;
    }
    const float TileHeight = 0.785f;   // full sprite height at 930 ppu (body 0.5 + spikes 0.285)

    // Each behavior is owned by a variant; placement-specific sizes and paths are instance overrides.
    static GameObject PlatformPrefab(string kind, Sprite tile)
    {
        string path = Folder + "P_Stage51_" + kind + ".prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        var go = exists ? PrefabUtility.LoadPrefabContents(path) : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefab));
        try
        {
            foreach (var b in go.GetComponents<PlatformBehaviour>()) UnityEngine.Object.DestroyImmediate(b);
            if (!go.GetComponent<PlatformCore>()) go.AddComponent<PlatformCore>();
            if (kind == "Moving") go.AddComponent<MovingPlatform>();
            if (kind == "Damage" || kind == "DamageClock") go.AddComponent<DamageFoothold>();
            if (kind == "Blinking") go.AddComponent<BlinkingFoothold>();
            if (kind == "Disappearing") go.AddComponent<DisappearingFoothold>();
            go.layer = LayerMask.NameToLayer("Ground");
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = tile; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(3, TileHeight);
            var box = go.GetComponent<BoxCollider2D>(); box.autoTiling = false; box.size = new Vector2(3, .5f); box.offset = Vector2.zero;
            go.transform.localScale = Vector3.one;
            if (go.TryGetComponent<Rigidbody2D>(out var body)) { body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation; }
            // the shared 2-1 clock owns the timing of these two variants
            if (kind == "DamageClock") Set(go.GetComponent<DamageFoothold>(), so => so.FindProperty("externallyControlled").boolValue = true);
            if (kind == "Blinking") Set(go.GetComponent<BlinkingFoothold>(), so => so.FindProperty("externallyControlled").boolValue = true);
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(go); else UnityEngine.Object.DestroyImmediate(go); }
    }

    [MenuItem("Tools/Deploy/Stage 5-1 Five Segments")]
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
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("5-1 v2 saved: five segments, 57 platforms (33 normal, 12 moving, 9 damage, 3 blinking), 5 projectile traps, shared 2-1 clock");
    }

    // Also accepts an isolated TestNNN copy for validation; main scene references are preserved.
    public static void Build(Scene scene)
    {
        var d = JsonUtility.FromJson<Design>(File.ReadAllText("@Edit/MapDesign_Stage5-1.json"));
        if (d.platforms.Length != 57 || d.traps == null || d.traps.Length != 5) throw new InvalidOperationException("Unexpected 5-1 v2 design counts (expected 57 platforms, 5 traps)");
        var map = new Dictionary<string, Transform>();
        foreach (var r in scene.GetRootGameObjects()) foreach (var t in r.GetComponentsInChildren<Transform>(true))
            if (!map.ContainsKey(t.name)) map.Add(t.name, t);
        var section = map["Section_1"];
        var start = map["ZoneStartPoint_5-1"]; var end = map["ZoneEndPoint_5-1"];
        var stage = map["StageManager"].GetComponent<StageManager>();
        var player = (Transform)new SerializedObject(stage).FindProperty("player").objectReferenceValue;
        float left = OriginX + d.bounds.minX, right = map["Boundary_1_2"].position.x;
        if (OriginX + d.bounds.maxX >= right) throw new InvalidOperationException("5-1 overlaps its right boundary; arrange later sections first");
        var zone = section.GetComponent<CameraBoundsZone>();
        if (!zone) throw new InvalidOperationException("Missing 5-1 camera zone");
        var remove = new List<GameObject>();
        foreach (Transform t in section) if (t != start && t != end && t != player) remove.Add(t.gameObject);
        foreach (var go in remove) UnityEngine.Object.DestroyImmediate(go);
        Vector3 oldPlayer = player.position;
        section.position = new Vector3((left + right) * .5f, (d.bounds.minY + d.bounds.maxY) * .5f, 0);
        player.position = oldPlayer;
        Set(zone, so =>
        {
            so.FindProperty("size").vector2Value = new Vector2(right - left, d.bounds.maxY - d.bounds.minY);
            so.FindProperty("orthographicSize").floatValue = 5;
            so.FindProperty("leftBoundary").objectReferenceValue = null;
            so.FindProperty("rightBoundary").objectReferenceValue = map["Boundary_1_2"];
        });
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/@ExternalAssets/@Prefab", "Stage5_1");
        var root = new GameObject("Generated_5-1").transform;
        root.SetParent(section, false); root.position = new Vector3(OriginX, 0, 0);
        var groups = new Transform[5];
        for (int i = 0; i < groups.Length; i++) { groups[i] = new GameObject("Segment_" + (i + 1).ToString("D2")).transform; groups[i].SetParent(root, false); }
        var prefabs = new Dictionary<string, GameObject>();
        var made = new Dictionary<string, GameObject>(); var data = new Dictionary<string, Platform>();
        var tile = Tile();
        foreach (var p in d.platforms)
        {
            string kind = p.type == "Damage" && p.settings != null && p.settings.externallyControlled ? "DamageClock" : p.type;
            if (!prefabs.ContainsKey(kind)) prefabs[kind] = PlatformPrefab(kind, tile);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind], groups[p.segment - 1]);
            go.name = "Platform_5-1_" + p.id + "_" + p.name + "_" + p.type;
            go.transform.localPosition = new Vector3(p.x, p.surfaceY - p.thickness * .5f, 0);
            go.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            Set(go.GetComponent<SpriteRenderer>(), so => so.FindProperty("m_Size").vector2Value = new Vector2(p.width, TileHeight));
            Set(go.GetComponent<BoxCollider2D>(), so => { so.FindProperty("m_Size").vector2Value = new Vector2(p.width, p.thickness); so.FindProperty("m_Offset").vector2Value = Vector2.zero; });
            if (p.type == "Moving")
            {
                if (p.path == null || p.path.Length != 2) throw new InvalidOperationException("Missing path " + p.id);
                var pathRoot = new GameObject("Path_5-1_" + p.id).transform; pathRoot.SetParent(groups[p.segment - 1], false);
                var path = pathRoot.gameObject.AddComponent<PlatformPath>();
                foreach (var point in p.path) { var t = new GameObject("Point").transform; t.SetParent(pathRoot, false); t.localPosition = new Vector3(point.x, point.y, 0); }
                if (Vector2.Distance(path.GetPoint(0), path.GetPoint(1)) > 56) throw new InvalidOperationException("Oversized mover " + p.id);
                Set(go.GetComponent<MovingPlatform>(), so =>
                {
                    so.FindProperty("path").objectReferenceValue = path;
                    so.FindProperty("speed").floatValue = p.settings.speed;
                    so.FindProperty("startDelay").floatValue = p.settings.startDelay;
                    so.FindProperty("endpointWait").floatValue = p.settings.waitAtPoint;
                    so.FindProperty("loopMode").enumValueIndex = 1;
                    so.FindProperty("offscreenSpeedMultiplier").floatValue = 1;
                });
                go.transform.position = path.GetPoint(0);
            }
            else if (p.type == "Damage") Set(go.GetComponent<DamageFoothold>(), so =>
            {
                so.FindProperty("damageDelay").floatValue = p.settings.damageDelay;
                so.FindProperty("damageAmount").intValue = Mathf.RoundToInt(p.settings.damageAmount);
                so.FindProperty("damageInterval").floatValue = p.settings.damageInterval;
                if (!p.settings.externallyControlled)
                {
                so.FindProperty("safeDuration").floatValue = p.settings.safeDuration;
                so.FindProperty("activeDuration").floatValue = p.settings.activeDuration;
                so.FindProperty("cycleOffset").floatValue = p.settings.cycleOffset;
                }
                so.FindProperty("damageEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/@ExternalAssets/@Prefab/P_DamageFootholdEffect.prefab");
            });
            else if (p.type == "Disappearing") Set(go.GetComponent<DisappearingFoothold>(), so =>
            {
                so.FindProperty("vanishDelay").floatValue = p.settings.vanishDelay;
                so.FindProperty("respawnDelay").floatValue = p.settings.respawnDelay;
            });
            go.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            made[p.name] = go; data[p.name] = p;
        }
        float feetOffset = player.position.y - player.GetComponent<Collider2D>().bounds.min.y;
        // Start just inside the long ground lane so the whole body is supported.
        float support = player.GetComponent<Collider2D>().bounds.extents.x + .05f;
        start.position = new Vector3(OriginX + d.start.x + support, d.start.y + feetOffset + .03f, 0);
        player.position = start.position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        end.position = new Vector3(OriginX + d.goal.x, d.goal.y + .7f, 0);
        Set(end.GetComponent<BoxCollider2D>(), so => { so.FindProperty("m_Size").vector2Value = new Vector2(.8f, 1.4f); so.FindProperty("m_IsTrigger").boolValue = true; });
        PrefabUtility.RecordPrefabInstancePropertyModifications(start); PrefabUtility.RecordPrefabInstancePropertyModifications(end);
        BuildTraps(d, root, made, data);
        BuildClock(d, root, made, data, stage, player, start);
        var fall = map["OutOfBoundsTrigger"];
        fall.position = new Vector3((left + right) * .5f, d.bounds.minY - 3, 0);
        Set(fall.GetComponent<BoxCollider2D>(), so => { so.FindProperty("m_Size").vector2Value = new Vector2(right - left, 2); so.FindProperty("m_IsTrigger").boolValue = true; });
        PrefabUtility.RecordPrefabInstancePropertyModifications(fall);
        Background(root, zone);
    }

    const string TrapFolder = "Assets/@ExternalAssets/@Prefab/Trap/Themed/Stage5_DemonCastle/";

    // 3-1 v4 projectile traps: the switch sits ON the host surface (sprite bottom = surface), the muzzle is where the design says.
    static void BuildTraps(Design d, Transform root, Dictionary<string, GameObject> made, Dictionary<string, Platform> data)
    {
        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@ExternalAssets/@Prefab/Stage5_3/P_Trap_Projectile_Stage5_3.prefab");
        if (!projectile) throw new InvalidOperationException("Stage5_3/P_Trap_Projectile_Stage5_3 (speed 6, damage 8, life 0.8) is required");
        foreach (var t in d.traps)
        {
            var sw = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TrapFolder + "P_Trap_Switch_Stage5_DemonCastle.prefab"), root);
            sw.name = "Trap_5-1_" + t.id + "_Switch";
            sw.transform.position = new Vector3(OriginX + t.@switch.x, t.@switch.y, 0);
            Set(sw.GetComponent<SwitchTrigger>(), so => so.FindProperty("cooldown").floatValue = t.cooldown);
            var launcher = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TrapFolder + "P_Trap_Launcher_Stage5_DemonCastle.prefab"), root);
            launcher.name = "Trap_5-1_" + t.id + "_Launcher";
            launcher.transform.position = new Vector3(OriginX + t.muzzle.x, t.muzzle.y, 0);
            launcher.transform.localScale = new Vector3(.6f, .6f, 1);
            var pl = launcher.GetComponent<ProjectileLauncher>();
            Set(pl, so =>
            {
                so.FindProperty("switchTrigger").objectReferenceValue = sw.GetComponent<SwitchTrigger>();
                so.FindProperty("projectilePrefab").objectReferenceValue = projectile.GetComponent<TrapProjectile>();
                so.FindProperty("launchDirection").vector2Value = new Vector2(t.direction[0], t.direction[1]);
                so.FindProperty("windupSeconds").floatValue = t.warningSeconds;
            });
            StandardizeArrowTrapPrefabs.ConfigureInstance(pl, launcher.transform.position);
        }
    }

    // Shared 2-1 clock: clock 0 covers segment 2, clock 1 the fusion's blink/damage pair. Distances are along x from the start point.
    static void BuildClock(Design d, Transform root, Dictionary<string, GameObject> made, Dictionary<string, Platform> data, StageManager stage, Transform player, Transform start)
    {
        Func<Platform, float> left = p => OriginX + p.x - p.width / 2;
        Func<Platform, float> right = p => OriginX + p.x + p.width / 2;
        var seg2 = new List<Platform>(); foreach (var p in d.platforms) if (p.segment == 2) seg2.Add(p);
        float e0 = float.MaxValue, x0 = float.MinValue; foreach (var p in seg2) { e0 = Mathf.Min(e0, left(p)); x0 = Mathf.Max(x0, right(p)); }
        var a0 = data["F-A0"]; var rest = data["F-Rest"];
        var spec = new[] { (e0, x0, 2), (left(a0), right(rest), 5) };
        var manager = root.gameObject.AddComponent<PlatformCycleManager>();
        Set(manager, so =>
        {
            so.FindProperty("stage").objectReferenceValue = stage; so.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerHealth>();
            so.FindProperty("startPoint").objectReferenceValue = start; so.FindProperty("requiredArea").intValue = 1;
            var clocks = so.FindProperty("clocks"); clocks.arraySize = spec.Length;
            for (int i = 0; i < spec.Length; i++)
            {
                var c = clocks.GetArrayElementAtIndex(i); var dm = new List<DamageFoothold>(); var bl = new List<BlinkingFoothold>();
                foreach (var p in d.platforms)
                {
                    if (p.settings == null || !p.settings.externallyControlled) continue;
                    bool inClock = i == 0 ? p.segment == 2 : (p.segment == 5 && (p.name == "F-Blink" || p.name == "F-Damage"));
                    if (!inClock) continue;
                    if (p.type == "Damage") dm.Add(made[p.name].GetComponent<DamageFoothold>());
                    else if (p.type == "Blinking") bl.Add(made[p.name].GetComponent<BlinkingFoothold>());
                }
                c.FindPropertyRelative("enterDistance").floatValue = spec[i].Item1 - start.position.x;
                c.FindPropertyRelative("exitDistance").floatValue = spec[i].Item2 - start.position.x;
                var dArr = c.FindPropertyRelative("damages"); dArr.arraySize = dm.Count; for (int k = 0; k < dm.Count; k++) dArr.GetArrayElementAtIndex(k).objectReferenceValue = dm[k];
                var bArr = c.FindPropertyRelative("blinks"); bArr.arraySize = bl.Count; for (int k = 0; k < bl.Count; k++) bArr.GetArrayElementAtIndex(k).objectReferenceValue = bl[k];
            }
        });
    }

    // Full-rectangle sprite is dedicated to this area; original image import settings are retained.
    static void Background(Transform root, CameraBoundsZone zone)
    {
        string spritePath = Folder + "T_Stage51_Background.asset";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (!sprite)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Backgrounds/Stage5_Background.png");
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            AssetDatabase.CreateAsset(sprite, spritePath);
        }
        string path = Folder + "P_Stage51_Background.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!prefab)
        {
            var source = new GameObject("P_Stage51_Background");
            try { var sr = source.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingOrder = -9; source.AddComponent<ZoneBackground>(); prefab = PrefabUtility.SaveAsPrefabAsset(source, path); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root); go.name = "Background_5-1";
        go.transform.position = new Vector3(zone.WorldBounds.center.x, zone.WorldBounds.center.y, 10);
        Set(go.GetComponent<ZoneBackground>(), so => so.FindProperty("zone").objectReferenceValue = zone);
        typeof(ZoneBackground).GetMethod("Fit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(go.GetComponent<ZoneBackground>(), null);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<SpriteRenderer>());
    }
}
