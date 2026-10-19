using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button tutorialButton;    // 튜토리얼 버튼(START 아래) - 기본 조작법을 익히는 Tutorial 씬으로 이동
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button exitButton;        // 게임 종료 버튼(크레딧 아래)
    [SerializeField] private AudioClip titleBgm;       // 오프닝과 같은 타이틀 BGM(같은 클립이라 오프닝에서 이어지면 끊기지 않음)
    [SerializeField] private AudioClip creditsBgm;     // 크레딧 화면 BGM(바로크풍, 비워두면 타이틀 BGM 유지)
    [SerializeField] private AudioClip sfxPreviewClip; // 옵션에서 효과음 볼륨을 조절한 뒤 들려줄 미리듣기 효과음(비워두면 소리 없음)

    private OptionsMenuUI optionsMenu; // 배경음/효과음 볼륨 옵션 패널(코드로 생성)
    private CreditsUI creditsMenu;     // 위로 올라가는 크레딧 화면(코드로 생성)

    private void Awake()
    {
        startButton.onClick.AddListener(OnClickStart);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(OnClickTutorial);
        optionsButton.onClick.AddListener(OnClickOptions);
        creditsButton.onClick.AddListener(OnClickCredits);
        if (exitButton != null) exitButton.onClick.AddListener(OnClickExit);

        optionsMenu = OptionsMenuUI.Create(sfxPreviewClip);
        creditsMenu = CreditsUI.Create(creditsBgm, titleBgm);
    }

    // 메인메뉴 진입 시 타이틀 BGM 재생(이미 같은 곡이 재생 중이면 그대로 이어짐)
    private void Start()
    {
        SoundManager.Instance.PlayBgm(titleBgm);
    }

    private void OnClickStart()
    {
        GameManager.Instance.LoadScene("Stage1");
    }

    // 튜토리얼 버튼: 기본 조작법을 알려 주는 튜토리얼 씬으로 이동한다
    private void OnClickTutorial()
    {
        GameManager.Instance.LoadScene("Tutorial");
    }

    // 옵션 버튼: 배경음/효과음 볼륨 조절 패널을 연다
    private void OnClickOptions()
    {
        optionsMenu.Open();
    }

    private void OnClickCredits()
    {
        creditsMenu.Open();
    }

    // 종료 버튼: 빌드에서는 게임을 끝내고, 에디터에서는 플레이 모드를 멈춘다
    private void OnClickExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
