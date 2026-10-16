using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Codex (GPT-6), 2026-10-16. Targeted Stage 1 background deployment; gameplay is preserved.
public static class ApplyStage1ContinuousBackground
{
    public const string TexturePath = "Assets/@ExternalAssets/ForestBackground/T_Stage1_Continuous_Forest.png";
    public const string PrefabPath = "Assets/@ExternalAssets/@Prefab/P_Stage1_Continuous_Forest.prefab";

    // Import the dedicated texture without changing shared forest textures used elsewhere.
    public static void PreparePrefab()
    {
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        GameObject prefabRoot = new GameObject("P_Stage1_Continuous_Forest");
        try
        {
            SpriteRenderer renderer = prefabRoot.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            renderer.sortingOrder = -100;
            prefabRoot.transform.position = new Vector3(0f, 0f, 10f);
            prefabRoot.AddComponent<ContinuousStageBackground>();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(prefabRoot); }
    }

    // Work on the supplied scene only; old zone objects retain their camera and transition references.
    public static ContinuousStageBackground Apply(Scene scene)
    {
        if (scene.path != "Assets/Scenes/Stage1.unity" && scene.path != "Assets/Scenes/Test061.unity")
            throw new InvalidOperationException("Only Stage1 and its dedicated validation copy may be changed.");
        var zones = new List<CameraBoundsZone>();
        ContinuousStageBackground current = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            zones.AddRange(root.GetComponentsInChildren<CameraBoundsZone>(true));
            foreach (var shared in root.GetComponentsInChildren<ContinuousStageBackground>(true)) current = shared;
            foreach (var legacy in root.GetComponentsInChildren<ZoneBackground>(true))
            {
                legacy.enabled = false;
                legacy.GetComponent<SpriteRenderer>().enabled = false;
                EditorUtility.SetDirty(legacy);
                EditorUtility.SetDirty(legacy.GetComponent<SpriteRenderer>());
            }
            foreach (var legacy in root.GetComponentsInChildren<BackgroundFitter>(true))
            {
                legacy.enabled = false;
                legacy.GetComponent<SpriteRenderer>().enabled = false;
                EditorUtility.SetDirty(legacy);
                EditorUtility.SetDirty(legacy.GetComponent<SpriteRenderer>());
            }
        }
        if (zones.Count != 5) throw new InvalidOperationException("Stage 1 requires exactly five camera zones.");
        foreach (var zone in zones)
        {
            SpriteRenderer renderer = zone.GetComponent<SpriteRenderer>();
            if (renderer != null) { renderer.enabled = false; EditorUtility.SetDirty(renderer); }
        }
        if (current == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            current = instance.GetComponent<ContinuousStageBackground>();
        }
        current.Configure(zones.ToArray());
        PrefabUtility.RecordPrefabInstancePropertyModifications(current);
        PrefabUtility.RecordPrefabInstancePropertyModifications(current.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(current.GetComponent<SpriteRenderer>());
        EditorSceneManager.MarkSceneDirty(scene);
        return current;
    }
}
