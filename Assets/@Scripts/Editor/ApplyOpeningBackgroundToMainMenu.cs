using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴(Assets/Scenes/MainMenu.unity)에 오프닝 배경을 적용하는 에디터 도구.
/// 메뉴: Tools/Apply Opening Background To MainMenu
/// - 기존 단색 Background 이미지에 Opening_Background(타이틀 글자·PRESS START가 없는 풍경 이미지)를 넣는다.
/// - 그 위에 Opening_Player(절벽 위 기사)와 글씨가 잘 보이도록 약한 어두운 막(BackgroundDim)을 쌓는다.
/// - 오프닝과 메인 메뉴는 이 한 씬(MainMenu)에서 연속으로 보여준다: 시작 시 오프닝 연출, 엔터를 누르면 같은 화면에서 메뉴로 전환(OpeningScreenController).
/// - 빌드 순서는 MainMenu(0) → Stage1~5. 별도 Opening 씬은 빌드에서 뺀다.
/// - 제목 'Dungeon' 텍스트는 FINAL DUNGEON 로고 이미지(Opening_Title)로 교체한다(텍스트는 끄고, 레이아웃 칸은 유지해 버튼 위치는 그대로).
/// - 버튼 글자(START/OPTIONS/CREDITS)도 타이틀과 같은 금색 3D 글자 이미지(Menu_*.png)로 교체한다(글자는 끄고 자식 이미지로 표시).
/// - 버튼 배경도 타이틀 톤(짙은 남색 판 + 금색 테두리)으로 교체한다: 평소 Button_Normal, 선택/호버 Button_Highlight, 눌림 Button_Pressed (SpriteSwap).
/// - 메뉴(MenuLayout) 크기를 기존의 70%로 줄이고(배율 1.98 → 1.386) 버튼 묶음 중심이 화면 y=663에 오도록 위치를 보정한다. 로고는 크기·위치를 그대로 유지한다.
/// - 선택된 메뉴 버튼에 금색 오라(Menu_Aura + MenuSelectionAura)가 나오도록 각 버튼에 오라 이미지를 달고, 시작 시 START 가 선택되게 한다.
/// 버튼 동작(MainMenuManager)은 건드리지 않는다. 다시 실행해도 중복 생성되지 않는다.
/// </summary>
public static class ApplyOpeningBackgroundToMainMenu
{
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const string OpeningPath = "Assets/Scenes/Opening.unity";
    private const string SpriteDir = "Assets/Sprites/Opening/";

    [MenuItem("Tools/Apply Opening Background To MainMenu")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);

