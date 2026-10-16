using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Completes the encounters in the existing 4-1 design using reusable prefab variants.
// Worker record: Codex (GPT-6), 2026-10-15.
public static class DeployStage41Content
{
    const string AssetsRoot = "Assets/@ExternalAssets/@Prefab/";
    const string Folder = AssetsRoot + "Stage4_1/";
    const string Theme = AssetsRoot + "Trap/Themed/Stage4_DemonCave/";
    [Serializable] class Point { public float x, y; }
    [Serializable] class Ring { public Point bottom; }
    [Serializable] class Zone { public float x, y, width, height; }
    [Serializable] class Trap
    {
        public string id, kind, mover, destinationPlatform;
        public Point switchPos, launcher, destination;
        public Zone zone;
        public float tSwitch, windupSeconds, cooldown, projectileSpeed, projectileLifetime, projectileDamage;
        public float[] direction;
    }
    [Serializable] class Monster
    {
        public string id, @class, pattern, platform;
        public Point spawn;
        public Point[] path;
        public float[] areaX, areaSize, detectSize, footprint;
        public float orbitRadius;
    }
    [Serializable] class Design { public Ring ring; public Trap[] traps; public Monster[] monsters; }

    // Placement-dependent references are assigned here; prefab originals own reusable behavior/art.
    public static void Apply(Transform section, Transform root)
    {
        var d = JsonUtility.FromJson<Design>(File.ReadAllText("@Edit/MapDesign_Stage4-1.json"));
        Vector2 offset = new Vector2(1 - d.ring.bottom.x, -d.ring.bottom.y);
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder(AssetsRoot.TrimEnd('/'), "Stage4_1");
        var zone = section.GetComponent<CameraBoundsZone>();
        var player = section.GetComponentInChildren<PlayerMovement>(true);
        PlayerCameraFollow follow = null;
        foreach (var sceneRoot in section.gameObject.scene.GetRootGameObjects())
        {
            var found = sceneRoot.GetComponentInChildren<PlayerCameraFollow>(true);
            if (found != null) follow = found;
        }
        if (follow == null || player == null) throw new InvalidOperationException("4-1 player/camera references missing");
        var platforms = new Dictionary<string, Transform>();
        foreach (Transform t in root)
            if (t.GetComponent<BoxCollider2D>() != null)
                foreach (string name in new[] { "M1", "M2", "M4", "M5", "V2", "V4" })
                    if (t.name.Contains("_" + name + "_")) platforms[name] = t;

        var projectile = EnsureVariant("P_Stage41_Projectile", Theme + "P_Trap_Projectile_Stage4_DemonCave.prefab", go =>
        {
            var so = new SerializedObject(go.GetComponent<TrapProjectile>());
            so.FindProperty("speed").floatValue = 14;
            so.FindProperty("lifeTime").floatValue = 2;
            so.FindProperty("damage").intValue = 15;
            so.ApplyModifiedPropertiesWithoutUndo();
        });
        var mounted = EnsureVariant("P_Stage41_MountedSwitch", Theme + "P_Trap_Switch_Stage4_DemonCave.prefab", go =>
        {
            if (!go.GetComponent<MovingPlatformSwitchGate>()) go.AddComponent<MovingPlatformSwitchGate>();
        });
        foreach (var t in d.traps)
        {
            if (t.kind == "SwitchProjectile")
            {
                bool mountedSwitch = t.mover == "M1" || t.mover == "M2";
                var sw = Spawn(mountedSwitch ? mounted : AssetDatabase.LoadAssetAtPath<GameObject>(Theme + "P_Trap_Switch_Stage4_DemonCave.prefab"), root, "Trap_4-1_" + t.id + "_Switch");
                sw.transform.position = offset + new Vector2(t.switchPos.x, t.switchPos.y);
                if (mountedSwitch)
                {
                    var platform = platforms[t.mover];
                    sw.transform.SetParent(platform, true);
                    sw.transform.localPosition = new Vector3(0, .95f, 0);
                    var so = new SerializedObject(sw.GetComponent<MovingPlatformSwitchGate>());
                    var path = new SerializedObject(platform.GetComponent<MovingPlatform>()).FindProperty("path").objectReferenceValue;
                    so.FindProperty("platform").objectReferenceValue = platform.GetComponent<MovingPlatform>();
                    so.FindProperty("path").objectReferenceValue = path;
                    so.FindProperty("switchCollider").objectReferenceValue = sw.GetComponent<Collider2D>();
                    so.FindProperty("activationFraction").floatValue = t.tSwitch;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                var swSo = new SerializedObject(sw.GetComponent<SwitchTrigger>());
                swSo.FindProperty("cooldown").floatValue = t.cooldown;
                swSo.ApplyModifiedPropertiesWithoutUndo();
                var launcher = Spawn(AssetDatabase.LoadAssetAtPath<GameObject>(Theme + "P_Trap_Launcher_Stage4_DemonCave.prefab"), root, "Trap_4-1_" + t.id + "_Launcher");
                Vector3 muzzle = offset + new Vector2(t.launcher.x, t.launcher.y);
                launcher.transform.position = muzzle;
                var pl = launcher.GetComponent<ProjectileLauncher>();
                var settings = new SerializedObject(pl);
                settings.FindProperty("switchTrigger").objectReferenceValue = sw.GetComponent<SwitchTrigger>();
                settings.FindProperty("projectilePrefab").objectReferenceValue = projectile.GetComponent<TrapProjectile>();
                settings.FindProperty("launchDirection").vector2Value = new Vector2(t.direction[0], t.direction[1]);
                settings.FindProperty("windupSeconds").floatValue = t.windupSeconds;
                settings.ApplyModifiedPropertiesWithoutUndo();
                StandardizeArrowTrapPrefabs.ConfigureInstance(pl, muzzle);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sw.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(launcher.transform);
            }
            else
            {
                // Large falling-player detection zone uses the existing self-activating teleport prefab.
                var tp = Spawn(AssetDatabase.LoadAssetAtPath<GameObject>(Theme + "P_Trap_Teleport_Stage4_DemonCave.prefab"), root, "Trap_4-1_" + t.id + "_Teleport");
                tp.transform.position = offset + new Vector2(t.zone.x, t.zone.y);
                var box = tp.GetComponent<BoxCollider2D>();
                box.size = new Vector2(t.zone.width, t.zone.height); box.isTrigger = true;
                var dest = new GameObject("Destination_4-1_" + t.id).transform;
                dest.SetParent(root, false);
                var surface = platforms[t.destinationPlatform].GetComponent<BoxCollider2D>().bounds.max.y;
                float halfHeight = player.GetComponent<Collider2D>().bounds.extents.y;
                dest.position = new Vector3(offset.x + t.destination.x, surface + halfHeight + .05f, 0);
                var so = new SerializedObject(tp.GetComponent<SwitchTeleportTrap>());
                so.FindProperty("switchTrigger").objectReferenceValue = tp.GetComponent<SwitchTrigger>();
                so.FindProperty("customDestination").objectReferenceValue = dest;
                so.FindProperty("destinationZone").objectReferenceValue = zone;
                so.FindProperty("cameraFollow").objectReferenceValue = follow;
                so.FindProperty("penaltyDamage").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(tp.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(box);
            }
        }
        foreach (var m in d.monsters) CreateMonster(m, offset, root, player.transform);
        CreateBackground(root, zone);
    }

    // The former camera-sized background covered only the old start, leaving the tall ring empty.
    // A separate full-rectangle sprite keeps the original image importer unchanged for other sections.
    static void CreateBackground(Transform root, CameraBoundsZone zone)
    {
        string spritePath = Folder + "T_Stage41_Background.asset";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Backgrounds/Stage4_Background.png");
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            AssetDatabase.CreateAsset(sprite, spritePath);
        }
        string prefabPath = Folder + "P_Stage41_Background.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            var source = new GameObject("P_Stage41_Background");
            try
            {
                var sr = source.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sortingOrder = -100;
                source.AddComponent<ZoneBackground>();
                prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        var go = Spawn(prefab, root, "Background_4-1");
        go.transform.position = new Vector3(zone.WorldBounds.center.x, zone.WorldBounds.center.y, 10);
        var so = new SerializedObject(go.GetComponent<ZoneBackground>());
        so.FindProperty("zone").objectReferenceValue = zone;
        so.ApplyModifiedPropertiesWithoutUndo();
        // Fit synchronously before saving instead of waiting for an editor Update callback.
        typeof(ZoneBackground).GetMethod("Fit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(go.GetComponent<ZoneBackground>(), null);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<SpriteRenderer>());
    }

    // Make one variant per pattern so future placements inherit the same reusable setup.
    static void CreateMonster(Monster m, Vector2 offset, Transform root, Transform player)
    {
        bool flying = m.@class == "Flying";
        Type patternType = typeof(MonsterController).Assembly.GetType(m.pattern);
        if (patternType == null) throw new InvalidOperationException("Unknown pattern " + m.pattern);
        var prefab = EnsureVariant("P_Stage41_" + m.pattern, AssetsRoot + (flying ? "Monster_ForestBat.prefab" : "Monster_ForestGoblin.prefab"), go =>
        {
            foreach (var component in go.GetComponents<MonoBehaviour>())
                if (component is IMonsterPattern) UnityEngine.Object.DestroyImmediate(component);
            go.AddComponent(patternType);
            var locomotion = go.GetComponent<MonsterLocomotion>() ?? go.AddComponent<MonsterLocomotion>();
            var so = new SerializedObject(locomotion);
            so.FindProperty("type").enumValueIndex = flying ? 1 : 0;
            so.FindProperty("groundGravityScale").floatValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();
            var stats = new SerializedObject(go.GetComponent<MonsterStats>());
            stats.FindProperty("attack").intValue = 10; stats.FindProperty("defense").intValue = 10;
            stats.ApplyModifiedPropertiesWithoutUndo();
            if (!go.GetComponent<MonsterScaleSetter>()) go.AddComponent<MonsterScaleSetter>();
            if (!go.GetComponent<MonsterMovementBounds>()) go.AddComponent<MonsterMovementBounds>();
            var rb = go.GetComponent<Rigidbody2D>(); rb.gravityScale = flying ? 0 : 3;
            // Hitbox follows the visible artwork instead of retaining the old one-unit placeholder.
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            var vertices = sr.sprite.vertices;
            Vector2 min = vertices[0], max = vertices[0];
            foreach (var v in vertices) { min = Vector2.Min(min, v); max = Vector2.Max(max, v); }
            var box = go.GetComponent<BoxCollider2D>(); box.size = (max - min) * .8f; box.offset = (max + min) * .5f;
            if (!flying && m.pattern == "ChasePattern")
            {
                var check = new GameObject("GroundCheck").transform; check.SetParent(go.transform, false);
                check.localPosition = new Vector3(box.offset.x, box.offset.y - box.size.y * .5f, 0);
                var chase = new SerializedObject(go.GetComponent(patternType));
                chase.FindProperty("groundCheck").objectReferenceValue = check;
                chase.FindProperty("groundLayer").intValue = LayerMask.GetMask("Ground");
                chase.ApplyModifiedPropertiesWithoutUndo();
            }
        });
        var instance = Spawn(prefab, root, "Monster_4-1_" + m.id + "_" + m.pattern);
        instance.transform.position = offset + new Vector2(m.spawn.x, m.spawn.y);
        instance.GetComponent<MonsterScaleSetter>().ApplyScale(player);
        var col = instance.GetComponent<Collider2D>();
        if (!flying)
        {
            // Design spawn Y is a nominal center; resolve actual feet against the supporting surface.
            float surface = offset.y + m.footprint[1];
            instance.transform.position += Vector3.up * (surface + .02f - col.bounds.min.y);
        }
        var pattern = new SerializedObject(instance.GetComponent(patternType));
        var area = pattern.FindProperty("areaSize");
        if (area != null)
        {
            // Patterns steer their center, while the leash reserves room for the collider.
            // Keep the pattern's turn/target points inside the leash so patrols cannot stall at its edge.
            Vector2 extent = col.bounds.size;
            area.vector2Value = flying
                ? new Vector2(m.areaSize[0] - extent.x - .05f, m.areaSize[1] - extent.y - .05f)
                : new Vector2(m.areaX[1] - m.areaX[0] - extent.x - .05f, 2);
        }
        var detection = pattern.FindProperty("detectionSize");
        if (detection != null) detection.vector2Value = flying ? new Vector2(m.detectSize[0], m.detectSize[1]) : new Vector2(m.areaX[1] - m.areaX[0], 4);
        var mask = pattern.FindProperty("playerLayer"); if (mask != null) mask.intValue = 1 << player.gameObject.layer;
        var radius = pattern.FindProperty("circleRadius"); if (radius != null) radius.floatValue = m.orbitRadius;
        if (m.path != null && m.path.Length > 0)
        {
            var pathGo = new GameObject("MonsterPath_4-1_" + m.id); pathGo.transform.SetParent(root, false);
            var path = pathGo.AddComponent<PlatformPath>();
            foreach (var point in m.path)
            {
                var t = new GameObject("Point").transform; t.SetParent(pathGo.transform, false);
                t.position = offset + new Vector2(point.x, point.y);
            }
            pattern.FindProperty("path").objectReferenceValue = path;
        }
        pattern.ApplyModifiedPropertiesWithoutUndo();
        var bounds = new SerializedObject(instance.GetComponent<MonsterMovementBounds>());
        bounds.FindProperty("areaOrigin").objectReferenceValue = root;
        bounds.FindProperty("min").vector2Value = offset + new Vector2(m.footprint[0], m.footprint[1]);
        bounds.FindProperty("max").vector2Value = offset + new Vector2(m.footprint[2], m.footprint[3]);
        bounds.FindProperty("constrainY").boolValue = flying;
        bounds.ApplyModifiedPropertiesWithoutUndo();
        // OnValidate on the old controller may recalculate scale while assigning serialized values.
        instance.GetComponent<MonsterScaleSetter>().ApplyScale(player);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
    }

    static GameObject Spawn(GameObject prefab, Transform parent, string name)
    {
        if (prefab == null) throw new InvalidOperationException("Missing prefab for " + name);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = name;
        return instance;
    }

    static GameObject EnsureVariant(string name, string source, Action<GameObject> configure)
    {
        string path = Folder + name + ".prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset != null) return asset;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try { configure(instance); return PrefabUtility.SaveAsPrefabAsset(instance, path); }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }
}
