using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private AudioClip titleBgm;       // 오프닝과 같은 타이틀 BGM(같은 클립이라 오프닝에서 이어지면 끊기지 않음)
    [SerializeField] private AudioClip sfxPreviewClip; // 옵션에서 효과음 볼륨을 조절한 뒤 들려줄 미리듣기 효과음(비워두면 소리 없음)

    private OptionsMenuUI optionsMenu; // 배경음/효과음 볼륨 옵션 패널(코드로 생성)

    private void Awake()
    {
        startButton.onClick.AddListener(OnClickStart);
        optionsButton.onClick.AddListener(OnClickOptions);
        creditsButton.onClick.AddListener(OnClickCredits);

        optionsMenu = OptionsMenuUI.Create(sfxPreviewClip);
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

    // 옵션 버튼: 배경음/효과음 볼륨 조절 패널을 연다
    private void OnClickOptions()
    {
        optionsMenu.Open();
    }

    private void OnClickCredits()
    {
        Debug.Log("Credits clicked");
    }
}
