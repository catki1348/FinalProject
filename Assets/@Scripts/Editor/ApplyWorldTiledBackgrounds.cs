using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Claude Sonnet 5.5, 2026-10-16. Deploys world-fixed tiled backgrounds (WorldTiledBackground) to Stage1~5.
// Replaces the camera-following CameraZoneBackgroundManager, which made moving platforms feel stationary.
public static class ApplyWorldTiledBackgrounds
{
    public static string PrefabPath(int stage)
    {
        return "Assets/@ExternalAssets/@Prefab/P_Stage" + stage + "_WorldBackgrounds.prefab";
    }

    public static string ImagePath(int stage, int area)
    {
        return "Assets/@ExternalAssets/WorldBackgrounds/Stage" + stage + "/T_Stage" + stage + "_Area" + area + "_WorldTile.png";
    }

    // Import the mirrored atlases as Full Rect sprites (required for Tiled draw mode) and build the stage prefab.
    public static void PreparePrefab(int stage)
    {
        var sprites = new Sprite[5];
        for (int i = 0; i < 5; i++)
        {
            string path = ImagePath(stage, i + 1);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing image: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprites[i] == null) throw new InvalidOperationException("Sprite import failed: " + path);
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        if (material == null) throw new InvalidOperationException("URP 2D background material is missing.");
        GameObject root = new GameObject("P_Stage" + stage + "_WorldBackgrounds");
        try
        {
            for (int i = 0; i < 5; i++)
            {
                var child = new GameObject("WorldBackground_" + stage + "-" + (i + 1));
                child.transform.SetParent(root.transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[i];
                renderer.sharedMaterial = material;
                renderer.sortingOrder = -100 + i;
                child.AddComponent<WorldTiledBackground>();
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(stage));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // Bind the five prefab children to the stage's ordered zones and retire every older background system.
    public static int Apply(Scene scene, int stageNumber)
    {
        StageManager stage = null;
        var legacy = new List<Behaviour>();
        var managers = new List<CameraZoneBackgroundManager>();
        WorldTiledBackground[] existing = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var item in root.GetComponentsInChildren<StageManager>(true)) stage = item;
            managers.AddRange(root.GetComponentsInChildren<CameraZoneBackgroundManager>(true));
            legacy.AddRange(root.GetComponentsInChildren<ContinuousStageBackground>(true));
            legacy.AddRange(root.GetComponentsInChildren<ZoneBackground>(true));
            legacy.AddRange(root.GetComponentsInChildren<BackgroundFitter>(true));
            var found = root.GetComponentsInChildren<WorldTiledBackground>(true);
            if (found.Length > 0) existing = found;
        }
        if (stage == null) throw new InvalidOperationException("Missing StageManager.");
        var ordered = new SerializedObject(stage).FindProperty("zoneCameraBounds");
        if (ordered.arraySize != 5) throw new InvalidOperationException("Exactly five ordered camera zones are required.");
        var zones = new CameraBoundsZone[5];
        for (int i = 0; i < 5; i++)
        {
            zones[i] = ordered.GetArrayElementAtIndex(i).objectReferenceValue as CameraBoundsZone;
            if (zones[i] == null) throw new InvalidOperationException("Missing camera zone " + i);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(stageNumber));
        if (prefab == null) throw new InvalidOperationException("Prepare the background prefab first.");

        foreach (var item in legacy)
        {
            item.enabled = false;
            var renderer = item.GetComponent<SpriteRenderer>();
            if (renderer != null) { renderer.enabled = false; Record(renderer); }
            Record(item);
        }
        foreach (var zone in zones)
        {
            var renderer = zone.GetComponent<SpriteRenderer>();
            if (renderer != null) { renderer.enabled = false; Record(renderer); }
        }
        // Deactivating the whole camera-following object also hides its five renderers.
        foreach (var manager in managers)
        {
            manager.enabled = false;
            manager.gameObject.SetActive(false);
            Record(manager); Record(manager.gameObject);
        }

        WorldTiledBackground[] tiles = existing;
        if (tiles == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            tiles = instance.GetComponentsInChildren<WorldTiledBackground>(true);
        }
        if (tiles.Length != 5) throw new InvalidOperationException("Expected five world backgrounds.");
        // Player height drives the background scale; read it from the player's main visual sprite.
        float playerHeight = 2f;
        var playerStats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
        if (playerStats != null)
        {
            var visual = playerStats.transform.Find("Visual");
            var visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (visualRenderer != null) playerHeight = visualRenderer.bounds.size.y;
        }
        for (int i = 0; i < 5; i++)
        {
            // Re-apply tuning on existing instances too, since serialized scene values would otherwise keep old defaults.
            var tuning = new SerializedObject(tiles[i]);
            tuning.FindProperty("padding").floatValue = 0.5f;
            tuning.FindProperty("imageHeightInPlayerHeights").floatValue = 5f;
            tuning.ApplyModifiedPropertiesWithoutUndo();
            tiles[i].SetPlayerHeight(playerHeight);
            tiles[i].Configure(zones[i]);
            Record(tiles[i]); Record(tiles[i].transform);
            Record(tiles[i].GetComponent<SpriteRenderer>());
        }
        EditorSceneManager.MarkSceneDirty(scene);
        return tiles.Length;
    }

    private static void Record(UnityEngine.Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
