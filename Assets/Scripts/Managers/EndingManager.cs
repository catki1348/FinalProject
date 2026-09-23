using UnityEngine;
using UnityEngine.UI;

// 엔딩 연출을 담당하는 매니저(규칙 39). GameOverManager와 동일하게 오버레이 UI를 코드로
// 직접 구성한다. MapEdit.md 규칙 10: 5-5(최종 스테이지의 최종 영역)에서 종료점에 도달하면
// 다음 스테이지로 넘어가는 대신 이 엔딩 화면을 띄우고 게임을 정지한다.
public class EndingManager : MonoBehaviour
{
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

    public void TriggerEnding()
    {
        overlayRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    private void BuildOverlay()
    {
        var canvasObject = new GameObject("EndingCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        var backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        var backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = new Color(0f, 0f, 0f, 0.9f);
        RectTransform backgroundRect = backgroundImage.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var textObject = new GameObject("EndingText");
        textObject.transform.SetParent(canvasObject.transform, false);
        var text = textObject.AddComponent<Text>();
        text.text = "THE END";
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
