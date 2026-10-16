using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 1-5 재설계 배치 코드. JSON 좌표를 원본 프리팹에 저장하고 해당 영역에만 적용한다.
// OpenAI Codex (GPT-6), 2026-10-14.
public static class DeployStage1BossRedesign
{
    public const string ArenaPath = "Assets/@ExternalAssets/@Prefab/P_Stage1_TreantArena.prefab";
    public const string LedgePath = "Assets/@ExternalAssets/@Prefab/P_Stage1_TreantLedge.prefab";
    private const string BossPath = "Assets/@ExternalAssets/@Prefab/Boss_Stage1_Treant.prefab";
    private const string TilePath = "Assets/@ExternalAssets/Tiles/T_Stage1_TreantArena_Tiled.png";
    private const string LedgeTilePath = "Assets/@ExternalAssets/Tiles/T_Stage1_TreantLedge_Tiled.png";
    private const string WhitePath = "Assets/@ExternalAssets/Tiles/T_Stage1_TreantSlam_White.png";
    // 단차 0.8: 1회 점프(최대 1.376)로 한 단만 오르고 두 단(1.6)은 못 오른다. 보스 형상 높이 10.16 → 7.0.
    private const float BossHeight = 7f, HeadBottom = 4.95f, BossVisualScale = 7f / 10.16f;
    private const float LedgeThickness = 0.36f;
    [Serializable] private class Ledge { public string id; public float x, top, width, thickness; public int tier, side; }
    [Serializable] private class Layout { public Ledge[] platforms; }

