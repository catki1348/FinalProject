using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 게임오버 연출을 담당하는 매니저(규칙 35). 오버레이 UI를 코드로 직접 구성해
// 씬마다 별도로 UI를 만들어두지 않아도 모든 스테이지에서 동일하게 동작한다.
// 게임오버 시 "다시 시작"(현재 스테이지 리트라이) / "메인 메뉴" 선택지를 제공한다
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

    private const string MainMenuSceneName = "MainMenu";

    private GameObject overlayRoot;

    // 이 화면이 떠 있는 동안에는 일시정지(PauseManager)를 열지 않는다. 인스턴스를 새로 만들지 않고 확인한다.
    public static bool IsShowing => instance != null && instance.overlayRoot != null && instance.overlayRoot.activeSelf;
    private Button retryButton;
    // Codex (GPT-6), 2026-10-15: zone systems release temporary grants on fall/game over too.
    public event System.Action OnGameOver;

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
        OnGameOver?.Invoke();
        overlayRoot.SetActive(true);
        Time.timeScale = 0f;
        EnsureEventSystem();
        // 키보드/패드로도 고를 수 있도록 첫 버튼을 선택 상태로 둔다
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(retryButton.gameObject);
    }

    // 현재 스테이지를 처음부터 다시 시작한다
    public void Retry()
    {
        LoadAndClose(SceneManager.GetActiveScene().name);
    }

    // 메인 메뉴로 이동한다
    public void GoToMainMenu()
    {
        LoadAndClose(MainMenuSceneName);
    }

    private void LoadAndClose(string sceneName)
    {
        overlayRoot.SetActive(false);
        Time.timeScale = 1f;
        GameManager.Instance.LoadScene(sceneName);
    }

    // 스테이지 씬에 EventSystem이 없으면 버튼이 동작하지 않으므로 보장한다
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
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

        // 글자는 오프닝/게임클리어와 같은 금색 3D 블록 글자 이미지(@Edit/Ending/build_overlay_titles.py 로 생성)
        EndingUi.CreatePicture(canvasObject.transform, "GameOverTitle", EndingUi.LoadTexture("GameOver_Title"),
            1000f, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f));

        retryButton = CreateButton(canvasObject.transform, "RetryButton", "다시 시작", new Vector2(0f, -40f), Retry);
        Button menuButton = CreateButton(canvasObject.transform, "MainMenuButton", "메인 메뉴", new Vector2(0f, -130f), GoToMainMenu);

        // 위/아래 키 이동이 두 버튼 사이에서만 오가도록 명시
        var retryNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = menuButton, selectOnUp = menuButton };
        var menuNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = retryButton, selectOnUp = retryButton };
        retryButton.navigation = retryNav;
        menuButton.navigation = menuNav;

        overlayRoot = canvasObject;
        overlayRoot.SetActive(false);
    }

    private static Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, UnityAction onClick)
    {
        var buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);

        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.15f);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(320f, 70f);
        rect.anchoredPosition = anchoredPosition;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.9f, 0.4f, 1f);
        colors.selectedColor = new Color(1f, 0.9f, 0.4f, 1f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.2f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);
        var text = textObject.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 32;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }
}
