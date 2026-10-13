using UnityEngine;

// 배경음(BGM)과 효과음(SFX) 재생을 전담하는 매니저(규칙 35: 사운드 같은 공통 시스템은 매니저로 중앙 관리).
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
                // Resources/SoundManager 프리팹이 있으면 그걸로 생성(어느 씬에서 시작해도 인스펙터에서 조절한 볼륨이 적용됨).
                // 프리팹이 없으면 코드 기본값으로 새로 만든다
                var prefab = Resources.Load<SoundManager>(nameof(SoundManager));
                instance = prefab != null
                    ? Instantiate(prefab)
                    : new GameObject(nameof(SoundManager)).AddComponent<SoundManager>();
                instance.name = nameof(SoundManager);
                DontDestroyOnLoad(instance.gameObject);
            }

            return instance;
        }
    }

    [SerializeField] private float bgmVolume = 0.5f; // 배경음 기본 볼륨
    [SerializeField] private float sfxVolume = 1f;    // 효과음 기본 볼륨
    [SerializeField] private float ambienceVolume = 0.4f; // 환경음 기본 볼륨

    private AudioSource bgmSource; // 배경음 전용(루프 재생, 한 번에 하나만)
    private AudioSource sfxSource; // 효과음 전용(PlayOneShot으로 여러 개가 겹쳐도 서로 끊기지 않음)
    private AudioSource ambienceSource; // 환경음 전용(루프 재생, BGM과 동시에 깔림)
    private AudioClip currentBgmClip; // 중복 재생 방지 - 이미 재생 중인 곡을 다시 요청하면 무시
    private AudioClip currentAmbienceClip; // 중복 재생 방지
    private AudioClip stageBgmClip; // 보스전 중에 기억해두는 원래 스테이지 BGM(보스 처치 후 복귀용)

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

        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.loop = true;
        ambienceSource.playOnAwake = false;
        ambienceSource.volume = ambienceVolume;
    }

    // 스테이지 진입 등에서 호출 - clip이 비어있거나 이미 재생 중인 곡과 같으면 아무 것도 하지 않는다
    // (씬을 오가며 같은 스테이지 BGM을 매번 다시 호출해도 처음부터 끊기지 않고 이어짐)
    public void PlayBgm(AudioClip clip)
    {
        if (clip == null || clip == currentBgmClip) return;

        stageBgmClip = null; // 새 스테이지 곡이 오면 보스전 복귀용 기억은 버린다
        currentBgmClip = clip;
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopBgm()
    {
        currentBgmClip = null;
        stageBgmClip = null;
        bgmSource.Stop();
    }

    // 보스전 BGM - 재생 중이던 스테이지 BGM을 기억해두고 보스곡으로 교체한다(clip이 비어있으면 스테이지 BGM 유지)
    public void PlayBossBgm(AudioClip clip)
    {
        if (clip == null || clip == currentBgmClip) return;

        if (stageBgmClip == null) stageBgmClip = currentBgmClip;
        currentBgmClip = clip;
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    // 보스 처치 후 호출 - 기억해둔 스테이지 BGM으로 되돌린다(없으면 BGM 정지)
    public void EndBossBgm()
    {
        if (stageBgmClip == null)
        {
            if (currentBgmClip != null) StopBgm();
            return;
        }

        AudioClip stageClip = stageBgmClip;
        stageBgmClip = null;
        PlayBgm(stageClip);
    }

    // 스테이지 환경음(새소리/바람 등) 재생 - BGM과 별도 채널이라 동시에 깔린다. PlayBgm과 동일한 중복 방지 규칙
    public void PlayAmbience(AudioClip clip)
    {
        if (clip == null || clip == currentAmbienceClip) return;

        currentAmbienceClip = clip;
        ambienceSource.clip = clip;
        ambienceSource.Play();
    }

    public void StopAmbience()
    {
        currentAmbienceClip = null;
        ambienceSource.Stop();
    }

    // 설정 UI 등에서 런타임에 볼륨을 바꿀 때 사용(0~1). 재생 중인 루프 채널에도 즉시 반영된다
    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        bgmSource.volume = bgmVolume;
    }

    public void SetAmbienceVolume(float volume)
    {
        ambienceVolume = Mathf.Clamp01(volume);
        ambienceSource.volume = ambienceVolume;
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        sfxSource.volume = sfxVolume;
    }

    // 점프/공격 등 한 번 재생되고 끝나는 짧은 효과음 재생
    public void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
