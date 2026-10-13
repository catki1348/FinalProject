using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 오프닝(타이틀) 화면 컨트롤러.
/// - "PRESS START" 문구를 부드럽게 깜빡이게 한다.
/// - 엔터 키(Return/Keypad Enter)를 누르면 1스테이지 1영역(Stage1)에서 게임을 시작한다.
/// 레퍼런스는 모두 Inspector에서 연결한다(GameObject.Find 금지 규칙).
/// </summary>
public class OpeningScreenController : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private TMP_Text pressStartText;   // 깜빡일 "PRESS START" 문구
    [SerializeField] private CanvasGroup fadeGroup;     // 화면 전환용 검은 페이드 (알파 0 → 1)

    [Header("동작 설정")]
    [SerializeField] private string nextSceneName = "Stage1";     // 시작 입력 후 이동할 씬(Stage1 = 1-1 영역에서 시작)
    [SerializeField] private float blinkSpeed = 0.9f;             // 깜빡임 속도(초당 주기 수)
    [SerializeField] private float minAlpha = 0f;                 // 깜빡일 때 가장 투명한 알파(0 = 완전 투명)
    [SerializeField] private float fadeOutSeconds = 0.6f;         // 페이드 아웃 시간
    [SerializeField] private float inputDelaySeconds = 0.4f;      // 씬 시작 직후 오입력 방지 시간

    private bool isStarting;      // 이미 전환 중인지
    private float elapsed;        // 씬 시작 후 경과 시간

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        BlinkPressStart();

        if (isStarting || elapsed < inputDelaySeconds) return;
        if (AnyStartInput()) StartCoroutine(FadeAndLoad());
    }

    /// <summary>사인 곡선으로 PRESS START 알파를 완전 투명(minAlpha=0) ~ 진한 상태(1) 사이에서 반복시킨다.</summary>
    private void BlinkPressStart()
    {
        if (pressStartText == null) return;
        float t = (Mathf.Sin(elapsed * blinkSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        Color c = pressStartText.color;
        c.a = Mathf.Lerp(minAlpha, 1f, t);
        pressStartText.color = c;
    }

    /// <summary>엔터 키(메인 Enter 또는 키패드 Enter)가 이번 프레임에 눌렸는지 확인한다.</summary>
    private static bool AnyStartInput()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    /// <summary>검은 페이드 아웃 후 다음 씬을 로드한다.</summary>
    private IEnumerator FadeAndLoad()
    {
        isStarting = true;
        float t = 0f;
        while (t < fadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            if (fadeGroup != null) fadeGroup.alpha = Mathf.Clamp01(t / fadeOutSeconds);
            yield return null;
        }
        GameManager.Instance.LoadScene(nextSceneName);   // 기존 시작 버튼과 같은 씬 전환 경로
    }
}
