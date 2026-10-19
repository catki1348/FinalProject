using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 클리어 화면과 엔딩 씬이 함께 쓰는 UI 도우미(정적 유틸리티, 상태 없음).
/// 이미지는 Assets/Resources/Ending 폴더의 PNG 를 Texture2D 로 불러 RawImage 에 표시한다(스프라이트 설정이 필요 없다).
/// 글자는 윈도우 기본 한글 글꼴(맑은 고딕)을 쓰는 UI.Text + Outline 으로 그린다(씬 파일에 글꼴 참조를 저장하지 않기 위함).
/// </summary>
public static class EndingUi
{
    private static Font cachedFont;

    /// <summary>한글이 나오는 글꼴을 한 번만 만들어 재사용한다. OS 에 없으면 기본 글꼴로 대체한다.</summary>
    public static Font GetFont()
    {
        if (cachedFont != null) return cachedFont;
        cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 48);
        if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }

    /// <summary>Resources/Ending 의 이미지를 불러온다(없으면 null).</summary>
    public static Texture2D LoadTexture(string name)
    {
        return Resources.Load<Texture2D>("Ending/" + name);
    }

    /// <summary>화면 전체를 덮는 오버레이 캔버스를 만든다(기준 해상도 1920x1080).</summary>
    public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    /// <summary>부모 전체를 채우는 단색 이미지(어둠막, 페이드 화면 등).</summary>
    public static Image CreateFill(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        Stretch(img.rectTransform);
        return img;
    }

    /// <summary>부모 전체를 채우는 그림(배경 등). 가로세로 비율은 호출하는 쪽이 1920x1080 비율의 그림을 쓴다고 가정한다.</summary>
    public static RawImage CreateStretchedPicture(Transform parent, string name, Texture2D texture)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var raw = go.AddComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;
        Stretch(raw.rectTransform);
        return raw;
    }

    /// <summary>가로 폭(width)에 맞춰 비율을 유지하는 그림을 만든다. anchor 는 화면 기준 정규화 위치(0~1), 위치 보정(offset)은 픽셀.</summary>
    public static RawImage CreatePicture(Transform parent, string name, Texture2D texture, float width, Vector2 anchor, Vector2 offset)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var raw = go.AddComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;
        float height = texture != null ? width * texture.height / texture.width : width * 0.25f;
        var r = raw.rectTransform;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(width, height);
        r.anchoredPosition = offset;
        return raw;
    }

    /// <summary>가운데 정렬 글자(검은 외곽선 포함)를 만든다.</summary>
    public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = GetFont();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);
        var r = text.rectTransform;
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = offset;
        return text;
    }

    public static void SetAlpha(Graphic g, float alpha)
    {
        if (g == null) return;
        Color c = g.color;
        c.a = alpha;
        g.color = c;
    }

    public static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    /// <summary>엔터(메인 또는 키패드)가 이번 프레임에 눌렸는지.</summary>
    public static bool EnterPressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
