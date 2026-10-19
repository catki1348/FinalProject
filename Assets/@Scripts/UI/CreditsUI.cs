using UnityEngine;
using UnityEngine.UI;

// 메인메뉴 크레딧 화면: 검은 화면에 흰 글자가 아래에서 위로 올라가는 영화 크레딧 연출.
// OptionsMenuUI와 같은 방식으로 UI를 코드로 직접 구성하므로 씬에 UI 오브젝트를 따로 만들어둘 필요가 없다.
// 끝까지 올라가면 자동으로 닫히고, 아무 키/클릭/Esc로 건너뛸 수 있다.
public class CreditsUI : MonoBehaviour
{
    private const float ScrollSpeed = 110f;        // 초당 이동량(기준 해상도 1080 높이 기준 픽셀)
    private const float FastMultiplier = 3f;     // 스페이스를 누르는 동안의 스크롤/BGM 배속
    private const float SpeedChangeRate = 8f;     // 배속이 바뀔 때 초당 변화량(부드럽게 가속/감속)
    private const float FadeOutDistance = 450f;  // 크레딧 끝까지 남은 스크롤 거리가 이 값 이하가 되면 BGM이 서서히 줄어든다
    private const float HeadingSize = 40f;
    private const float NameSize = 64f;
    private const float HeadingToName = 30f;       // 항목 제목과 이름 사이 간격
    private const float GroupGap = 150f;           // 항목 사이 간격
    private const float ReferenceHeight = 1080f;

    private static readonly Color HeadingColor = new Color(1f, 1f, 1f, 0.6f);

    // 제목, 이름 순서(위에서 아래)
    private static readonly string[][] Groups =
    {
        new[] { "제작자", "LHS" },
        new[] { "프로그래밍", "GPT, CLAUDE" },
        new[] { "음악", "VARCO SOUND" },
        new[] { "아트 디자인", "VARCO 3D" },
        new[] { "최종 감수", "LHS" },
    };

    private GameObject canvasRoot;
    private RectTransform scrollRoot;
    private float contentHeight;
    private Font font;
    private int openedFrame = -1;
    private float speedMultiplier = 1f;
    private AudioClip creditsBgm;  // 크레딧 동안 재생할 BGM(비어 있으면 현재 곡 유지)
    private AudioClip returnBgm;   // 크레딧이 끝나면 되돌릴 곡(타이틀 BGM)

    public static CreditsUI Create(AudioClip creditsBgm, AudioClip returnBgm)
    {
        var rootObject = new GameObject(nameof(CreditsUI));
        var credits = rootObject.AddComponent<CreditsUI>();
        credits.creditsBgm = creditsBgm;
        credits.returnBgm = returnBgm;
        credits.BuildCanvas();
        return credits;
    }

    // 크레딧이 닫힐 때(끝까지 올라갔거나 건너뛰기) 한 번 알린다. 엔딩 씬이 크레딧이 끝난 뒤 메인 메뉴로 돌아가는 데 사용한다.
    public event System.Action Closed;

    public bool IsOpen => canvasRoot != null && canvasRoot.activeSelf;

    public void Open()
    {
        // 스페이스로 선택된 버튼이 다시 눌려 크레딧이 처음부터 재시작되는 것을 막는다
        if (IsOpen) return;

        speedMultiplier = 1f;
        // 콘텐츠 윗면이 화면 아래 끝에 오도록 시작한다(음수 = 화면 아래)
        scrollRoot.anchoredPosition = new Vector2(0f, -ReferenceHeight);
        openedFrame = Time.frameCount;
        canvasRoot.SetActive(true);
        SoundManager.Instance.SetBgmFade(1f);
        SoundManager.Instance.PlayBgm(creditsBgm);
    }

    public void Close()
    {
        canvasRoot.SetActive(false);
        speedMultiplier = 1f;
        SoundManager.Instance.SetBgmPitch(1f);
        SoundManager.Instance.SetBgmFade(1f);
        // 크레딧 곡을 재생했다면 타이틀 BGM으로 복귀
        if (creditsBgm != null) SoundManager.Instance.PlayBgm(returnBgm);
        Closed?.Invoke();
    }

