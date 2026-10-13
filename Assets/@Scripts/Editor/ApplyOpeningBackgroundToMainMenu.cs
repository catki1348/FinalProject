using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴(Assets/Scenes/MainMenu.unity)에 오프닝 배경을 적용하는 에디터 도구.
/// 메뉴: Tools/Apply Opening Background To MainMenu
/// - 기존 단색 Background 이미지에 Opening_Background(타이틀 글자·PRESS START가 없는 풍경 이미지)를 넣는다.
/// - 그 위에 Opening_Player(절벽 위 기사)와 글씨가 잘 보이도록 약한 어두운 막(BackgroundDim)을 쌓는다.
/// - 게임 시작 시 메인 메뉴가 첫 화면이 되도록 빌드 설정 맨 앞에 MainMenu를 두고 Opening 씬은 제외한다.
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
        SetFirstSelected(canvas.transform, "StartButton");
        ReplaceLabelWithImage(canvas.transform, "StartLabel", "Menu_Start");
        ReplaceLabelWithImage(canvas.transform, "OptionsLabel", "Menu_Options");
        ReplaceLabelWithImage(canvas.transform, "CreditsLabel", "Menu_Credits");

        ApplyMenuScale(canvas.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SetBuildSettings();
        EditorSceneManager.playModeStartScene = null;
        Debug.Log("[ApplyOpeningBackground] MainMenu 배경 적용 및 빌드 설정 완료");
    }

    /// <summary>MenuLayout 안의 TitleText 를 끄고, 같은 칸 안에 로고 이미지(TitleLogo)를 자식으로 둔다. 이미 있으면 갱신한다.</summary>
    private static void ReplaceTitleWithLogo(Transform canvas)
    {
        Transform titleText = null;
        foreach (var t in canvas.GetComponentsInChildren<Transform>(true))
            if (t.name == "TitleText") { titleText = t; break; }
        if (titleText == null) { Debug.LogError("[ApplyOpeningBackground] TitleText 를 찾지 못했습니다."); return; }

        var tmp = titleText.GetComponent<TMPro.TMP_Text>();
        if (tmp != null) tmp.enabled = false;                  // 칸(레이아웃 슬롯)은 그대로 두고 글자만 끈다
        var uiText = titleText.GetComponent<Text>();
        if (uiText != null) uiText.enabled = false;

        Transform logoTr = titleText.Find("TitleLogo");
        GameObject go = logoTr != null ? logoTr.gameObject : new GameObject("TitleLogo", typeof(RectTransform));
        if (logoTr == null) go.transform.SetParent(titleText, false);
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = LoadSprite("Opening_Title");
        img.preserveAspect = true;
        img.raycastTarget = false;
        var r = img.rectTransform;                              // 부모 칸 중앙 기준, 로고 비율(1500:403) 유지, 버튼과 겹치지 않도록 약간 위로
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(520f, 140f);
        r.anchoredPosition = new Vector2(0f, 12f);
        r.localScale = Vector3.one;
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

    /// <summary>EventSystem 의 시작 선택 버튼을 지정한다(시작하자마자 START 에 오라가 켜짐).</summary>
    private static void SetFirstSelected(Transform canvas, string buttonName)
    {
        Button target = null;
        foreach (var b in canvas.GetComponentsInChildren<Button>(true))
            if (b.name == buttonName) { target = b; break; }
        var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null || target == null) return;
        es.firstSelectedGameObject = target.gameObject;
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

    /// <summary>MainMenu 를 빌드 설정 맨 앞으로 옮기고 Opening 씬은 목록에서 제거한다.</summary>
    private static void SetBuildSettings()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == OpeningPath || s.path == MainMenuPath);
        list.Insert(0, new EditorBuildSettingsScene(MainMenuPath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
