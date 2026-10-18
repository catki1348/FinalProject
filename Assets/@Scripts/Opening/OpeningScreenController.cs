using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 오프닝 + 메인 메뉴를 한 씬(MainMenu)에서 이어서 보여주는 컨트롤러.
/// 1) 오프닝 상태: 큰 타이틀 로고 + 한글 제목/부제 + 깜빡이는 PRESS START. 메뉴 버튼은 숨겨지고 클릭도 막힌다.
/// 2) 엔터 키(Return/Keypad Enter) → 같은 화면에서 전환: 로고가 메뉴 위치로 부드럽게 올라가며 작아지고,
///    오프닝 글자는 사라지고, 배경 어둠막과 메뉴 버튼이 서서히 나타난다(배경과 기사 그림은 그대로라 끊김이 없다).
/// 3) 메뉴 상태: 버튼이 활성화되고 START 가 선택된다.
/// 한 번 오프닝을 본 뒤 같은 실행 중에 이 씬으로 다시 돌아오면 오프닝을 건너뛰고 바로 메뉴를 보여준다.
/// 참조는 모두 Inspector 연결(GameObject.Find 금지 규칙).
/// </summary>
public class OpeningScreenController : MonoBehaviour
{
    [Header("오프닝 요소")]
    [SerializeField] private CanvasGroup openingGroup;   // 한글 제목/가로선/부제/PRESS START 묶음
    [SerializeField] private TMP_Text pressStartText;    // 깜빡일 PRESS START

    [Header("로고 (오프닝 자세 → 메뉴 자세로 이동)")]
    [SerializeField] private RectTransform logo;
    [SerializeField] private Vector2 logoOpeningPos = new Vector2(960f, -433.5f);   // 앵커(좌상단) 기준 중심 위치
    [SerializeField] private Vector2 logoOpeningSize = new Vector2(1500f, 403f);
    [SerializeField] private Vector2 logoMenuPos = new Vector2(960f, -152.5f);
    [SerializeField] private Vector2 logoMenuSize = new Vector2(1030f, 277f);

    [Header("메뉴 요소")]
    [SerializeField] private CanvasGroup menuGroup;      // 메뉴 버튼 묶음(MenuLayout)
    [SerializeField] private Image backgroundDim;        // 메뉴에서만 쓰는 어둠막(검은 30%)
    [SerializeField] private Button startButton;         // 전환이 끝나면 선택해 줄 버튼

    [Header("동작 설정")]
    [SerializeField] private float blinkSpeed = 0.9f;    // PRESS START 깜빡임 속도(초당 주기)
    [SerializeField] private float transitionSeconds = 1.1f;   // 전체 전환 시간
    [SerializeField] private float inputDelaySeconds = 0.4f;   // 시작 직후 오입력 방지

    private enum State { Opening, Transition, Menu }

    private static bool hasShownOpening;   // 이번 실행에서 오프닝을 이미 보았는지(재진입 시 건너뜀)

    private State state = State.Opening;
    private float elapsed;
    private float dimTargetAlpha;

    private void Awake()
    {
        dimTargetAlpha = backgroundDim != null ? backgroundDim.color.a : 0.3f;
        if (hasShownOpening) ApplyMenuState();
        else ApplyOpeningState();
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (state == State.Opening)
        {
            BlinkPressStart();
            if (elapsed >= inputDelaySeconds && EnterPressed()) StartCoroutine(PlayTransition());
        }
    }

    /// <summary>엔터(메인 또는 키패드)가 이번 프레임에 눌렸는지.</summary>
    private static bool EnterPressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    /// <summary>PRESS START 알파를 완전 투명(0) ~ 진한 상태(1) 사이에서 사인 곡선으로 반복시킨다.</summary>
    private void BlinkPressStart()
    {
        if (pressStartText == null) return;
        float t = (Mathf.Sin(elapsed * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        Color c = pressStartText.color;
        c.a = t;
        pressStartText.color = c;
    }

    /// <summary>오프닝 상태: 로고 크게, 오프닝 글자 보임, 메뉴 숨김·잠금, 선택 커서 없음.</summary>
    private void ApplyOpeningState()
    {
        state = State.Opening;
        SetLogo(0f);
        SetGroup(openingGroup, 1f, false);
        SetGroup(menuGroup, 0f, false);
        SetDim(0f);
        ClearSelection();
    }

    /// <summary>메뉴 상태: 로고 작게, 오프닝 글자 숨김, 메뉴 보임·활성, START 선택.</summary>
    private void ApplyMenuState()
    {
        state = State.Menu;
        SetLogo(1f);
        SetGroup(openingGroup, 0f, false);
        SetGroup(menuGroup, 1f, true);
        SetDim(1f);
        StartCoroutine(SelectStartNextFrame());
    }

    /// <summary>오프닝 → 메뉴 전환 연출. 단계마다 구간을 나눠 겹쳐 진행한다.</summary>
    private IEnumerator PlayTransition()
    {
        state = State.Transition;
        hasShownOpening = true;
        float t = 0f;
        while (t < transitionSeconds)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / transitionSeconds);
            SetLogo(Smooth(u));                                        // 로고 이동·축소: 전체 구간
            SetGroup(openingGroup, 1f - Smooth(Mathf.Clamp01(u / 0.45f)), false);          // 오프닝 글자: 앞 45% 에서 사라짐
            SetGroup(menuGroup, Smooth(Mathf.Clamp01((u - 0.4f) / 0.6f)), false);          // 메뉴: 40% 이후 서서히 나타남
            SetDim(Smooth(Mathf.Clamp01((u - 0.2f) / 0.8f)));
            yield return null;
        }
        ApplyMenuState();
    }

    private static float Smooth(float x) { return x * x * (3f - 2f * x); }

    /// <summary>k=0 오프닝 자세, k=1 메뉴 자세로 로고 위치·크기를 보간한다.</summary>
    private void SetLogo(float k)
    {
        if (logo == null) return;
        logo.anchoredPosition = Vector2.Lerp(logoOpeningPos, logoMenuPos, k);
        logo.sizeDelta = Vector2.Lerp(logoOpeningSize, logoMenuSize, k);
    }

    private static void SetGroup(CanvasGroup g, float alpha, bool interactive)
    {
        if (g == null) return;
        g.alpha = alpha;
        g.interactable = interactive;
        g.blocksRaycasts = interactive;
    }

    private void SetDim(float k)
    {
        if (backgroundDim == null) return;
        Color c = backgroundDim.color;
        c.a = dimTargetAlpha * k;
        backgroundDim.color = c;
    }

    private static void ClearSelection()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    /// <summary>활성화가 반영된 다음 프레임에 START 를 선택한다(오라가 켜지도록).</summary>
    private IEnumerator SelectStartNextFrame()
    {
        yield return null;
        if (startButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }
}
