using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 엔딩 씬(Ending) 컨트롤러. 제작한 엔딩 영상(Assets/@ExternalAssets/Ending/FinalDungeon_Ending.mp4, 약 36초)을 전체 화면으로 재생한다.
/// 영상에는 이야기 자막(마왕은 쓰러졌다 … )과 마지막 화면(FINAL DUNGEON 로고 / THE END / 감사 문구)이 이미 들어 있다.
/// 음악은 SoundManager 의 BGM 채널로 영상과 함께 재생하므로 옵션의 배경음 볼륨이 그대로 적용된다
/// (영상 파일에는 소리를 넣지 않았다. BGM 은 Resources/Ending/Ending_BGM, 바로크풍 곡).
/// 순서:
///   1) 영상 준비가 끝나면 영상과 음악을 동시에 시작한다.
///   2) 영상 도중 엔터 → 검게 페이드 아웃 → 크레딧(건너뛰기).
///   3) 영상이 끝나면 마지막 장면이 그대로 남고 깜빡이는 PRESS ENTER 가 나온다 → 엔터 → 검게 페이드 아웃 → 크레딧.
///   4) 크레딧(메인메뉴 크레딧과 같은 CreditsUI)이 올라간다. 끝까지 올라가거나 건너뛰면(아무 키, 스페이스는 빨리 감기) 메인 메뉴로 돌아간다.
/// 화면 UI 는 모두 코드로 구성하므로 씬 파일에는 이 컴포넌트만 있으면 된다. 영상 재생에 실패해도 PRESS ENTER 로 빠져나갈 수 있다.
/// </summary>
public class EndingSceneController : MonoBehaviour
{
    [Header("영상")]
    [SerializeField] private VideoClip endingVideo;               // 엔딩 영상(Assets/@ExternalAssets/Ending/FinalDungeon_Ending.mp4, Inspector 에서 연결)
    [SerializeField] private float prepareTimeoutSeconds = 10f;   // 이 시간 안에 영상이 준비되지 않으면 영상 없이 진행

    [Header("동작 설정")]
    [SerializeField] private string nextSceneName = "MainMenu";   // 마지막에 돌아갈 씬
    [SerializeField] private float introFadeSeconds = 0.6f;       // 영상 첫 화면이 뜰 때 검은 막이 걷히는 시간
    [SerializeField] private float outroFadeSeconds = 1.0f;       // 나갈 때 검게 페이드 아웃
    [SerializeField] private float inputDelaySeconds = 0.5f;      // 씬 시작 직후 오입력 방지
    [SerializeField] private AudioClip endingBgm;                 // 비워두면 Resources/Ending/Ending_BGM 을 불러온다
    [SerializeField] private AudioClip creditsBgm;                // 크레딧 음악(비워두면 엔딩 음악이 그대로 이어짐)

    private VideoPlayer videoPlayer;
    private RenderTexture videoTexture;
    private Text prompt;
    private Image blackFade;

    private bool videoFinished;
    private float elapsed;

    private void Start()
    {
        Time.timeScale = 1f;   // 안전장치(게임 클리어 화면에서 멈춰 둔 값 복구)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopAmbience();
            SoundManager.Instance.StopBgm();
        }

        BuildUi();
        StartCoroutine(Run());
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
    }

    private void OnDestroy()
    {
        if (videoPlayer != null) videoPlayer.loopPointReached -= OnVideoFinished;
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
        }
    }

    private void BuildUi()
    {
        Canvas canvas = EndingUi.CreateCanvas(transform, "EndingCanvas", 0);

        videoTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
        videoTexture.name = "EndingVideoTexture";
        var picture = EndingUi.CreateStretchedPicture(canvas.transform, "EndingVideo", null);
        picture.texture = videoTexture;
        picture.color = Color.white;

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = false;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;   // 영상 파일에는 소리가 없고, 음악은 SoundManager 가 맡는다
        videoPlayer.source = VideoSource.VideoClip;   // 10MB 가 넘는 영상은 @ExternalAssets 에 두므로(수칙 43) StreamingAssets 경로 대신 클립을 직접 참조한다
        videoPlayer.clip = endingVideo;
        videoPlayer.loopPointReached += OnVideoFinished;

        prompt = EndingUi.CreateText(canvas.transform, "PressEnter", "PRESS ENTER", 44, new Color(1f, 1f, 1f, 0f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -430f), new Vector2(800f, 80f));

        blackFade = EndingUi.CreateFill(canvas.transform, "BlackFade", Color.black);   // 처음엔 완전히 검다
    }

    private IEnumerator Run()
    {
        // 1) 영상 준비 → 영상+음악 동시 시작
        videoPlayer.Prepare();
        float waited = 0f;
        bool videoOk = true;
        while (!videoPlayer.isPrepared)
        {
            waited += Time.unscaledDeltaTime;
            if (waited > prepareTimeoutSeconds)
            {
                Debug.LogWarning("엔딩 영상을 준비하지 못했습니다: " + videoPlayer.url);
                videoOk = false;
                break;
            }
            yield return null;
        }

        if (videoOk)
        {
            AudioClip clip = endingBgm != null ? endingBgm : Resources.Load<AudioClip>("Ending/Ending_BGM");
            if (SoundManager.Instance != null && clip != null) SoundManager.Instance.PlayBgm(clip);
            videoPlayer.Play();
            yield return Fade(blackFade, 1f, 0f, introFadeSeconds);

            // 2) 재생 중: 엔터로 건너뛰기
            while (!videoFinished)
            {
                if (elapsed > inputDelaySeconds && EndingUi.EnterPressed())
                {
                    yield return LeaveToNextScene();
                    yield break;
                }
                yield return null;
            }
        }
        else
        {
            yield return Fade(blackFade, 1f, 0f, introFadeSeconds);
        }

        // 3) 영상이 끝난 뒤: 마지막 장면 위에 PRESS ENTER 깜빡임 → 엔터 → 메인 메뉴
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            EndingUi.SetAlpha(prompt, (Mathf.Sin(t * 0.9f * Mathf.PI * 2f) + 1f) * 0.5f);
            if (t > 0.8f && EndingUi.EnterPressed()) break;
            yield return null;
        }

        yield return LeaveToNextScene();
    }

    private IEnumerator LeaveToNextScene()
    {
        yield return Fade(blackFade, 0f, 1f, outroFadeSeconds);

        // 크레딧: 메인메뉴의 크레딧 버튼과 같은 화면(CreditsUI)을 재사용한다. 닫히면(끝까지 올라감/건너뛰기) 메인 메뉴로 돌아간다.
        CreditsUI credits = CreditsUI.Create(creditsBgm, null);
        bool creditsDone = false;
        credits.Closed += () => creditsDone = true;
        credits.Open();
        while (!creditsDone) yield return null;
        Destroy(credits.gameObject);

        GameManager.Instance.LoadScene(nextSceneName);
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        videoFinished = true;
    }

    /// <summary>그래픽의 알파를 from → to 로 부드럽게 바꾼다.</summary>
    private static IEnumerator Fade(Graphic g, float from, float to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            EndingUi.SetAlpha(g, Mathf.Lerp(from, to, EndingUi.Smooth(t / seconds)));
            yield return null;
        }
        EndingUi.SetAlpha(g, to);
    }
}
