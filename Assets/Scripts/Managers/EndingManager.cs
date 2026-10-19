using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 엔딩 연출로 넘어가는 전환을 담당하는 매니저(규칙 35). MapEdit.md 규칙 10: 5-5(최종 스테이지의 최종 영역)에서
// 종료점에 도달하면 다음 스테이지로 넘어가는 대신 게임 클리어 화면(GameClearManager) → 엔딩 씬(Ending) 순서로 진행한다.
// 이 매니저는 게임 클리어 화면 이후의 "검게 페이드 아웃 → 엔딩 씬 로드" 만 맡는다.
// (예전에는 이 클래스가 "THE END" 글자만 띄우고 게임을 멈췄다. 이제 글자·연출은 엔딩 씬이 담당한다.)
public class EndingManager : MonoBehaviour
{
    public const string EndingSceneName = "Ending";    // 빌드 설정에 등록된 엔딩 씬 이름
    private const float FadeOutSeconds = 1.0f;

    private static EndingManager instance;

    public static EndingManager Instance
    {
        get
        {
            if (instance == null)
            {
                var managerObject = new GameObject(nameof(EndingManager));
                instance = managerObject.AddComponent<EndingManager>();
                DontDestroyOnLoad(managerObject);
            }

            return instance;
        }
    }

    private GameObject overlayRoot;
    private Image fade;
    private bool transitioning;

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

    // 엔딩 씬으로 넘어간다(여러 번 불려도 한 번만 실행). 게임 클리어 화면이 timeScale 을 0 으로 멈춰 두므로
    // 모든 연출은 unscaled 시간으로 진행하고, 씬을 불러오기 직전에 1 로 되돌린다.
    public void TriggerEnding()
    {
        if (transitioning) return;
        transitioning = true;
        StartCoroutine(FadeAndLoad());
    }

    private IEnumerator FadeAndLoad()
    {
        overlayRoot.SetActive(true);
        float t = 0f;
        while (t < FadeOutSeconds)
        {
            t += Time.unscaledDeltaTime;
            EndingUi.SetAlpha(fade, Mathf.Clamp01(t / FadeOutSeconds));
            yield return null;
        }

        Time.timeScale = 1f;
        GameManager.Instance.LoadScene(EndingSceneName);

        // 엔딩 씬은 스스로 검은 화면에서 시작해 밝아지므로, 씬이 올라온 뒤 이 검은 막은 치운다.
        yield return null;
        yield return null;
        overlayRoot.SetActive(false);
        transitioning = false;
    }

    private void BuildOverlay()
    {
        Canvas canvas = EndingUi.CreateCanvas(transform, "EndingFadeCanvas", 1200);
        fade = EndingUi.CreateFill(canvas.transform, "Fade", new Color(0f, 0f, 0f, 0f));
        overlayRoot = canvas.gameObject;
        overlayRoot.SetActive(false);
    }
}
