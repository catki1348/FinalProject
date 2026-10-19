using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 게임 중 ESC로 여는 일시정지 메뉴(규칙 35). GameOverManager처럼 오버레이 UI를 코드로 구성하고
// 첫 씬 로드 때 스스로 생성(DontDestroyOnLoad)되므로 씬/프리팹 작업이 필요 없다.
// 메뉴: 게임 계속하기 / 영역 다시 하기(StageManager.RestartCurrentArea) / 게임 종료하기.
// 스테이지 씬(StageManager가 있는 씬)에서만 열린다. 게임오버/클리어 화면 등 다른 오버레이로 이미 멈춰 있을 때(timeScale 0)는 열지 않는다.
public class PauseManager : MonoBehaviour
{
    private static PauseManager instance;

    private GameObject overlayRoot;
    private Button resumeButton;
    private bool paused;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var managerObject = new GameObject(nameof(PauseManager));
        instance = managerObject.AddComponent<PauseManager>();
    }

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

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (paused) Resume();
        else if (CanPause()) Pause();
    }

    // 스테이지 플레이 중에만 연다: 스테이지 씬(StageManager 존재)이어야 하고, 게임오버/스테이지 클리어/게임 클리어 화면이 떠 있거나 이미 멈춰 있으면 열지 않는다.
    // 엔딩 씬·메인 메뉴·오프닝에는 StageManager가 없어 열리지 않는다.
    private static bool CanPause()
    {
        if (StageManager.Instance == null || Time.timeScale <= 0f) return false;
        return !GameOverManager.IsShowing && !StageClearManager.IsShowing && !GameClearManager.IsShowing;
    }

    private void Pause()
    {
        paused = true;
        overlayRoot.SetActive(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;
        EnsureEventSystem();
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    public void Resume()
    {
        paused = false;
        overlayRoot.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void RestartArea()
    {
        Resume();
        StageManager.Instance.RestartCurrentArea();
    }

    private void QuitGame()
    {
        Resume();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 스테이지 씬에 EventSystem이 없으면 버튼이 동작하지 않으므로 보장한다
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    // 메인 메뉴와 같은 디자인: 어둠막 위에 PAUSE 금색 글자, 판 버튼(Button_Normal/Highlight/Pressed) + 금색 3D 단어 + 선택 오라(MenuSelectionAura).
    // 이미지는 Resources/PauseMenu(메인 메뉴 스프라이트 사본 + RESUME/RESTART 단어)에서 불러온다.
    private const float ButtonWidth = 720f, ButtonHeight = 120f, ButtonGap = 140f;

    private void BuildOverlay()
    {
        Canvas canvas = EndingUi.CreateCanvas(transform, "PauseCanvas", 900); // 게임오버(1000)보다 아래
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        Transform canvasTr = canvas.transform;

        Image dim = EndingUi.CreateFill(canvasTr, "Dim", new Color(0.02f, 0.03f, 0.08f, 0.72f));
        dim.raycastTarget = true; // 뒤 화면 클릭 차단

        EndingUi.CreatePicture(canvasTr, "PauseTitle", EndingUi.LoadTexture("Pause_Title"),
            640f, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f));

        resumeButton = CreateMenuButton(canvasTr, "ResumeButton", "Menu_RESUME", new Vector2(0f, 90f), Resume);
        Button restartButton = CreateMenuButton(canvasTr, "RestartAreaButton", "Menu_RESTART", new Vector2(0f, 90f - ButtonGap), RestartArea);
        Button quitButton = CreateMenuButton(canvasTr, "QuitButton", "Menu_Exit", new Vector2(0f, 90f - ButtonGap * 2f), QuitGame);

        // 버튼 영문 단어의 뜻(한글) 안내 + 닫기 키 안내
        var hint = EndingUi.CreateText(canvasTr, "Hint", "RESUME 게임 계속하기   ·   RESTART 영역 다시 하기   ·   EXIT 게임 종료하기   (ESC : 계속하기)", 26,
            new Color(1f, 1f, 1f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(1500f, 40f));
        hint.fontStyle = FontStyle.Normal;

        // 위/아래 키 이동이 세 버튼 사이에서 순환하도록 명시
        resumeButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = restartButton, selectOnUp = quitButton };
        restartButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = quitButton, selectOnUp = resumeButton };
        quitButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = resumeButton, selectOnUp = restartButton };

        overlayRoot = canvas.gameObject;
        overlayRoot.SetActive(false);
    }

    private static Sprite LoadSprite(string name)
    {
        return Resources.Load<Sprite>("PauseMenu/" + name);
    }

    // 메인 메뉴 버튼과 같은 구조: 판 이미지(SpriteSwap) + 선택 오라(MenuSelectionAura가 선택 시 맥동) + 금색 단어 이미지
    private static Button CreateMenuButton(Transform parent, string objectName, string wordSpriteName, Vector2 anchoredPosition, UnityAction onClick)
    {
        var buttonObject = new GameObject(objectName, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        var plate = buttonObject.AddComponent<Image>();
        plate.sprite = LoadSprite("Button_Normal");
        plate.type = Image.Type.Simple;
        RectTransform rect = plate.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        rect.anchoredPosition = anchoredPosition;

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = plate;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = LoadSprite("Button_Highlight"),
            selectedSprite = LoadSprite("Button_Highlight"),
            pressedSprite = LoadSprite("Button_Pressed"),
            disabledSprite = LoadSprite("Button_Pressed")
        };
        button.onClick.AddListener(onClick);

        // 선택 오라: 판보다 가로 10%, 세로 30%씩 더 크게(Menu_Aura 스프라이트의 패딩 비율)
        var auraObject = new GameObject("Aura", typeof(RectTransform));
        auraObject.transform.SetParent(buttonObject.transform, false);
        var aura = auraObject.AddComponent<Image>();
        aura.sprite = LoadSprite("Menu_Aura");
        aura.raycastTarget = false;
        aura.color = new Color(1f, 1f, 1f, 0f);
        aura.enabled = false;
        RectTransform auraRect = aura.rectTransform;
        auraRect.anchorMin = Vector2.zero;
        auraRect.anchorMax = Vector2.one;
        auraRect.offsetMin = new Vector2(-ButtonWidth * 0.1f, -ButtonHeight * 0.3f);
        auraRect.offsetMax = new Vector2(ButtonWidth * 0.1f, ButtonHeight * 0.3f);
        // MenuSelectionAura.Awake 가 오라의 원래 크기를 읽으므로, 오라를 연결한 뒤에 Awake 가 돌도록 잠시 꺼 두고 추가한다
        buttonObject.SetActive(false);
        var auraComponent = buttonObject.AddComponent<MenuSelectionAura>();
        typeof(MenuSelectionAura).GetField("aura", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(auraComponent, aura);
        buttonObject.SetActive(true);

        var wordObject = new GameObject("Word", typeof(RectTransform));
        wordObject.transform.SetParent(buttonObject.transform, false);
        var word = wordObject.AddComponent<Image>();
        word.sprite = LoadSprite(wordSpriteName);
        word.preserveAspect = true;
        word.raycastTarget = false;
        RectTransform wordRect = word.rectTransform;
        wordRect.anchorMin = new Vector2(0.1f, 0.12f);
        wordRect.anchorMax = new Vector2(0.9f, 0.88f);
        wordRect.offsetMin = Vector2.zero;
        wordRect.offsetMax = Vector2.zero;

        return button;
    }
}
