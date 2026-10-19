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

    // 옵션 메뉴에서 조절한 볼륨을 다음 실행에도 유지하기 위한 PlayerPrefs 키
    private const string BgmVolumeKey = "Sound.BgmVolume";
    private const string SfxVolumeKey = "Sound.SfxVolume";

    [SerializeField] private float bgmVolume = 0.5f; // 배경음 기본 볼륨(저장된 설정이 있으면 그 값이 우선)
    [SerializeField] private float sfxVolume = 1f;    // 효과음 기본 볼륨(저장된 설정이 있으면 그 값이 우선)
    [SerializeField] private float ambienceVolume = 0.4f; // 환경음 기본 볼륨(BGM 볼륨 슬라이더를 인스펙터 기본값에 둔 상태일 때의 크기)
    [SerializeField] private AudioClip fallRecoverySfx;   // 낙사 후 안전 발판으로 복귀할 때 재생(비워두면 재생하지 않음)

    private float bgmFade = 1f;    // 연출용 배경음 감쇠 배율(0~1). 사용자 볼륨 설정과 별개로 곱해진다
    private AudioSource bgmSource; // 배경음 전용(루프 재생, 한 번에 하나만)
    private AudioSource sfxSource; // 효과음 전용(PlayOneShot으로 여러 개가 겹쳐도 서로 끊기지 않음)
    private AudioSource ambienceSource; // 환경음 전용(루프 재생, BGM과 동시에 깔림)
    private AudioClip currentBgmClip; // 중복 재생 방지 - 이미 재생 중인 곡을 다시 요청하면 무시
    private AudioClip currentAmbienceClip; // 중복 재생 방지
    private AudioClip stageBgmClip; // 보스전 중에 기억해두는 원래 스테이지 BGM(보스 처치 후 복귀용)
    private float bgmReferenceVolume; // 인스펙터에 설정된 BGM 기본 볼륨 - 환경음이 BGM 슬라이더를 따를 때 "기본 크기"의 기준

    // 옵션 UI가 슬라이더 초기값을 맞출 때 읽는 현재 볼륨(0~1)
    public float BgmVolume => bgmVolume;
    public float SfxVolume => sfxVolume;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        bgmReferenceVolume = Mathf.Max(bgmVolume, 0.01f); // 저장값으로 덮어쓰기 전에 기본값을 기억
        // 이전에 옵션에서 저장한 볼륨이 있으면 인스펙터 기본값 대신 사용
        bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, bgmVolume));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume));

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
        ApplyAmbienceVolume();
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

    // 배경음 재생 배속(피치). 크레딧 빨리 감기처럼 BGM 속도를 임시로 바꿀 때 쓴다(1 = 기본)
    public void SetBgmPitch(float pitch)
    {
        bgmSource.pitch = pitch;
    }

    // 크레딧 끝 페이드아웃처럼 BGM을 연출로 줄일 때 쓴다(1 = 설정 볼륨 그대로). 사용자 볼륨 설정은 바뀌지 않는다
    public void SetBgmFade(float fade)
    {
        bgmFade = Mathf.Clamp01(fade);
        bgmSource.volume = bgmVolume * bgmFade;
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

    // 낙사 후 복귀 효과음(Claude Sonnet 5.5, 2026-10-18). FallRecoveryManager는 런타임에 생성되어 인스펙터 슬롯이 없으므로,
    // 클립은 Resources/SoundManager 프리팹의 이 슬롯에 연결해 두고 복귀 직후 FallRecoveryManager가 호출한다. 비어 있으면 무시.
    public void PlayFallRecovery()
    {
        PlaySfx(fallRecoverySfx);
    }

    // 설정 UI 등에서 런타임에 볼륨을 바꿀 때 사용(0~1). 재생 중인 루프 채널에도 즉시 반영된다
    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        bgmSource.volume = bgmVolume * bgmFade;
        ApplyAmbienceVolume(); // 환경음도 BGM 슬라이더를 따라간다
        PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolume);
    }

    public void SetAmbienceVolume(float volume)
    {
        ambienceVolume = Mathf.Clamp01(volume);
        ApplyAmbienceVolume();
    }

    // 환경음은 BGM 슬라이더에 묶여 있다: 실제 볼륨 = 환경음 기본 볼륨 × (현재 BGM 볼륨 ÷ BGM 기본 볼륨).
    // BGM을 기본값(인스펙터 설정)에 두면 예전과 같은 크기이고, 0으로 내리면 환경음도 꺼지며, 올리면 함께 커진다(최대 1)
    private void ApplyAmbienceVolume()
    {
        ambienceSource.volume = Mathf.Clamp01(ambienceVolume * (bgmVolume / bgmReferenceVolume));
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        sfxSource.volume = sfxVolume;
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
    }

    // 슬라이더를 움직이는 동안은 값만 메모리에 두고, 조작이 끝나거나 옵션을 닫을 때 디스크에 기록한다
    public void SaveVolumeSettings()
    {
        PlayerPrefs.Save();
    }

    // 점프/공격 등 한 번 재생되고 끝나는 짧은 효과음 재생.
    // volumeScale: 클립 하나만 따로 줄이고 싶을 때(예: 아이템 획득음) 0~1 배율. 옵션의 효과음 볼륨은 sfxSource.volume으로 항상 함께 적용된다
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }
}
