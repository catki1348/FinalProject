using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Codex (GPT-6), 2026-10-16. Shared deployment for one camera-sized image per stage area.
public static class ApplyStageAreaBackgrounds
{
    public static string PrefabPath(int stage)
    {
        CheckStage(stage);
        return "Assets/@ExternalAssets/@Prefab/P_Stage" + stage + "_AreaBackgrounds.prefab";
    }

    public static string TestScenePath(int stage)
    {
        CheckStage(stage);
        return stage == 1 ? "Assets/Scenes/Test062.unity" : "Assets/Scenes/Test" + (68 + stage).ToString("000") + ".unity";
    }

    // Keep the approved original files and use separate sprite importers for each generated image.
    public static string ImagePath(int stage, int area)
    {
        CheckStage(stage);
        if (area < 1 || area > 5) throw new ArgumentOutOfRangeException("area");
        if (stage == 1) return "Assets/@ExternalAssets/ForestBackground/T_Stage1_Area" + area + "_Forest.png";
        string theme = new string[] { "", "", "MossRootCavern", "LavaCavern", "HauntedDungeon", "CrimsonCastle" }[stage];
        return "Assets/@ExternalAssets/AreaBackgrounds/Stage" + stage + "/T_Stage" + stage + "_Area" + area + "_" + theme + ".png";
    }

    // Five sprite renderers are authored in the prefab; all scene camera/zone references stay on instances.
    public static void PreparePrefab(int stage)
    {
        CheckStage(stage);
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
        GameObject root = new GameObject("P_Stage" + stage + "_AreaBackgrounds");
        try
        {
            var manager = root.AddComponent<CameraZoneBackgroundManager>();
            var data = new SerializedObject(manager);
            var areas = data.FindProperty("areas");
            areas.arraySize = 5;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            if (material == null) throw new InvalidOperationException("URP 2D background material is missing.");
            for (int i = 0; i < 5; i++)
            {
                var child = new GameObject("Background_" + stage + "-" + (i + 1));
                child.transform.SetParent(root.transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[i];
                renderer.sharedMaterial = material;
                renderer.sortingOrder = -100 + i;
                renderer.enabled = false;
                areas.GetArrayElementAtIndex(i).FindPropertyRelative("renderer").objectReferenceValue = renderer;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(stage));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // Validate every required reference before disabling the legacy background or adding an instance.
    public static CameraZoneBackgroundManager Apply(Scene scene, int stageNumber)
    {
        CheckStage(stageNumber);
        if (scene.path != "Assets/Scenes/Stage" + stageNumber + ".unity" && scene.path != TestScenePath(stageNumber))
            throw new InvalidOperationException("The scene does not match the requested stage.");
        StageManager stage = null;
        PlayerCameraFollow follow = null;
        CameraZoneBackgroundManager manager = null;
        var legacy = new List<Behaviour>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var item in root.GetComponentsInChildren<StageManager>(true))
            {
                if (stage != null) throw new InvalidOperationException("Multiple StageManagers.");
                stage = item;
            }
            foreach (var item in root.GetComponentsInChildren<PlayerCameraFollow>(true))
            {
                if (follow != null) throw new InvalidOperationException("Multiple player cameras.");
                follow = item;
            }
            foreach (var item in root.GetComponentsInChildren<CameraZoneBackgroundManager>(true))
            {
                if (manager != null) throw new InvalidOperationException("Multiple background managers.");
                manager = item;
            }
            legacy.AddRange(root.GetComponentsInChildren<ContinuousStageBackground>(true));
            legacy.AddRange(root.GetComponentsInChildren<ZoneBackground>(true));
            legacy.AddRange(root.GetComponentsInChildren<BackgroundFitter>(true));
        }
        if (stage == null || follow == null) throw new InvalidOperationException("Missing StageManager or player camera.");
        var ordered = new SerializedObject(stage).FindProperty("zoneCameraBounds");
        if (ordered.arraySize != 5) throw new InvalidOperationException("Exactly five ordered camera zones are required.");
        var zones = new CameraBoundsZone[5];
        for (int i = 0; i < zones.Length; i++)
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
        if (manager == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            manager = instance.GetComponent<CameraZoneBackgroundManager>();
        }
        manager.Configure(follow.GetComponent<Camera>(), follow, zones);
        Record(manager);
        foreach (var area in manager.Areas) { Record(area.renderer); Record(area.renderer.transform); }
        EditorSceneManager.MarkSceneDirty(scene);
        return manager;
    }

    private static void CheckStage(int stage)
    {
        if (stage < 1 || stage > 5) throw new ArgumentOutOfRangeException("stage");
    }

    // Retiring old prefab instances uses local overrides, so shared prefab assets elsewhere stay intact.
    private static void Record(UnityEngine.Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
