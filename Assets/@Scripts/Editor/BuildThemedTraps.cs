using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// 원본 시트는 보존하고 Sprite rect와 흰색 키 셰이더로 함정 외관을 분리한다.
public static class BuildThemedTraps
{
    public const string Output = "Assets/@ExternalAssets/@Prefab/Trap/Themed";
    static readonly string[] Themes = { "Stage1_Forest", "Stage2_Jungle", "Stage3_Lava", "Stage4_DemonCave", "Stage5_DemonCastle" };
    static readonly string[] Kinds = { "Switch", "Launcher", "Projectile", "Teleport" };

    // 기존 프리팹의 동작 설정을 복제하며 테마별 내부 참조를 연결한다.
    public static string Build()
    {
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        var shader = Shader.Find("Traps/WhiteKeySprite");
        if (shader == null) throw new Exception("WhiteKeySprite shader missing");
        string materialPath = "Assets/Sprites/Traps/Themed/M_TrapWhiteKey.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, materialPath); }
        foreach (string theme in Themes)
        {
            string folder = Output + "/" + theme;
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var sprites = Slice(theme);
            var sw = Copy("Switch", theme, folder);
            SetVisual(sw, sprites[0], mat, 1.1f, true);
            var box = sw.GetComponent<BoxCollider2D>();
            box.size = new Vector2(1.1f, .18f); box.offset = new Vector2(0, .09f);
            Save(sw, folder, "Switch", theme);

            var projectile = Copy("Projectile", theme, folder);
            SetVisual(projectile, sprites[2], mat, .65f, false);
            var projectileAsset = Save(projectile, folder, "Projectile", theme);

            var launcher = Copy("Launcher", theme, folder);
            var render = SetVisual(launcher, sprites[1], mat, 1.6f, false);
            var switchAsset = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/P_Trap_Switch_" + theme + ".prefab");
            var childSwitch = (GameObject)PrefabUtility.InstantiatePrefab(switchAsset, launcher.transform);
            childSwitch.transform.localPosition = new Vector3(-2, -.5f, 0);
            var serial = new SerializedObject(launcher.GetComponent<ProjectileLauncher>());
            serial.FindProperty("switchTrigger").objectReferenceValue = childSwitch.GetComponent<SwitchTrigger>();
            serial.FindProperty("projectilePrefab").objectReferenceValue = projectileAsset.GetComponent<TrapProjectile>();
            serial.FindProperty("warningTarget").objectReferenceValue = render;
            serial.FindProperty("launchDirection").vector2Value = Vector2.right;
            var muzzle = serial.FindProperty("firePoint").objectReferenceValue as Transform;
            muzzle.localPosition = new Vector3(.95f, 0, 0);
            serial.ApplyModifiedPropertiesWithoutUndo();
            Save(launcher, folder, "Launcher", theme);

            var teleport = Copy("Teleport", theme, folder);
            SetVisual(teleport, sprites[0], mat, 1.1f, true);
            var rune = new GameObject("TeleportRune"); rune.transform.SetParent(teleport.transform, false);
            rune.transform.localPosition = new Vector3(1.6f, 0, 0);
            var runeRenderer = rune.AddComponent<SpriteRenderer>();
            Assign(runeRenderer, sprites[3], mat, 1.4f, true);
            var teleportBox = teleport.GetComponent<BoxCollider2D>();
            teleportBox.size = new Vector2(1.1f, .18f); teleportBox.offset = new Vector2(0, .09f);
            Save(teleport, folder, "Teleport", theme);
        }
        AssetDatabase.SaveAssets();
        return "Created 20 themed trap prefabs";
    }

    // 각 사분면의 흰 여백을 제외한 영역을 측정해 독립 Sprite asset을 만든다.
    static Sprite[] Slice(string theme)
    {
        string path = "Assets/Sprites/Traps/Themed/" + theme + "_Traps.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.isReadable = true; importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096; importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var pixels = texture.GetPixels32();
        var result = new Sprite[4];
        for (int k = 0; k < 4; k++)
        {
            int x0 = k % 2 * texture.width / 2, y0 = (k < 2 ? 1 : 0) * texture.height / 2;
            int minX = texture.width, minY = texture.height, maxX = 0, maxY = 0;
            for (int y = y0; y < y0 + texture.height / 2; y++)
            for (int x = x0; x < x0 + texture.width / 2; x++)
            {
                var c = pixels[y * texture.width + x];
                if (Math.Min(c.r, Math.Min(c.g, c.b)) > 247) continue;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            var sprite = Sprite.Create(texture, new Rect(minX, minY, maxX-minX+1, maxY-minY+1), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "T_Trap_" + Kinds[k] + "_" + theme;
            string assetPath = "Assets/Sprites/Traps/Themed/" + sprite.name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath)) throw new Exception("Asset already exists: " + assetPath);
            AssetDatabase.CreateAsset(sprite, assetPath); result[k] = sprite;
        }
        return result;
    }

    // 프리팹 편집용 분리 씬을 사용해 현재 작업 씬에 영향을 주지 않는다.
    static GameObject Copy(string kind, string theme, string folder)
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/@ExternalAssets/@Prefab/Trap/P_Trap_" + kind + ".prefab");
        root.name = "P_Trap_" + kind + "_" + theme;
        root.transform.localScale = Vector3.one;
        return root;
    }

    // 기존 임시 도형 렌더러를 숨기고 실제 함정 외관 하나를 부착한다.
    static SpriteRenderer SetVisual(GameObject root, Sprite sprite, Material mat, float width, bool grounded)
    {
        foreach (var old in root.GetComponentsInChildren<SpriteRenderer>(true)) old.enabled = false;
        var visual = new GameObject("ThemedVisual"); visual.transform.SetParent(root.transform, false);
        var render = visual.AddComponent<SpriteRenderer>(); Assign(render, sprite, mat, width, grounded);
        return render;
    }

    // 종횡비와 픽셀 경계를 유지하며 지면형 장치는 발판 높이에 하단을 맞춘다.
    static void Assign(SpriteRenderer render, Sprite sprite, Material mat, float width, bool grounded)
    {
        render.sprite = sprite; render.sharedMaterial = mat; render.sortingOrder = 12;
        float scale = width / sprite.bounds.size.x;
        render.transform.localScale = Vector3.one * scale;
        if (grounded) render.transform.localPosition += new Vector3(0, sprite.bounds.size.y * scale / 2, 0);
    }

    // 자산을 저장한 뒤 임시 프리팹 편집 컨텍스트를 닫는다.
    static GameObject Save(GameObject root, string folder, string kind, string theme)
    {
        string path = folder + "/P_Trap_" + kind + "_" + theme + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) throw new Exception("Prefab already exists: " + path);
        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return saved;
    }
}
