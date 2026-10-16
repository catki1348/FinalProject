using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Codex (GPT-6), 2026-10-16. Replace the oversized panorama with five camera-scale forest images.
public static class ApplyStage1AreaBackgrounds
{
    public const string PrefabPath = "Assets/@ExternalAssets/@Prefab/P_Stage1_AreaBackgrounds.prefab";

    // Import all five dedicated images with their real source resolution and full rectangular coverage.
    public static void PreparePrefab()
    {
        GameObject root = new GameObject("P_Stage1_AreaBackgrounds");
        try
        {
            var manager = root.AddComponent<CameraZoneBackgroundManager>();
            var data = new SerializedObject(manager);
            var areas = data.FindProperty("areas");
            areas.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                string path = "Assets/@ExternalAssets/ForestBackground/T_Stage1_Area" + (i + 1) + "_Forest.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
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
                var child = new GameObject("Background_1-" + (i + 1));
                child.transform.SetParent(root.transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
                renderer.sortingOrder = -100 + i;
                renderer.enabled = false;
                areas.GetArrayElementAtIndex(i).FindPropertyRelative("renderer").objectReferenceValue = renderer;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // StageManager's ordered zone list is the source of truth, rather than hierarchy names or position.
    public static CameraZoneBackgroundManager Apply(Scene scene)
    {
        if (scene.path != "Assets/Scenes/Stage1.unity" && scene.path != "Assets/Scenes/Test062.unity")
            throw new InvalidOperationException("Only Stage1 and its dedicated validation copy are supported.");
        StageManager stage = null;
        PlayerCameraFollow follow = null;
        CameraZoneBackgroundManager manager = null;
        var legacy = new List<Behaviour>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var item in root.GetComponentsInChildren<StageManager>(true)) stage = item;
            foreach (var item in root.GetComponentsInChildren<PlayerCameraFollow>(true)) follow = item;
            foreach (var item in root.GetComponentsInChildren<CameraZoneBackgroundManager>(true)) manager = item;
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
            if (zones[i] == null) throw new InvalidOperationException("Missing zone reference " + i);
        }
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
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            manager = instance.GetComponent<CameraZoneBackgroundManager>();
        }
        manager.Configure(follow.GetComponent<Camera>(), follow, zones);
        Record(manager);
        foreach (var area in manager.Areas) { Record(area.renderer); Record(area.renderer.transform); }
        EditorSceneManager.MarkSceneDirty(scene);
        return manager;
    }

    // Retain prefab overrides on reopening the saved scene.
    private static void Record(UnityEngine.Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
