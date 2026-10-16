using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Claude Sonnet 5.5, 2026-10-16. Deploys the stitched, non-repeating panoramas (one per area) to a stage scene
// and retires the tiled-repeat WorldTiledBackground objects that the panoramas replace.
public static class ApplyWorldPanoramaBackgrounds
{
    // Matches Build_WorldPanoramas.py: one image pixel is 1/48 world unit.
    private const float PixelsPerUnit = 48f;

    public static string PrefabPath(int stage)
    {
        return "Assets/@ExternalAssets/@Prefab/P_Stage" + stage + "_PanoramaBackgrounds.prefab";
    }

    public static string ImagePath(int stage, int area)
    {
        return "Assets/@ExternalAssets/WorldBackgrounds/Stage" + stage + "/Panorama/Stage" + stage + "_Area" + area + "_Panorama.png";
    }

    // Import each panorama at a fixed pixel size and build the prefab with five sprite children.
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
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.maxTextureSize = 16384;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
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
        GameObject root = new GameObject("P_Stage" + stage + "_PanoramaBackgrounds");
        try
        {
            for (int i = 0; i < 5; i++)
            {
                var child = new GameObject("PanoramaBackground_" + stage + "-" + (i + 1));
                child.transform.SetParent(root.transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[i];
                renderer.sharedMaterial = material;
                renderer.sortingOrder = -100 + i;
                child.AddComponent<WorldPanoramaBackground>();
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(stage));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // Bind the five panoramas to the stage's ordered zones and switch off the repeat-tiled version.
    public static int Apply(Scene scene, int stageNumber)
    {
        StageManager stage = null;
        WorldPanoramaBackground[] existing = null;
        var tiled = new System.Collections.Generic.List<WorldTiledBackground>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var item in root.GetComponentsInChildren<StageManager>(true)) stage = item;
            tiled.AddRange(root.GetComponentsInChildren<WorldTiledBackground>(true));
            var found = root.GetComponentsInChildren<WorldPanoramaBackground>(true);
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
        if (prefab == null) throw new InvalidOperationException("Prepare the panorama prefab first.");

        // The repeat-tiled backgrounds are replaced, not deleted, so they can be restored if needed.
        foreach (var item in tiled)
        {
            var parent = item.transform.parent != null ? item.transform.parent.gameObject : item.gameObject;
            parent.SetActive(false);
            EditorUtility.SetDirty(parent);
        }

        WorldPanoramaBackground[] panoramas = existing;
        if (panoramas == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            panoramas = instance.GetComponentsInChildren<WorldPanoramaBackground>(true);
        }
        if (panoramas.Length != 5) throw new InvalidOperationException("Expected five panoramas.");
        for (int i = 0; i < 5; i++)
        {
            panoramas[i].Configure(zones[i]);
            EditorUtility.SetDirty(panoramas[i]);
            EditorUtility.SetDirty(panoramas[i].transform);
            if (PrefabUtility.IsPartOfPrefabInstance(panoramas[i])) PrefabUtility.RecordPrefabInstancePropertyModifications(panoramas[i]);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        return panoramas.Length;
    }
}