    private void Update()
    {
        if (!IsOpen) return;

        // 열기를 누른 같은 프레임의 키 입력(Enter/Space 등)으로 곧바로 닫히지 않게 한다
        // 스페이스는 건너뛰기가 아니라 빨리 감기 키이므로 제외
        if (Input.anyKeyDown && !Input.GetKeyDown(KeyCode.Space) && Time.frameCount != openedFrame)
        {
            Close();
            return;
        }

        // 스페이스를 누르는 동안 빨리 올라가고, BGM도 같은 배속으로 재생한다(크레딧 곡이 있을 때만)
        float target = Input.GetKey(KeyCode.Space) ? FastMultiplier : 1f;
        speedMultiplier = Mathf.MoveTowards(speedMultiplier, target, SpeedChangeRate * Time.unscaledDeltaTime);
        if (creditsBgm != null) SoundManager.Instance.SetBgmPitch(speedMultiplier);

        scrollRoot.anchoredPosition += Vector2.up * (ScrollSpeed * speedMultiplier * Time.unscaledDeltaTime);

        // 끝이 가까워지면 BGM을 페이드아웃(남은 스크롤 거리 기준이라 빨리 감기 중에도 끝에서 자연스럽게 사라진다)
        if (creditsBgm != null)
            SoundManager.Instance.SetBgmFade(Mathf.Clamp01((contentHeight - scrollRoot.anchoredPosition.y) / FadeOutDistance));

        // 마지막 줄까지 화면 위로 완전히 사라지면 종료
        if (scrollRoot.anchoredPosition.y > contentHeight) Close();
    }

    private void BuildCanvas()
    {
        font = LoadFont();

        canvasRoot = new GameObject("CreditsCanvas");
        canvasRoot.transform.SetParent(transform, false);
        var canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600; // 옵션 패널(500)보다 위
        var scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, ReferenceHeight);
        scaler.matchWidthOrHeight = 1f; // 높이 기준 고정 → 스크롤 거리 계산이 해상도와 무관
        canvasRoot.AddComponent<GraphicRaycaster>();

        // 검은 배경(뒤쪽 메뉴 클릭도 막는다)
        var background = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
        background.transform.SetParent(canvasRoot.transform, false);
        background.color = Color.black;
        Stretch(background.rectTransform);

        // 위쪽 중앙을 기준점으로 한 스크롤 컨테이너. 자식은 y를 아래로(음수) 쌓는다
        scrollRoot = new GameObject("Scroll", typeof(RectTransform)).GetComponent<RectTransform>();
        scrollRoot.SetParent(canvasRoot.transform, false);
        scrollRoot.anchorMin = scrollRoot.anchorMax = new Vector2(0.5f, 1f);
        scrollRoot.pivot = new Vector2(0.5f, 1f);
        scrollRoot.sizeDelta = new Vector2(1200f, 0f);

        float y = 0f;
        foreach (string[] group in Groups)
        {
            CreateLine(group[0], HeadingSize, HeadingColor, ref y);
            y -= HeadingToName;
            CreateLine(group[1], NameSize, Color.white, ref y);
            y -= GroupGap;
        }
        contentHeight = -y;
        scrollRoot.sizeDelta = new Vector2(1200f, contentHeight);

        canvasRoot.SetActive(false);
    }

    private void CreateLine(string content, float fontSize, Color color, ref float y)
    {
        float height = fontSize * 1.4f;
        var textObject = new GameObject("Line", typeof(RectTransform));
        var rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(scrollRoot, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(1200f, height);
        rect.anchoredPosition = new Vector2(0f, y);

        var text = textObject.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = Mathf.RoundToInt(fontSize);
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        y -= height;
    }

    // 한글 표시를 위해 OS 폰트(맑은 고딕)를 우선 사용하고, 없으면 기본 폰트로 대체한다
    private static Font LoadFont()
    {
        Font osFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 64);
        return osFont != null ? osFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