    // 새 레이어를 확보하고 충돌 제외는 전용 프리팹에 저장한다(전역 기존 Ground 설정은 유지).
    private static int EnsureLayer()
    {
        const string name = "BossArenaLedge";
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = settings.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
            layers.GetArrayElementAtIndex(i).stringValue = name;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return i;
        }
        throw new InvalidOperationException("No free layer for boss arena ledges");
    }

    // 기존 숲 발판 이미지의 빈 여백 없는 부분을 전용 반복 타일로 준비한다.
    private static Sprite CropTile(string path, RectInt rect, float ppu)
    {
        if (!File.Exists(path))
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes("Assets/@ExternalAssets/Tiles/Platform_GrassStone.png"));
            var texture = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
            texture.SetPixels(source.GetPixels(rect.x, rect.y, rect.width, rect.height));
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(path);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 경고/활성 색상은 런타임에서 설정하는 작은 단색 타일이다.
    private static Sprite WhiteTile()
    {
        if (!File.Exists(WhitePath))
        {
            var texture = new Texture2D(4, 4);
            texture.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray()); texture.Apply();
            File.WriteAllBytes(WhitePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(WhitePath);
        }
        var imp = (TextureImporter)AssetImporter.GetAtPath(WhitePath);
        imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.spritePixelsPerUnit = 4; // Multiple이면 스프라이트가 0개가 되어 경고 띠가 보이지 않았다
        imp.mipmapEnabled = false; imp.filterMode = FilterMode.Point;
        var settings = new TextureImporterSettings(); imp.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(settings); imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath);
    }

    // 늘린 Transform 대신 Tiled 크기와 독립 콜라이더 크기를 일치시킨다.
    private static void SetPlatform(GameObject go, Sprite sprite, Vector2 size)
    {
        go.transform.localScale = Vector3.one;
        var sr = go.GetComponent<SpriteRenderer>(); if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size; sr.sortingOrder = 2;
        var collider = go.GetComponent<BoxCollider2D>(); if (collider == null) collider = go.AddComponent<BoxCollider2D>();
        collider.autoTiling = false; collider.size = size; collider.offset = Vector2.zero;
    }

    // 바닥(T_Stage1_TreantArena_Tiled, 512ppu)과 같은 원본에서 잔디 윗면·흙·돌 한 줄 단면만 이어 붙인 얇은 슬랩 타일.
    // 같은 재질·같은 픽셀 밀도라 바닥과 한 세트로 보인다(0.6x0.36 = 307x185px). 원본 좌표는 위에서 아래 기준.
    private static Sprite ComposeLedge()
    {
        if (!File.Exists(LedgeTilePath))
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes("Assets/@ExternalAssets/Tiles/Platform_GrassStone.png"));
            const int x = 340, width = 307;
            // (원본 위쪽 y, 높이): 잔디 윗면 70, 흙과 잔디 아랫단 60, 돌 한 줄 55
            var bands = new[] { new Vector2Int(245, 70), new Vector2Int(452, 60), new Vector2Int(576, 55) };
            var texture = new Texture2D(width, 185, TextureFormat.RGBA32, false);
            int destTop = 185;
            foreach (var band in bands)
            {
                int srcBottom = source.height - band.x - band.y;
                destTop -= band.y;
                texture.SetPixels(0, destTop, width, band.y, source.GetPixels(x, srcBottom, width, band.y));
            }
            texture.Apply();
            File.WriteAllBytes(LedgeTilePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(LedgeTilePath);
        }
        return CropTile(LedgeTilePath, default, 512);
    }

    // 원본 발판 프리팹을 저장한 후 삼각 격자의 모든 반복 인스턴스가 이를 사용한다.
    private static GameObject BuildArena(int ledgeLayer)
    {
        var floorSprite = CropTile(TilePath, new RectInt(320, 240, 384, 512), 512);
        var ledgeSprite = ComposeLedge();
        var template = new GameObject("P_Stage1_TreantLedge");
        template.layer = ledgeLayer;
        SetPlatform(template, ledgeSprite, new Vector2(0.6f, LedgeThickness));
        var c = template.GetComponent<BoxCollider2D>(); c.usedByEffector = true;
        c.excludeLayers = 1 << LayerMask.NameToLayer("Enemy");
        var effector = template.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true; effector.useOneWayGrouping = true; effector.surfaceArc = 160;
        // PlatformEffector의 독립 충돌 마스크도 Enemy를 제외해야 보스가 발판에 서지 않는다.
        effector.useColliderMask = true; effector.colliderMask = ~(1 << LayerMask.NameToLayer("Enemy"));
        template.AddComponent<BossArenaLedge>();
        var ledgePrefab = PrefabUtility.SaveAsPrefabAsset(template, LedgePath);
        UnityEngine.Object.DestroyImmediate(template);

        var arena = new GameObject("P_Stage1_TreantArena");
        var floor = new GameObject("ArenaFloor"); floor.transform.SetParent(arena.transform, false);
        floor.transform.localPosition = new Vector3(0, -0.5f, 0);
        floor.layer = LayerMask.NameToLayer("Ground"); SetPlatform(floor, floorSprite, new Vector2(28, 1));
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText("@Edit/MapDesign_Stage1-5_Redesign.json"));
        if (layout.platforms.Length != 45) throw new InvalidOperationException("Expected 45 ledges");
        foreach (var p in layout.platforms)
        {
            var ledge = (GameObject)PrefabUtility.InstantiatePrefab(ledgePrefab);
            ledge.name = "Platform_1-5_" + (p.side < 0 ? "L" : p.side > 0 ? "R" : "C") + "_Tier" + p.tier + "_" + p.id;
            ledge.transform.SetParent(arena.transform, false);
            ledge.transform.localPosition = new Vector3(p.x, p.top - p.thickness / 2, 0);
        }
        var warning = new GameObject("GroundSlamIndicator"); warning.transform.SetParent(arena.transform, false);
        var indicator = warning.AddComponent<SpriteRenderer>(); indicator.sprite = WhiteTile();
        indicator.drawMode = SpriteDrawMode.Tiled; indicator.tileMode = SpriteTileMode.Continuous;
        indicator.size = new Vector2(28, 0.25f); indicator.sortingOrder = 5; indicator.enabled = false;
        var prefab = PrefabUtility.SaveAsPrefabAsset(arena, ArenaPath);
        UnityEngine.Object.DestroyImmediate(arena);
        return prefab;
    }

    // 픽셀 프레임의 머리 중심을 Visual 로컬 위치로 바꾼다. 방향 반전은 부모 Visual을 따라간다.
    private static Vector2 HeadPixel(Sprite sprite)
    {
        string n = sprite.name;
        if (n.StartsWith("05_")) return new Vector2(590, 710);
        if (n.StartsWith("04_")) return new Vector2(565, 490);
        if (n.StartsWith("10_") || n.StartsWith("12_")) return new Vector2(560, 520);
        if (n.StartsWith("11_")) return new Vector2(520, 390);
        if (n.StartsWith("06_") || n.StartsWith("07_") || n.StartsWith("08_") || n.StartsWith("09_")) return new Vector2(600, 370);
        return new Vector2(540, 340);
    }

    // 기존 sprite 교체 키와 같은 시간에 머리 앵커를 이동시켜 공격/그로기 자세를 따라가게 한다.
    public static void ConfigureHeadClips()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Animations/Boss/Stage1_Treant" }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip.name == "Boss_Death") continue;
            var spriteBinding = AnimationUtility.GetObjectReferenceCurveBindings(clip).First(b => b.type == typeof(SpriteRenderer));
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);
            foreach (var axis in new[] { "x", "y" })
            {
                var curve = new AnimationCurve();
                foreach (var key in keys)
                {
                    var sprite = (Sprite)key.value; if (sprite == null) continue;
                    Vector2 pixel = HeadPixel(sprite);
                    Vector2 point = new Vector2((pixel.x - sprite.pivot.x) / sprite.pixelsPerUnit,
                        (sprite.rect.height - pixel.y - sprite.pivot.y) / sprite.pixelsPerUnit);
                    curve.AddKey(new Keyframe(key.time, axis == "x" ? point.x : point.y));
                }
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("HeadAnchor", typeof(Transform), "m_LocalPosition." + axis), curve);
            }
            EditorUtility.SetDirty(clip);
        }
    }

    // 실제 보스 원본 프리팹에 행동 보완/머리 위치/발판 제외를 저장한다.
    private static void ConfigureBoss(int layer)
    {
        var root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            var visual = root.GetComponentInChildren<Animator>().transform;
            // 2026-10-14: 보스 높이 7.0(스프라이트 형상 10.16 기준 배율). 5단 최상층(윗면 4.0)에서 서서 검을 휘두를 때만
            // 머리 판정(하단 4.95)에 닿는다. 4단 검 도달 4.593 < 4.95 < 5단 검 도달 5.393.
            visual.localScale = new Vector3(BossVisualScale, BossVisualScale, 1);
            var head = visual.Find("HeadAnchor");
            if (head == null) { head = new GameObject("HeadAnchor").transform; head.SetParent(visual, false); }
            head.localPosition = new Vector3(1.08f, 7.56f, 0);
            // 몸통 판정: 지면~머리 하단. 전 단에서 타격 가능하지만 머리 근접 배율은 거리로만 반영된다.
            var body = root.GetComponent<BoxCollider2D>();
            body.size = new Vector2(2.4f, HeadBottom); body.offset = new Vector2(0, HeadBottom / 2);
            // 머리 판정: 월드 x -0.4~1.5, y 4.95~7.0. Visual 자식이라 방향 반전을 따라가며 트리거로 플레이어와 부딪히지 않는다.
            var headBox = visual.Find("HeadHurtbox");
            if (headBox == null) { headBox = new GameObject("HeadHurtbox").transform; headBox.SetParent(visual, false); }
            headBox.gameObject.layer = root.layer;
            var headCollider = headBox.GetComponent<BoxCollider2D>();
            if (headCollider == null) headCollider = headBox.gameObject.AddComponent<BoxCollider2D>();
            headCollider.isTrigger = true;
            headBox.localPosition = new Vector3(0.55f / BossVisualScale, (HeadBottom + BossHeight) / 2 / BossVisualScale, 0);
            headCollider.size = new Vector2(1.9f / BossVisualScale, (BossHeight - HeadBottom) / BossVisualScale);
            var bonus = root.GetComponent<BossHeadProximityDamage>();
            if (bonus == null) bonus = root.AddComponent<BossHeadProximityDamage>();
            var so = new SerializedObject(bonus); so.FindProperty("headAnchor").objectReferenceValue = head; so.ApplyModifiedPropertiesWithoutUndo();
            if (root.GetComponent<BossArenaGroundSlam>() == null) root.AddComponent<BossArenaGroundSlam>();
            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true)) collider.excludeLayers |= 1 << layer;
            var controller = new SerializedObject(root.GetComponent<BossController>());
            controller.FindProperty("specialMinRange").floatValue = 0;
            controller.FindProperty("groundLayer").intValue = 1 << LayerMask.NameToLayer("Ground");
            // 몸집(약 2.3배)에 맞춰 접근·타격 거리를 키우고, 주먹은 4단(발 3.2)까지 닿고 5단(발 4.0)은 닿지 않게 한다.
            controller.FindProperty("stopDistance").floatValue = 2.2f;
            controller.FindProperty("meleeRange").floatValue = 4f;
            controller.FindProperty("punchReach").floatValue = 4.2f;
            controller.FindProperty("punchHeight").floatValue = 3.5f;
            controller.FindProperty("landingStandoff").floatValue = 3f;
            controller.FindProperty("edgeProbeDistance").floatValue = 1.5f;
            controller.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        ConfigureHeadClips();
    }

    // 현재 사용 중인 플레이어 원본에 발판 접지와 하강 기능을 추가한다.
    private static void ConfigurePlayer(GameObject player, int layer)
    {
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var so = new SerializedObject(root.GetComponent<PlayerMovement>());
            so.FindProperty("groundLayer").intValue |= 1 << layer; so.ApplyModifiedPropertiesWithoutUndo();
            if (root.GetComponent<PlayerPlatformDrop>() == null) root.AddComponent<PlayerPlatformDrop>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        // 기존 씬에서 groundLayer를 override한 경우에도 이 영역의 발판을 감지한다.
        var instance = new SerializedObject(player.GetComponent<PlayerMovement>());
        instance.FindProperty("groundLayer").intValue |= 1 << layer; instance.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Boss/Apply Stage1 Treant Arena Redesign")]
    private static void ApplyFromMenu() { Debug.Log(Apply()); }

    // 사용자가 지시한 Unity 적용을 재현한다.
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Apply in edit mode only");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Stage1.unity");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage1.unity", OpenSceneMode.Additive);
        var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        var section = transforms.First(t => t.name == "Section_5");
        var boss = section.GetComponentsInChildren<Boss>(true).Single();
        var player = transforms.First(t => t.GetComponent<PlayerMovement>() != null).gameObject;
        int layer = EnsureLayer();
        var arenaPrefab = BuildArena(layer); ConfigureBoss(layer); ConfigurePlayer(player, layer);
        // 재실행도 동일한 원본 프리팹 결과를 만든다. 다른 영역 오브젝트는 건드리지 않는다.
        foreach (var old in section.Cast<Transform>().Where(t => t.name == "Platform_GrassStone_1-5" || t.name == "Arena_1-5_Redesign").ToArray())
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        var arena = (GameObject)PrefabUtility.InstantiatePrefab(arenaPrefab, scene);
        arena.name = "Arena_1-5_Redesign"; arena.transform.SetParent(section, false);
        arena.transform.localPosition = new Vector3(0, 1, 0);
        boss.transform.position = arena.transform.position + new Vector3(0, 0.02f, 0);
        var floor = arena.GetComponentsInChildren<BoxCollider2D>().Single(c => c.gameObject.GetComponent<BossArenaLedge>() == null);
        boss.GetComponent<BossArenaGroundSlam>().Configure(floor, arena.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "GroundSlamIndicator"));
        PrefabUtility.RecordPrefabInstancePropertyModifications(boss.GetComponent<BossArenaGroundSlam>());
        var cameraBounds = section.GetComponentInChildren<CameraBoundsZone>();
        var cam = new SerializedObject(cameraBounds); cam.FindProperty("orthographicSize").floatValue = 6.5f;
        cam.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Stage1 Section_5: 3 equilateral pyramids / 45 ledges, continuous floor, boss collision exclusion, whole-ground slam, head damage, drop input applied.";
    }

    // OpenAI Codex (GPT-6), 2026-10-15: 발판만 갱신하여 이후 추가된 보스 패턴/크기/애니메이션을 보존한다.
    public static string ApplyLayoutOnly()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use edit mode");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Stage1.unity");
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage1.unity", OpenSceneMode.Additive);
        var section = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == "Section_5");
        var prefab = BuildArena(EnsureLayer());
        var old = section.Cast<Transform>().Single(t => t.name == "Arena_1-5_Redesign");
        Vector3 position = old.localPosition;
        UnityEngine.Object.DestroyImmediate(old.gameObject);
        var arena = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        arena.name = "Arena_1-5_Redesign"; arena.transform.SetParent(section, false); arena.transform.localPosition = position;
        var floor = arena.GetComponentsInChildren<BoxCollider2D>().Single(c => c.GetComponent<BossArenaLedge>() == null);
        var slam = section.GetComponentInChildren<BossArenaGroundSlam>();
        slam.Configure(floor, arena.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "GroundSlamIndicator"));
        PrefabUtility.RecordPrefabInstancePropertyModifications(slam);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return "Applied 3 pyramids / 45 ledges; existing boss configuration preserved.";
    }
}