        // 메인 메뉴 캔버스를 이름이 MainMenuCanvas 인 Canvas 컴포넌트에서 찾는다(씬 전체 문자열 검색 대신 컴포넌트 순회).
        Canvas canvas = null;
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.rootCanvas == c) { canvas = c; break; }
        }
        if (canvas == null) { Debug.LogError("[ApplyOpeningBackground] Canvas 를 찾지 못했습니다."); return; }

        Transform bgTr = canvas.transform.Find("Background");
        if (bgTr == null) { Debug.LogError("[ApplyOpeningBackground] Background 오브젝트가 없습니다."); return; }

        var bg = bgTr.GetComponent<Image>();
        bg.sprite = LoadSprite("Opening_Background");
        bg.color = Color.white;
        bg.type = Image.Type.Simple;
        bg.preserveAspect = false;
        bg.raycastTarget = false;
        Stretch(bg.rectTransform);
        bg.rectTransform.localScale = Vector3.one;   // 기존 Background 는 배율 1.98 이었으므로 이미지가 확대되지 않도록 1로 되돌린다

        int index = bgTr.GetSiblingIndex();
        EnsureLayer(canvas.transform, "Player", LoadSprite("Opening_Player"), Color.white, ++index);
        EnsureLayer(canvas.transform, "BackgroundDim", null, new Color(0f, 0f, 0f, 0.30f), ++index);

        ReplaceTitleWithLogo(canvas.transform);
        foreach (var bn in new[] { "StartButton", "OptionsButton", "CreditsButton" }) { ApplyButtonPlate(canvas.transform, bn); ApplySelectionAura(canvas.transform, bn); }
        ClearFirstSelected();
        ReplaceLabelWithImage(canvas.transform, "StartLabel", "Menu_Start");
        ReplaceLabelWithImage(canvas.transform, "OptionsLabel", "Menu_Options");
        ReplaceLabelWithImage(canvas.transform, "CreditsLabel", "Menu_Credits");

        ApplyMenuScale(canvas.transform);
        BuildTitleSequence(canvas.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SetBuildSettings();
        EditorSceneManager.playModeStartScene = null;
        Debug.Log("[ApplyOpeningBackground] MainMenu 배경 적용 및 빌드 설정 완료");
    }

    /// <summary>
    /// 'Dungeon' 제목 글자는 끄고(칸은 유지), 로고는 메뉴 안이 아니라 캔버스 바로 아래 독립 오브젝트(TitleLogo)로 둔다.
    /// 오프닝 → 메뉴 전환에서 로고가 크기·위치를 바꾸며 이동해야 하므로 메뉴 배율의 영향을 받지 않게 분리한다.
    /// </summary>
    private static void ReplaceTitleWithLogo(Transform canvas)
    {
        Transform titleText = null;
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            if (t.name == "TitleText") { titleText = t; break; }
        if (titleText == null) { Debug.LogError("[ApplyOpeningBackground] TitleText 를 찾지 못했습니다."); return; }

        var tmp = titleText.GetComponent<TMPro.TMP_Text>();
        if (tmp != null) tmp.enabled = false;
        var uiText = titleText.GetComponent<Text>();
        if (uiText != null) uiText.enabled = false;

        Transform old = titleText.Find("TitleLogo");               // 이전 방식(메뉴 안 로고)이 남아 있으면 제거
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Transform logoTr = canvas.Find("TitleLogo");
        GameObject go = logoTr != null ? logoTr.gameObject : new GameObject("TitleLogo", typeof(RectTransform));
        if (logoTr == null) go.transform.SetParent(canvas, false);
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = LoadSprite("Opening_Title");
        img.preserveAspect = true;
        img.raycastTarget = false;
        var r = img.rectTransform;                                  // 앵커 좌상단, 중심 피벗 — 위치/크기는 컨트롤러가 오프닝↔메뉴 자세로 보간
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.localScale = Vector3.one;
        r.sizeDelta = new Vector2(1500f, 403f);
        r.anchoredPosition = new Vector2(960f, -433.5f);
    }

    /// <summary>버튼 라벨(텍스트)을 끄고 같은 칸에 3D 글자 이미지를 자식(Art)으로 둔다. 이미 있으면 갱신한다.</summary>
    private static void ReplaceLabelWithImage(Transform canvas, string labelName, string spriteName)
    {
        Transform label = null;
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            if (t.name == labelName) { label = t; break; }
        if (label == null) { Debug.LogError("[ApplyOpeningBackground] " + labelName + " 를 찾지 못했습니다."); return; }

        var tmp = label.GetComponent<TMPro.TMP_Text>();
        if (tmp != null) tmp.enabled = false;
        var uiText = label.GetComponent<Text>();
        if (uiText != null) uiText.enabled = false;

        Transform artTr = label.Find("Art");
        GameObject go = artTr != null ? artTr.gameObject : new GameObject("Art", typeof(RectTransform));
        if (artTr == null) go.transform.SetParent(label, false);
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = LoadSprite(spriteName);
        img.preserveAspect = true;
        img.raycastTarget = false;
        var r = img.rectTransform;                              // 버튼 칸 중앙, 높이 60(로컬)에 비율 유지
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(420f, 60f);
        r.anchoredPosition = Vector2.zero;
        r.localScale = Vector3.one;
    }

    /// <summary>버튼 배경을 타이틀 톤 판 스프라이트로 바꾸고 상태별(SpriteSwap) 스프라이트를 지정한다.</summary>
    private static void ApplyButtonPlate(Transform canvas, string buttonName)
    {
        Button button = null;
        foreach (var b in canvas.GetComponentsInChildren<Button>(true))
            if (b.name == buttonName) { button = b; break; }
        if (button == null) { Debug.LogError("[ApplyOpeningBackground] " + buttonName + " 를 찾지 못했습니다."); return; }

        var img = button.GetComponent<Image>();
        img.sprite = LoadSprite("Button_Normal");
        img.type = Image.Type.Simple;
        img.color = Color.white;                    // 기존 색 틴트(파랑/회색)를 제거해 판 그림이 그대로 보이게 한다
        button.targetGraphic = img;
        button.transition = Selectable.Transition.SpriteSwap;
        var st = button.spriteState;
        st.highlightedSprite = LoadSprite("Button_Highlight");
        st.selectedSprite = LoadSprite("Button_Highlight");
        st.pressedSprite = LoadSprite("Button_Pressed");
        st.disabledSprite = LoadSprite("Button_Pressed");
        button.spriteState = st;
        EditorUtility.SetDirty(button);
    }

    /// <summary>버튼 자식으로 금색 오라 이미지(Aura)를 만들고 MenuSelectionAura 컴포넌트에 연결한다. 이미 있으면 갱신한다.</summary>
    private static void ApplySelectionAura(Transform canvas, string buttonName)
    {
        Button button = null;
        foreach (var b in canvas.GetComponentsInChildren<Button>(true))
            if (b.name == buttonName) { button = b; break; }
        if (button == null) return;

        Transform auraTr = button.transform.Find("Aura");
        GameObject go = auraTr != null ? auraTr.gameObject : new GameObject("Aura", typeof(RectTransform));
        if (auraTr == null) go.transform.SetParent(button.transform, false);
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = LoadSprite("Menu_Aura");
        img.raycastTarget = false;                       // 클릭은 버튼 판이 받도록
        img.color = new Color(1f, 1f, 1f, 0f);           // 저장 상태는 항상 꺼진 상태(투명) — 편집 화면에서도 선택 전에는 보이지 않게
        img.enabled = false;
        var r = img.rectTransform;                       // 버튼 칸보다 가로 60, 세로 30(로컬) 더 크게(스프라이트 패딩 120x60 px 과 대응)
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(-60f, -30f);
        r.offsetMax = new Vector2(60f, 30f);
        r.localScale = Vector3.one;
        go.transform.SetAsFirstSibling();                // 글자(라벨) 뒤에 그려지게

        var comp = button.GetComponent<MenuSelectionAura>();
        if (comp == null) comp = button.gameObject.AddComponent<MenuSelectionAura>();
        var so = new SerializedObject(comp);
        so.FindProperty("aura").objectReferenceValue = img;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>EventSystem 의 시작 선택을 비운다. 오프닝 동안은 선택 커서(오라)가 없고, 메뉴 전환이 끝나면 컨트롤러가 START 를 선택한다.</summary>
    private static void ClearFirstSelected()
    {
        var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null) return;
        es.firstSelectedGameObject = null;
        EditorUtility.SetDirty(es);
    }

    private const float MenuScale = 1.386f;          // 기존 1.98 × 0.7
    private const float ButtonsCenterY = 663f;       // 기존 버튼 묶음 중심(화면 위에서 아래로 잰 y). 축소 후에도 이 지점에 둔다
    private const float LogoCenterY = 152.5f;        // 로고 중심 y(위에서 아래로)
    private static readonly Vector2 LogoSize = new Vector2(1030f, 277f);   // 로고 화면 크기(축소와 무관하게 유지)

    /// <summary>MenuLayout 을 0.7배로 줄이고 위치를 보정한 뒤, 자식인 로고는 화면 크기·위치를 그대로 되돌린다.</summary>
    private static void ApplyMenuScale(Transform canvas)
    {
        Transform menu = null;
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            if (t.name == "MenuLayout") { menu = t; break; }
        if (menu == null) { Debug.LogError("[ApplyOpeningBackground] MenuLayout 를 찾지 못했습니다."); return; }

        var rt = (RectTransform)menu;
        rt.localScale = new Vector3(MenuScale, MenuScale, 1f);
        const float oldScale = 1.98f, screenCenterY = 540f;
        float scaledCenterY = screenCenterY + (ButtonsCenterY - screenCenterY) * (MenuScale / oldScale);   // 중심(540) 기준 축소 후 버튼 묶음 중심
        rt.anchoredPosition = new Vector2(0f, -(ButtonsCenterY - scaledCenterY));                            // 화면 아래 방향으로 보정(UI y는 위가 +)
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        var logo = menu.GetComponentInChildren<Image>(true);
        foreach (var im in menu.GetComponentsInChildren<Image>(true))
            if (im.name == "TitleLogo") { logo = im; break; }
        if (logo != null && logo.name == "TitleLogo")
        {
            var lr = logo.rectTransform;
            lr.sizeDelta = LogoSize / MenuScale;                                   // 부모 배율을 상쇄해 화면 크기 1030x277 유지
            lr.position = new Vector3(960f, 1080f - LogoCenterY, 0f);              // 화면 좌표(오버레이 캔버스, 배율 1)로 직접 지정
        }
    }

    private static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    /// <summary>이름이 같은 레이어가 있으면 갱신하고 없으면 만든다. 배경 바로 위(index)에 놓는다.</summary>
    private static void EnsureLayer(Transform parent, string name, Sprite sprite, Color color, int siblingIndex)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform));
        if (t == null) go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        Stretch(img.rectTransform);
        go.transform.SetSiblingIndex(siblingIndex);
    }

    /// <summary>빌드 순서를 MainMenu(0, 오프닝 연출 포함) → 스테이지 순서로 맞춘다. 별도 Opening 씬은 목록에서 뺀다.</summary>
    private static void SetBuildSettings()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == OpeningPath || s.path == MainMenuPath);
        list.Insert(0, new EditorBuildSettingsScene(MainMenuPath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>
    /// 오프닝 연출 요소를 MainMenu 캔버스에 만든다(이미 있으면 갱신): OpeningGroup(한글 제목·가로선·부제·PRESS START),
    /// 메뉴 묶음(MenuLayout)에 CanvasGroup, 그리고 OpeningScreenController 를 연결한다. 저장 상태는 '오프닝 상태'(메뉴 숨김).
    /// </summary>
    private static void BuildTitleSequence(Transform canvas)
    {
        // 캔버스 배율을 원본 오프닝 씬과 동일하게: 기준 1920x1080, 가로·세로 반반(0.5)으로 맞춘다.
        // (이전 MainMenu 값은 세로 기준 1 이라 16:9 가 아닌 화면에서 오프닝이 원본과 다르게 보였다)
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/MalgunGothic SDF.asset");

        Transform groupTr = canvas.Find("OpeningGroup");
        GameObject group = groupTr != null ? groupTr.gameObject : new GameObject("OpeningGroup", typeof(RectTransform));
        if (groupTr == null) group.transform.SetParent(canvas, false);
        var gr = (RectTransform)group.transform;
        Stretch(gr);
        var cg = group.GetComponent<CanvasGroup>();
        if (cg == null) cg = group.AddComponent<CanvasGroup>();

        EnsureOpeningText(group.transform, "TitleKorean", "파이널 던전", 80f, 675f, font);
        EnsureOpeningRule(group.transform, "TitleRule", 724f);
        EnsureOpeningText(group.transform, "SubtitleKorean", "마왕성을 향한 마지막 여정", 56f, 775f, font);
        var press = EnsureOpeningText(group.transform, "PressStart", "PRESS START", 58f, 930f, font);

        // 그리는 순서: 배경 → 플레이어 → 어둠막 → 로고 → 오프닝 글자 → 메뉴
        Transform logo = canvas.Find("TitleLogo");
        Transform menu = null;
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            if (t.name == "MenuLayout") { menu = t; break; }
        Transform dim = canvas.Find("BackgroundDim");
        if (dim == null || logo == null || menu == null) { Debug.LogError("[ApplyOpeningBackground] 오프닝 연결에 필요한 오브젝트를 찾지 못했습니다."); return; }
        logo.SetSiblingIndex(dim.GetSiblingIndex() + 1);
        group.transform.SetSiblingIndex(logo.GetSiblingIndex() + 1);
        menu.SetSiblingIndex(group.transform.GetSiblingIndex() + 1);

        var menuGroup = menu.GetComponent<CanvasGroup>();
        if (menuGroup == null) menuGroup = menu.gameObject.AddComponent<CanvasGroup>();

        Button start = null;
        foreach (var b in menu.GetComponentsInChildren<Button>(true))
            if (b.name == "StartButton") { start = b; break; }

        var ctrl = canvas.GetComponent<OpeningScreenController>();
        if (ctrl == null) ctrl = canvas.gameObject.AddComponent<OpeningScreenController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("openingGroup").objectReferenceValue = cg;
        so.FindProperty("pressStartText").objectReferenceValue = press;
        so.FindProperty("logo").objectReferenceValue = (RectTransform)logo;
        so.FindProperty("menuGroup").objectReferenceValue = menuGroup;
        so.FindProperty("backgroundDim").objectReferenceValue = dim.GetComponent<Image>();
        so.FindProperty("startButton").objectReferenceValue = start;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 저장 상태 = 오프닝 상태
        cg.alpha = 1f; cg.interactable = false; cg.blocksRaycasts = false;
        menuGroup.alpha = 0f; menuGroup.interactable = false; menuGroup.blocksRaycasts = false;
    }

    private static TMPro.TextMeshProUGUI EnsureOpeningText(Transform parent, string name, string text, float size, float yFromTop, TMPro.TMP_FontAsset font)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform));
        if (t == null) go.transform.SetParent(parent, false);
        var tx = go.GetComponent<TMPro.TextMeshProUGUI>();
        if (tx == null) tx = go.AddComponent<TMPro.TextMeshProUGUI>();
        tx.font = font;
        tx.text = text;
        tx.fontSize = size;
        tx.fontStyle = TMPro.FontStyles.Bold;
        tx.alignment = TMPro.TextAlignmentOptions.Center;
        tx.color = Color.white;
        tx.raycastTarget = false;
        tx.outlineColor = new Color32(0, 0, 0, 255);
        tx.outlineWidth = 0.22f;
        var r = tx.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, -yFromTop);
        r.sizeDelta = new Vector2(1200f, 110f);
        return tx;
    }

    private static void EnsureOpeningRule(Transform parent, string name, float yFromTop)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform));
        if (t == null) go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, -yFromTop);
        r.sizeDelta = new Vector2(930f, 3f);
    }
}
