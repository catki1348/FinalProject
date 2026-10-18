using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오프닝 씬(Assets/Scenes/Opening.unity)을 코드로 생성하는 에디터 도구.
/// 메뉴: Tools/Build Opening Scene
/// 구성(위에서 아래 순서로 그려짐): 배경 → 플레이어 → 타이틀 로고 → 한글 제목/가로선/부제 → PRESS START → 페이드 화면
/// 배치 좌표는 @Edit/OpeningDesign.md 의 최종 시안(OpeningScreen_ShapeTitle3_Gold + PRESS START) 기준(기준 해상도 1920x1080).
/// </summary>
public static class BuildOpeningScene
{
    private const string ScenePath = "Assets/Scenes/Opening.unity";
    private const string SpriteDir = "Assets/Sprites/Opening/";

    [MenuItem("Tools/Build Opening Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 카메라 (UI만 쓰므로 검은 배경)
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        camGo.AddComponent<AudioListener>();

        // 캔버스: 1920x1080 기준, 화면 크기에 맞춰 스케일
        var canvasGo = new GameObject("OpeningCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        CreateStretchedImage(canvasGo.transform, "Background", LoadSprite("Opening_Background"));
        CreateStretchedImage(canvasGo.transform, "Player", LoadSprite("Opening_Player"));

        // 타이틀 로고: 좌상단 기준 (210, 232)에 1500x403
        var title = CreateImage(canvasGo.transform, "Title", LoadSprite("Opening_Title"));
        var tr = title.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
        tr.pivot = new Vector2(0f, 1f);
        tr.anchoredPosition = new Vector2(210f, -232f);
        tr.sizeDelta = new Vector2(1500f, 403f);

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MalgunGothic SDF.asset");
        CreateText(canvasGo.transform, "TitleKorean", "파이널 던전", 80f, 675f, font);

        // 제목과 부제 사이 가로선
        var rule = CreateImage(canvasGo.transform, "TitleRule", null);
        rule.color = Color.white;
        var rr = rule.rectTransform;
        rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 1f);
        rr.pivot = new Vector2(0.5f, 0.5f);
        rr.anchoredPosition = new Vector2(0f, -724f);
        rr.sizeDelta = new Vector2(930f, 3f);

        CreateText(canvasGo.transform, "SubtitleKorean", "마왕성을 향한 마지막 여정", 56f, 775f, font);
        var press = CreateText(canvasGo.transform, "PressStart", "PRESS START", 58f, 930f, font);

        // 전환용 검은 페이드 화면 (처음엔 투명)
        var fadeImg = CreateStretchedImage(canvasGo.transform, "Fade", null);
        fadeImg.color = Color.black;
        var fade = fadeImg.gameObject.AddComponent<CanvasGroup>();
        fade.alpha = 0f;
        fade.blocksRaycasts = false;
        fade.interactable = false;

        // 컨트롤러 연결
        var managerGo = new GameObject("OpeningManager");
        var ctrl = managerGo.AddComponent<OpeningScreenController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("pressStartText").objectReferenceValue = press;
        so.FindProperty("fadeGroup").objectReferenceValue = fade;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        // (오프닝은 이제 MainMenu 씬에 통합됨: 이 별도 씬은 빌드 설정에 추가하지 않는다)
        Debug.Log("[BuildOpeningScene] 저장 완료: " + ScenePath + " (폰트: " + (font != null ? font.name : "NULL") + ")");
    }

    private static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static Image CreateStretchedImage(Transform parent, string name, Sprite sprite)
    {
        var img = CreateImage(parent, name, sprite);
        var r = img.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        return img;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, float yFromTop, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.raycastTarget = false;
        t.outlineColor = new Color32(0, 0, 0, 255);
        t.outlineWidth = 0.22f;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, -yFromTop);
        r.sizeDelta = new Vector2(1200f, 110f);
        return t;
    }

    /// <summary>Opening 씬을 빌드 설정 맨 앞(인덱스 0)에 등록한다. 이미 있으면 위치만 앞으로 옮긴다.</summary>
    private static void AddToBuildSettingsFirst()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == ScenePath);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
