using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 클리어 화면 매니저(5-5 보스 처치 → 종료점 도달 시 표시). StageClearManager/EndingManager 와 같은 방식으로
/// 오버레이 UI 를 코드로 직접 구성하므로 씬에 따로 UI 를 만들 필요가 없다(싱글톤은 매니저 클래스에만 허용).
/// 흐름: 어두워지는 막 → "GAME CLEAR" 금색 3D 글자가 커졌다 자리잡음 → 한글 문구 → 깜빡이는 PRESS ENTER →
///       엔터(또는 일정 시간 후 자동) → EndingManager 가 엔딩 씬으로 넘긴다.
/// 화면이 떠 있는 동안 게임은 멈추므로(timeScale 0) 모든 연출은 unscaled 시간으로 진행한다.
/// </summary>
public class GameClearManager : MonoBehaviour
{
    private const float MinWaitSeconds = 1.8f;       // 이 시간이 지나야 엔터가 먹는다(전투 중 눌린 입력 방지)
    private const float AutoAdvanceSeconds = 14f;    // 입력이 없어도 이 시간 뒤에는 엔딩으로 넘어간다
    private const float DimAlpha = 0.80f;

    private static GameClearManager instance;

    public static GameClearManager Instance
    {
        get
        {
            if (instance == null)
            {
                var managerObject = new GameObject(nameof(GameClearManager));
                instance = managerObject.AddComponent<GameClearManager>();
                DontDestroyOnLoad(managerObject);
            }

            return instance;
        }
    }

    private GameObject overlayRoot;

    // 이 화면이 떠 있는 동안에는 일시정지(PauseManager)를 열지 않는다. 인스턴스를 새로 만들지 않고 확인한다.
    public static bool IsShowing => instance != null && instance.overlayRoot != null && instance.overlayRoot.activeSelf;
    private Image dim;
    private RawImage title;
    private Text subtitle;
    private Text prompt;
    private bool shown;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    /// <summary>게임 클리어 화면을 띄운다(여러 번 불려도 한 번만 실행).</summary>
    public void TriggerGameClear()
    {
        if (shown) return;
        shown = true;
        overlayRoot.SetActive(true);
        Time.timeScale = 0f;
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        EndingUi.SetAlpha(dim, 0f);
        EndingUi.SetAlpha(title, 0f);
        EndingUi.SetAlpha(subtitle, 0f);
        EndingUi.SetAlpha(prompt, 0f);

        float t = 0f;
        bool entered = false;
        while (true)
        {
            t += Time.unscaledDeltaTime;

            EndingUi.SetAlpha(dim, DimAlpha * EndingUi.Smooth(t / 0.7f));

            float titleT = EndingUi.Smooth((t - 0.35f) / 0.7f);          // 글자: 0.35초 뒤 0.7초 동안 나타나며 1.25배 → 1배로 자리잡음
            EndingUi.SetAlpha(title, titleT);
            title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, titleT);

            EndingUi.SetAlpha(subtitle, EndingUi.Smooth((t - 1.1f) / 0.8f));

            if (t > 1.6f)                                                  // PRESS ENTER: 완전 투명 ↔ 진한 상태를 부드럽게 반복
            {
                float blink = (Mathf.Sin((t - 1.6f) * 0.9f * Mathf.PI * 2f) + 1f) * 0.5f;
                EndingUi.SetAlpha(prompt, blink);
            }

            if (t >= MinWaitSeconds && EndingUi.EnterPressed()) entered = true;
            if (entered || t >= AutoAdvanceSeconds) break;
            yield return null;
        }

        EndingManager.Instance.TriggerEnding();
    }

    private void BuildOverlay()
    {
        Canvas canvas = EndingUi.CreateCanvas(transform, "GameClearCanvas", 1100);
        overlayRoot = canvas.gameObject;

        dim = EndingUi.CreateFill(canvas.transform, "Dim", new Color(0f, 0f, 0f, 0f));
        title = EndingUi.CreatePicture(canvas.transform, "GameClearTitle", EndingUi.LoadTexture("GameClear_Title"),
            1300f, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f));
        subtitle = EndingUi.CreateText(canvas.transform, "Subtitle", "마왕을 쓰러뜨리고 던전을 정복했다!", 54,
            new Color(1f, 0.93f, 0.7f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(1400f, 90f));
        prompt = EndingUi.CreateText(canvas.transform, "PressEnter", "PRESS ENTER", 46,
            new Color(1f, 1f, 1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(800f, 80f));

        overlayRoot.SetActive(false);
    }
}
