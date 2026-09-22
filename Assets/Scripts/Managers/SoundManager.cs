using UnityEngine;

// 배경음(BGM)과 효과음(SFX) 재생을 전담하는 매니저(규칙 39: 사운드 같은 공통 시스템은 매니저로 중앙 관리).
// 각 스테이지는 StageManager에 BGM 클립을 연결해 스테이지 진입 시 PlayBgm을 호출하고,
// 점프/공격처럼 짧은 효과음은 PlaySfx로 재생한다. 클립을 아직 연결하지 않았다면 조용히 무시된다
// (TreasureChest.cs와 동일하게, 나중에 에셋만 연결하면 바로 동작하도록 설계).
public class SoundManager : MonoBehaviour
{
    private static SoundManager instance;

    public static SoundManager Instance
    {
        get
        {
            if (instance == null)
            {
                var managerObject = new GameObject(nameof(SoundManager));
                instance = managerObject.AddComponent<SoundManager>();
                DontDestroyOnLoad(managerObject);
            }

            return instance;
        }
    }

    [SerializeField] private float bgmVolume = 0.5f; // 배경음 기본 볼륨
    [SerializeField] private float sfxVolume = 1f;    // 효과음 기본 볼륨

    private AudioSource bgmSource; // 배경음 전용(루프 재생, 한 번에 하나만)
    private AudioSource sfxSource; // 효과음 전용(PlayOneShot으로 여러 개가 겹쳐도 서로 끊기지 않음)
    private AudioClip currentBgmClip; // 중복 재생 방지 - 이미 재생 중인 곡을 다시 요청하면 무시

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = bgmVolume;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;
    }

    // 스테이지 진입 등에서 호출 - clip이 비어있거나 이미 재생 중인 곡과 같으면 아무 것도 하지 않는다
    // (씬을 오가며 같은 스테이지 BGM을 매번 다시 호출해도 처음부터 끊기지 않고 이어짐)
    public void PlayBgm(AudioClip clip)
    {
        if (clip == null || clip == currentBgmClip) return;

        currentBgmClip = clip;
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopBgm()
    {
        currentBgmClip = null;
        bgmSource.Stop();
    }

    // 점프/공격 등 한 번 재생되고 끝나는 짧은 효과음 재생
    public void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
