using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 튜토리얼 씬(Assets/Scenes/Tutorial.unity)을 만드는 에디터 도구. 메뉴: Tools/Build Tutorial Scene
/// 방식: Stage1 씬을 통째로 복사한 뒤(플레이어, 카메라, HUD, 매니저 연결이 그대로 따라온다) 스테이지 내용만 걷어내고
///       튜토리얼 코스(바닥, 계단 발판, 허수아비, 방어 훈련 장치, 표지)를 새로 놓는다.
/// 코스(왼쪽 → 오른쪽, x 좌표):
///   2 시작 · 6 이동 · 21~25 계단 발판(점프) · 36 대시 · 47 허수아비(공격) · 62 방어 훈련 장치 · 76 도착
/// 매번 실행하면 Stage1 에서 새로 복사해 처음부터 다시 만든다(손으로 고친 내용은 사라진다). 빌드 설정에도 등록한다.
/// 설계: @Edit/TutorialDesign.md
/// </summary>
public static class BuildTutorialScene
{
    private const string SourcePath = "Assets/Scenes/Stage1.unity";
    private const string ScenePath = "Assets/Scenes/Tutorial.unity";

    [MenuItem("Tools/Build Tutorial Scene")]
    public static void Build()
    {
        if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogError("[BuildTutorialScene] 저장되지 않은 변경이 있는 씬이 열려 있습니다. 먼저 저장하세요."); return; }

        AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(SourcePath, ScenePath)) { Debug.LogError("[BuildTutorialScene] Stage1 복사에 실패했습니다."); return; }
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // ---------- 1. 스테이지 내용 걷어내기 ----------
        var keepRoots = new System.Collections.Generic.HashSet<string>
        { "Main Camera", "StageManager", "Sections", "OutOfBoundsTrigger", "Background", "EventSystem", "WarpManager", "HUD_HealthUI", "P_Stage1_Continuous_Forest", "DropConditionManager" };
        foreach (var root in scene.GetRootGameObjects())
            if (!keepRoots.Contains(root.name)) Object.DestroyImmediate(root);

        Transform sections = FindRoot(scene, "Sections").transform;
        Transform section1 = sections.GetChild(0);
        for (int i = sections.childCount - 1; i >= 1; i--) Object.DestroyImmediate(sections.GetChild(i).gameObject);

        Transform player = null, startPoint = null, cameraZone = null, platformTemplate = null;
        for (int i = section1.childCount - 1; i >= 0; i--)
        {
            Transform c = section1.GetChild(i);
            if (c.name == "Player") player = c;
            else if (c.name.StartsWith("ZoneStartPoint")) startPoint = c;
            else if (c.name.StartsWith("Background_Forest")) cameraZone = c;
            else if (c.name.StartsWith("Generated_"))
            {
                Transform g = c.Find("Platform_1-1_G");
                if (g != null) { platformTemplate = Object.Instantiate(g.gameObject).transform; platformTemplate.name = "PlatformTemplate"; platformTemplate.SetParent(null); }
                Object.DestroyImmediate(c.gameObject);
            }
            else Object.DestroyImmediate(c.gameObject);   // ZoneBackground, ZoneEndPoint 등
        }
        if (player == null || startPoint == null || cameraZone == null || platformTemplate == null)
        { Debug.LogError("[BuildTutorialScene] Stage1 에서 필요한 오브젝트를 찾지 못했습니다."); return; }

        // ---------- 2. 시작 지점 / 카메라 구간 / 낙사선 ----------
        startPoint.position = new Vector3(2f, 1.2f, 0f);
        player.position = new Vector3(2f, 1.9f, 0f);

        cameraZone.position = new Vector3(45f, 3f, 0f);
        var zoneSo = new SerializedObject(cameraZone.GetComponent<CameraBoundsZone>());
        zoneSo.FindProperty("size").vector2Value = new Vector2(110f, 14f);
        zoneSo.FindProperty("leftBoundary").objectReferenceValue = null;
        zoneSo.FindProperty("rightBoundary").objectReferenceValue = null;
        zoneSo.ApplyModifiedPropertiesWithoutUndo();

        var oob = FindRoot(scene, "OutOfBoundsTrigger");
        oob.transform.position = new Vector3(45f, -14f, 0f);
        oob.GetComponent<BoxCollider2D>().size = new Vector2(220f, 4f);

        // StageManager: 1영역만 사용(남은 참조는 비움), 이름 정리
        var stage = FindRoot(scene, "StageManager").GetComponent<StageManager>();
        var sm = new SerializedObject(stage);
        sm.FindProperty("nextStageSceneName").stringValue = "";
        var starts = sm.FindProperty("zoneStartPoints");
        var bounds = sm.FindProperty("zoneCameraBounds");
        for (int i = 0; i < starts.arraySize; i++) starts.GetArrayElementAtIndex(i).objectReferenceValue = i == 0 ? startPoint.GetComponent<ZoneStartPoint>() : null;
        for (int i = 0; i < bounds.arraySize; i++) bounds.GetArrayElementAtIndex(i).objectReferenceValue = i == 0 ? cameraZone.GetComponent<CameraBoundsZone>() : null;
        sm.ApplyModifiedPropertiesWithoutUndo();

        // ---------- 3. 튜토리얼 코스 ----------
        var level = new GameObject("Tutorial_Level").transform;
        int playerOrder = 0;
        var playerRenderer = player.GetComponentInChildren<SpriteRenderer>();
        if (playerRenderer != null) playerOrder = playerRenderer.sortingOrder;

        MakePlatform(platformTemplate, level, "Ground_Tutorial", new Vector2(45f, -0.02f), 100f);   // x -5 ~ 95
        MakePlatform(platformTemplate, level, "Step_1", new Vector2(21f, 0.78f), 3.0f);
        MakePlatform(platformTemplate, level, "Step_2", new Vector2(24.6f, 1.58f), 3.0f);
        float groundTop = 0.495f;
        float step2Top = 1.58f + 0.515f;

        var jumpGoal = Marker(level, "JumpGoal", new Vector2(24.6f, step2Top));
        var guardTrainer = BuildGuardTrainer(level, 62f, groundTop, player, playerOrder);
        var dummy = BuildDummy(level, 47f, groundTop, playerOrder);

        // 안내 글자 위치(바닥 표지)
        var labels = new[]
        {
            Label(level, "Label_Move", new Vector2(6f, 2.9f)),
            Label(level, "Label_Jump", new Vector2(22.8f, 3.8f)),
            Label(level, "Label_Dash", new Vector2(36f, 2.9f)),
            Label(level, "Label_Attack", new Vector2(47f, 3.4f)),
            Label(level, "Label_Guard", new Vector2(62f, 3.4f)),
            Label(level, "Label_Goal", new Vector2(76f, 3.2f)),
        };
        var goalPole = AddSprite(level, "GoalPole", Builtin("UI/Skin/UISprite.psd"), new Vector2(76f, groundTop + 1.0f), new Vector2(0.15f, 2.0f), new Color(1f, 0.85f, 0.35f, 1f), playerOrder - 1);
        goalPole.GetComponent<SpriteRenderer>().sortingOrder = playerOrder - 1;

        Object.DestroyImmediate(platformTemplate.gameObject);

        // ---------- 4. 튜토리얼 매니저 ----------
        var managerGo = new GameObject("TutorialManager");
        var manager = managerGo.AddComponent<TutorialManager>();
        var mso = new SerializedObject(manager);
        mso.FindProperty("player").objectReferenceValue = player;
        mso.FindProperty("dummy").objectReferenceValue = dummy;
        mso.FindProperty("guardTrainer").objectReferenceValue = guardTrainer;
        mso.FindProperty("jumpGoal").objectReferenceValue = jumpGoal;
        string[] labelTexts = { "1. 이동", "2. 점프", "3. 대시", "4. 공격", "5. 방어", "도착!" };
        var arr = mso.FindProperty("worldLabels");
        arr.arraySize = labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("anchor").objectReferenceValue = labels[i];
            e.FindPropertyRelative("text").stringValue = labelTexts[i];
        }
        mso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RegisterInBuildSettings();
        Debug.Log("[BuildTutorialScene] 저장 완료: " + ScenePath);
    }

    // ---------- 도우미 ----------

    private static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects()) if (go.name == name) return go;
        return null;
    }

    private static Sprite Builtin(string path) { return AssetDatabase.GetBuiltinExtraResource<Sprite>(path); }

    /// <summary>Stage1 의 바닥 발판을 복제해 위치와 가로 길이를 바꾼다(스프라이트는 타일 방식이라 size 와 콜라이더 크기를 함께 바꾼다).</summary>
    private static void MakePlatform(Transform template, Transform parent, string name, Vector2 pos, float width)
    {
        var go = Object.Instantiate(template.gameObject, parent);
        go.name = name;
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.size = new Vector2(width, sr.size.y);
        var box = go.GetComponent<BoxCollider2D>();
        box.size = new Vector2(width, box.size.y);
    }

    private static Transform Marker(Transform parent, string name, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        return go.transform;
    }

    private static Transform Label(Transform parent, string name, Vector2 pos) { return Marker(parent, name, pos); }

    /// <summary>스프라이트 한 장을 월드 크기(가로, 세로)에 맞춰 놓는다.</summary>
    private static Transform AddSprite(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        Vector2 native = sprite.bounds.size;
        go.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
        return go.transform;
    }

    /// <summary>허수아비: 기둥 + 가로대 + 머리. 적 레이어의 키네마틱 Rigidbody2D + 트리거 콜라이더(플레이어를 막지 않고 검에는 맞는다).</summary>
    private static TutorialDummy BuildDummy(Transform parent, float x, float groundTop, int playerOrder)
    {
        var root = new GameObject("TrainingDummy");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(x, groundTop, 0f);
        root.layer = LayerMask.NameToLayer("Enemy");
        var rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1.5f, 2.2f);
        box.offset = new Vector2(0f, 1.1f);

        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        Sprite rect = Builtin("UI/Skin/UISprite.psd"), knob = Builtin("UI/Skin/Knob.psd");
        int order = playerOrder - 1;
        var wood = new Color(0.62f, 0.42f, 0.22f, 1f);
        var parts = new[]
        {
            AddSprite(visual, "Pole", rect, new Vector2(x, groundTop + 0.9f), new Vector2(0.22f, 1.8f), wood, order),
            AddSprite(visual, "Arms", rect, new Vector2(x, groundTop + 1.35f), new Vector2(1.3f, 0.2f), wood, order),
            AddSprite(visual, "Head", knob, new Vector2(x, groundTop + 1.95f), new Vector2(0.55f, 0.55f), new Color(0.85f, 0.7f, 0.45f, 1f), order),
            AddSprite(visual, "Base", rect, new Vector2(x, groundTop + 0.08f), new Vector2(0.8f, 0.16f), new Color(0.4f, 0.28f, 0.16f, 1f), order),
        };
        var dummy = root.AddComponent<TutorialDummy>();
        var so = new SerializedObject(dummy);
        so.FindProperty("visualRoot").objectReferenceValue = visual;
        var flash = so.FindProperty("flashTargets");
        flash.arraySize = parts.Length;
        for (int i = 0; i < parts.Length; i++) flash.GetArrayElementAtIndex(i).objectReferenceValue = parts[i].GetComponent<SpriteRenderer>();
        so.ApplyModifiedPropertiesWithoutUndo();
        return dummy;
    }

    /// <summary>방어 훈련 장치: 받침대 + 위의 빨간 신호등 + 충격파 링. 예고 → 충격파를 반복한다.</summary>
    private static TutorialGuardTrainer BuildGuardTrainer(Transform parent, float x, float groundTop, Transform player, int playerOrder)
    {
        var root = new GameObject("GuardTrainer");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(x, groundTop, 0f);
        var box = root.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1.2f, 1.6f);
        box.offset = new Vector2(0f, 0.8f);

        Sprite rect = Builtin("UI/Skin/UISprite.psd"), knob = Builtin("UI/Skin/Knob.psd");
        int order = playerOrder - 1;
        AddSprite(root.transform, "Pedestal", rect, new Vector2(x, groundTop + 0.6f), new Vector2(0.9f, 1.2f), new Color(0.28f, 0.3f, 0.38f, 1f), order);
        var lamp = AddSprite(root.transform, "Lamp", knob, new Vector2(x, groundTop + 1.5f), new Vector2(0.7f, 0.7f), new Color(0.35f, 0.08f, 0.08f, 1f), order + 1);
        var ring = AddSprite(root.transform, "Shockwave", knob, new Vector2(x, groundTop + 1.0f), new Vector2(1f, 1f), new Color(1f, 0.5f, 0.35f, 1f), order + 2);

        var trainer = root.AddComponent<TutorialGuardTrainer>();
        var so = new SerializedObject(trainer);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("lamp").objectReferenceValue = lamp.GetComponent<SpriteRenderer>();
        so.FindProperty("shockwave").objectReferenceValue = ring.GetComponent<SpriteRenderer>();
        so.ApplyModifiedPropertiesWithoutUndo();
        return trainer;
    }

    private static void RegisterInBuildSettings()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in list) if (s.path == ScenePath) { s.enabled = true; EditorBuildSettings.scenes = list.ToArray(); return; }
        list.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
