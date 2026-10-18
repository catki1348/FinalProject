using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 메인메뉴 옵션 패널: 전체 스테이지 공통 배경음(BGM)/효과음(SFX) 볼륨 슬라이더를 제공한다(환경음은 BGM 슬라이더에 함께 묶여 조절됨).
// GameOverManager와 같은 방식으로 UI를 코드로 직접 구성하므로 씬에 UI 오브젝트를 따로 만들어둘 필요가 없다.
// 볼륨 값의 저장·적용은 전부 SoundManager가 담당하고(규칙 35), 이 스크립트는 슬라이더 표시와 조작만 맡는다(단일 책임).
// Claude Code (Sonnet 5.5), 2026-10-18: 메인메뉴 옵션에 BGM/SFX 볼륨 조절 추가.
public class OptionsMenuUI : MonoBehaviour
{
    // 패널 색상(메인메뉴의 어두운 배경 + 금색 포인트에 맞춤)
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.7f);
    private static readonly Color WindowColor = new Color(0.07f, 0.07f, 0.09f, 0.97f);
    private static readonly Color GoldColor = new Color(0.95f, 0.78f, 0.32f, 1f);
    private static readonly Color TrackColor = new Color(0.22f, 0.22f, 0.26f, 1f);

    private GameObject panelRoot;       // 패널 전체(열고 닫을 때 이 오브젝트를 켜고 끔)
    private Slider bgmSlider;
    private Slider sfxSlider;
    private Text bgmValueText;          // 슬라이더 옆의 "50%" 표시
    private Text sfxValueText;
    private AudioClip sfxPreviewClip;   // 효과음 슬라이더를 놓았을 때 크기를 확인시켜 주는 미리듣기 클립(비워두면 소리 없음)

    // 옵션 패널을 만들어 돌려준다. 메인메뉴 씬이 사라질 때 함께 사라지도록 별도 루트 오브젝트로 생성한다.
    public static OptionsMenuUI Create(AudioClip previewClip)
    {
        var rootObject = new GameObject(nameof(OptionsMenuUI));
        var menu = rootObject.AddComponent<OptionsMenuUI>();
        menu.sfxPreviewClip = previewClip;
        menu.BuildPanel();
        return menu;
    }

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    // 옵션 버튼을 눌렀을 때: 슬라이더를 현재 볼륨에 맞춰 표시하고 패널을 연다
    public void Open()
    {
        SoundManager sound = SoundManager.Instance;
        bgmSlider.SetValueWithoutNotify(sound.BgmVolume);
        sfxSlider.SetValueWithoutNotify(sound.SfxVolume);
        RefreshValueTexts();
        panelRoot.SetActive(true);
    }

    // 닫을 때 현재 볼륨을 디스크에 저장해 다음 실행에도 유지한다
    public void Close()
    {
        SoundManager.Instance.SaveVolumeSettings();
        panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void OnBgmChanged(float value)
    {
        SoundManager.Instance.SetBgmVolume(value);
        RefreshValueTexts();
    }

    private void OnSfxChanged(float value)
    {
        SoundManager.Instance.SetSfxVolume(value);
        RefreshValueTexts();
    }

    // 슬라이더에서 손을 뗄 때: 값 저장 + 효과음 슬라이더라면 미리듣기로 현재 크기를 들려준다
    private void OnSliderReleased(bool previewSfx)
    {
        SoundManager.Instance.SaveVolumeSettings();
        if (previewSfx) SoundManager.Instance.PlaySfx(sfxPreviewClip);
    }

    private void RefreshValueTexts()
    {
        bgmValueText.text = Mathf.RoundToInt(bgmSlider.value * 100f) + "%";
        sfxValueText.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
    }

    // ───────────── UI 구성 ─────────────

    private void BuildPanel()
    {
        var canvasObject = new GameObject("OptionsCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500; // 메인메뉴 UI 위에 표시
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        panelRoot = canvasObject;

        // 화면 전체를 덮는 어두운 막(뒤쪽 메뉴 버튼이 눌리지 않도록 클릭도 막는다)
        Image dim = CreateImage("Dim", canvasObject.transform, DimColor);
        Stretch(dim.rectTransform);

        Image window = CreateImage("Window", canvasObject.transform, WindowColor);
        SetRect(window.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 560f));
        var outline = window.gameObject.AddComponent<Outline>();
        outline.effectColor = GoldColor;
        outline.effectDistance = new Vector2(3f, -3f);

        CreateText("Title", window.transform, "OPTIONS", 60, GoldColor, TextAnchor.MiddleCenter,
            new Vector2(0f, 205f), new Vector2(600f, 90f));

        bgmSlider = CreateVolumeRow(window.transform, "BGM", 70f, false, OnBgmChanged, out bgmValueText);
        sfxSlider = CreateVolumeRow(window.transform, "SFX", -50f, true, OnSfxChanged, out sfxValueText);

        Button closeButton = CreateButton("CloseButton", window.transform, "CLOSE", new Vector2(0f, -200f), new Vector2(260f, 80f));
        closeButton.onClick.AddListener(Close);

        panelRoot.SetActive(false);
    }

    // "라벨 / 슬라이더 / 퍼센트" 한 줄을 만든다
    private Slider CreateVolumeRow(Transform parent, string label, float y, bool previewOnRelease,
        UnityAction<float> onChanged, out Text valueText)
    {
        CreateText(label + "Label", parent, label, 44, Color.white, TextAnchor.MiddleLeft,
            new Vector2(-330f, y), new Vector2(160f, 70f));

        Slider slider = CreateSlider(label + "Slider", parent, new Vector2(40f, y), new Vector2(440f, 40f));
        slider.onValueChanged.AddListener(onChanged);

        // 슬라이더를 놓는 순간 저장(+효과음은 미리듣기). Slider 컴포넌트에는 PointerUp 이벤트가 없어 EventTrigger를 쓴다
        var trigger = slider.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entry.callback.AddListener(_ => OnSliderReleased(previewOnRelease));
        trigger.triggers.Add(entry);

        valueText = CreateText(label + "Value", parent, "0%", 40, GoldColor, TextAnchor.MiddleRight,
            new Vector2(340f, y), new Vector2(150f, 70f));
        return slider;
    }

    // 스프라이트 없이 단색 Image만으로 슬라이더(배경/채움/손잡이)를 조립한다
    private Slider CreateSlider(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        var sliderObject = new GameObject(name, typeof(RectTransform));
        sliderObject.transform.SetParent(parent, false);
        SetRect(sliderObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), anchoredPosition, size);

        Image track = CreateImage("Background", sliderObject.transform, TrackColor);
        track.rectTransform.anchorMin = new Vector2(0f, 0.3f);
        track.rectTransform.anchorMax = new Vector2(1f, 0.7f);
        track.rectTransform.offsetMin = Vector2.zero;
        track.rectTransform.offsetMax = Vector2.zero;

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.3f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.7f);
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        Image fill = CreateImage("Fill", fillArea.transform, GoldColor);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        var handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(14f, 0f);
        handleAreaRect.offsetMax = new Vector2(-14f, 0f);

        Image handle = CreateImage("Handle", handleArea.transform, Color.white);
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(28f, 0f);

        var slider = sliderObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        return slider;
    }

    private Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        Image background = CreateImage(name, parent, new Color(0.18f, 0.15f, 0.08f, 1f));
        SetRect(background.rectTransform, new Vector2(0.5f, 0.5f), anchoredPosition, size);
        var outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = GoldColor;
        outline.effectDistance = new Vector2(2f, -2f);

        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        Text text = CreateText("Label", background.transform, label, 40, GoldColor, TextAnchor.MiddleCenter, Vector2.zero, size);
        text.raycastTarget = false; // 글자가 클릭을 가로채지 않게 함
        return button;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string content, int fontSize, Color color,
        TextAnchor alignment, Vector2 anchoredPosition, Vector2 size)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        SetRect(textObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), anchoredPosition, size);

        var text = textObject.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
