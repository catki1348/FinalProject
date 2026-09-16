using UnityEngine;
using UnityEngine.UI;

// 게임오버 연출을 담당하는 매니저(규칙 39). 오버레이 UI를 코드로 직접 구성해
// 씬마다 별도로 UI를 만들어두지 않아도 모든 스테이지에서 동일하게 동작한다
public class GameOverManager : MonoBehaviour
{
    private static GameOverManager instance;

    public static GameOverManager Instance
    {
        get
        {
            if (instance == null)
            {
                var managerObject = new GameObject(nameof(GameOverManager));
                instance = managerObject.AddComponent<GameOverManager>();
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

    public void TriggerGameOver()
    {
        overlayRoot.SetActive(true);
        Time.timeScale = 0f;
    }

    private void BuildOverlay()
    {
        var canvasObject = new GameObject("GameOverCanvas");
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

        var textObject = new GameObject("GameOverText");
        textObject.transform.SetParent(canvasObject.transform, false);
        var text = textObject.AddComponent<Text>();
        text.text = "GAME OVER";
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
