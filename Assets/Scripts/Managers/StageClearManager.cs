using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 스테이지 클리어 연출을 담당하는 매니저(규칙 39). GameOverManager와 동일하게 오버레이 UI를
// 코드로 직접 구성해 씬마다 별도로 UI를 만들어두지 않아도 모든 스테이지에서 동일하게 동작한다.
// MapEdit.md 규칙 9: 5영역 종료점 도달 시 이 클리어 화면을 먼저 보여준 뒤 다음 스테이지로 넘어간다.
public class StageClearManager : MonoBehaviour
{
    private const float DisplaySeconds = 2f;

    private static StageClearManager instance;

    public static StageClearManager Instance
    {
        get
        {
            if (instance == null)
            {
                var managerObject = new GameObject(nameof(StageClearManager));
                instance = managerObject.AddComponent<StageClearManager>();
                DontDestroyOnLoad(managerObject);
            }

            return instance;
        }
    }

    private GameObject overlayRoot;

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

    // 클리어 화면을 표시한 뒤, 정해진 시간이 지나면 onContinue를 호출한다(다음 스테이지 로드 등).
    public void TriggerStageClear(System.Action onContinue)
    {
        StartCoroutine(ShowThenContinue(onContinue));
    }

    private IEnumerator ShowThenContinue(System.Action onContinue)
    {
        overlayRoot.SetActive(true);
        yield return new WaitForSecondsRealtime(DisplaySeconds);
        overlayRoot.SetActive(false);
        onContinue?.Invoke();
    }

    private void BuildOverlay()
    {
        var canvasObject = new GameObject("StageClearCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        var backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        var backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0.85f);
        RectTransform backgroundRect = backgroundImage.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var textObject = new GameObject("StageClearText");
        textObject.transform.SetParent(canvasObject.transform, false);
        var text = textObject.AddComponent<Text>();
        text.text = "STAGE CLEAR";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        overlayRoot = canvasObject;
        overlayRoot.SetActive(false);
    }
}
