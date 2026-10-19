using UnityEngine;

// 스테이지 진행 상태(1영역 시작 지점 스폰, 5영역 종료점 도달 시 클리어/엔딩 처리)를 관리하는 매니저(규칙 35).
// 카메라 이동/줌 책임은 PlayerCameraFollow + CameraBoundsZone으로 이전됨(팔로우 카메라 구조 도입, @Edit/CameraDesign_FollowCamera.md 참고).
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [SerializeField] private string nextStageSceneName; // 5영역 보스 처치 시 이동할 다음 스테이지 씬 이름(비워두면 이동하지 않음)
    [SerializeField] private AudioClip stageBgm;         // 이 스테이지의 배경음(비워두면 재생하지 않음)
    [SerializeField] private AudioClip stageAmbience;    // 이 스테이지의 환경음(비워두면 재생하지 않음)
    [SerializeField] private Transform player;           // 씬의 플레이어(Inspector 연결)
    [SerializeField] private int stageIndex = 1;         // 이 스테이지 번호(MapEdit.md 규칙 3 "스테이지-영역" 표기용)
    [SerializeField] private bool enableSection2SlopeSlide; // 선택한 스테이지의 2영역에서만 경사 슬라이드 허용
    [SerializeField] private ZoneStartPoint[] zoneStartPoints = new ZoneStartPoint[5]; // 1~5영역 시작 지점(★), 인덱스 0=1영역
    [SerializeField] private PlayerCameraFollow cameraFollow;                          // 카메라 활성 구간 전환용
    [SerializeField] private CameraBoundsZone[] zoneCameraBounds = new CameraBoundsZone[5]; // 1~5영역 각각의 카메라 구간, zoneStartPoints와 1:1 대응

    // 일시정지 메뉴의 "영역 다시 하기"용: 씬을 다시 불러온 뒤 이 (스테이지, 영역)에서 시작하도록 예약한다. 한 번 쓰면 비워진다(static이라 씬 재로드 후에도 유지).
    private static int pendingStage;
    private static int pendingArea;

    public int StageIndex => stageIndex;

    // 현재 진행 중인 영역(1~5). 골인 지점(ZoneEndPoint)에 도달할 때마다 갱신된다(MapEdit.md 규칙 4).
    public int CurrentArea { get; private set; } = 1;

    // 영역이 바뀔 때 구독자(디버그 UI 등)에게 알리는 이벤트(규칙 36: Find 대신 이벤트 기반 통신)
    public event System.Action<int> OnAreaChanged;

    // "스테이지-영역" 표기(규칙 3), 예: 1스테이지 2영역 -> "1-2"
    public string CurrentAreaLabel => $"{stageIndex}-{CurrentArea}";

    private void Awake()
    {
        Instance = this;
    }

    // 스테이지 진입 시 플레이어를 시작 영역(평소 1영역, WarpManager에 테스트 지점이 지정되어
    // 있으면 그 영역)의 시작 지점에 스폰
    private void Start()
    {
        SoundManager.Instance.PlayBgm(stageBgm);
        SoundManager.Instance.PlayAmbience(stageAmbience);
        CurrentArea = ResolveStartArea();
        MovePlayerToStartOfCurrentArea();
        TryWarpToTestPlatform();
    }

    // 이름/발판 검증은 WarpManager, 실제 이동과 진행 상태 갱신은 StageManager가 담당한다.
    public bool TryWarpToTestPlatform()
    {
        if (WarpManager.Instance == null ||
            !WarpManager.Instance.TryGetTestPlatformPosition(player, out Vector2 position)) return false;

        WarpPlayerTo(position, true);
        return true;
    }

    // Claude Sonnet 5.5, 2026-10-17: 플레이어를 position으로 옮기고, 그 위치가 속한 카메라 구간으로 진행 영역(CurrentArea)과 카메라를 맞춘다.
    // WarpManager 테스트 이동과 낙사 복귀(FallRecoveryManager)가 함께 쓴다. notifyAlways=false면 영역이 실제로 바뀔 때만 OnAreaChanged를 발행한다.
    public void WarpPlayerTo(Vector2 position, bool notifyAlways)
    {
        if (player == null) return;
        int previousArea = CurrentArea;
        CameraBoundsZone destinationZone = null;
        if (zoneCameraBounds != null)
        for (int i = 0; i < zoneCameraBounds.Length; i++)
        {
            CameraBoundsZone zone = zoneCameraBounds[i];
            if (zone == null) continue;
            Bounds bounds = zone.WorldBounds;
            if (position.x < bounds.min.x || position.x > bounds.max.x ||
                position.y < bounds.min.y || position.y > bounds.max.y) continue;
            CurrentArea = i + 1;
            destinationZone = zone;
            break;
        }
        if (destinationZone == null && cameraFollow != null) destinationZone = cameraFollow.ActiveZone; // 어느 구간에도 없으면 현재 구간 유지

        if (player.TryGetComponent(out PlayerMovement movement))
            movement.SetSlopeSlideEnabled(enableSection2SlopeSlide && CurrentArea == 2);
        if (player.TryGetComponent(out Rigidbody2D body))
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        else player.position = new Vector3(position.x, position.y, player.position.z);

        if (cameraFollow != null) cameraFollow.SnapTo(position, destinationZone);
        if (notifyAlways || CurrentArea != previousArea) OnAreaChanged?.Invoke(CurrentArea);
    }

    // 현재 영역의 시작 지점(★) 위치 - 낙사 복귀 기록이 없을 때의 복귀 지점
    public bool TryGetCurrentAreaStart(out Vector2 position)
    {
        int index = CurrentArea - 1;
        position = default;
        if (zoneStartPoints == null || index < 0 || index >= zoneStartPoints.Length || zoneStartPoints[index] == null) return false;
        position = zoneStartPoints[index].transform.position;
        return true;
    }

    // 시작할 영역(1~5)을 결정한다. WarpManager에 이 스테이지(stageIndex)에 대한 테스트 시작
    // 지점이 유효하게 지정되어 있으면 그 영역에서, 아니면 평소처럼 1영역에서 시작한다.
    private int ResolveStartArea()
    {
        int reservedStage = pendingStage, reservedArea = pendingArea;
        pendingStage = pendingArea = 0;
        if (reservedStage == stageIndex && reservedArea >= 1 && reservedArea <= zoneStartPoints.Length) return reservedArea;

        if (WarpManager.Instance != null &&
            WarpManager.Instance.TryGetTestZone(out int stage, out int zone) &&
            stage == stageIndex && zone >= 1 && zone <= zoneStartPoints.Length)
        {
            return zone;
        }

        return 1;
    }

    // 현재 영역을 처음부터 다시 한다: 씬을 다시 불러오고(몬스터/아이템/보스 초기화, 플레이어 상태는 새로 시작) 같은 영역의 시작 지점(★)에서 시작한다.
    public void RestartCurrentArea()
    {
        pendingStage = stageIndex;
        pendingArea = CurrentArea;
        Time.timeScale = 1f;
        GameManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    // 영역의 골인 지점(○)에 도달했을 때 ZoneEndPoint가 호출 - 다음 영역으로 진행 상태를 갱신하고
    // 플레이어를 다음 영역의 시작 지점(★)으로 이동시킨다.
    public void AdvanceToNextArea()
    {
        if (CurrentArea >= 5) return;
        CurrentArea++;
        OnAreaChanged?.Invoke(CurrentArea);
        MovePlayerToStartOfCurrentArea();
    }

    // CurrentArea(1~5)에 대응하는 시작 지점으로 플레이어를 이동시키고, 카메라의 활성 구간도
    // 그 영역에 맞춰 함께 전환한다(평소 걸어서 이동할 때는 SectionEdgeTrigger/ZoneEndPoint가
    // 별도로 카메라를 전환해주지만, WarpManager로 특정 영역에 곧바로 스폰되는 경우는 그런 트리거를
    // 거치지 않으므로 여기서도 직접 맞춰줘야 카메라가 플레이어를 제대로 따라간다).
    // Rigidbody2D가 있으면 물리 트리거 콜백 도중에도 안전하게 rb.position으로 이동시키고,
    // 없으면 transform.position으로 이동시킨다.
    private void MovePlayerToStartOfCurrentArea()
    {
        // 정상 진입과 WarpManager 진입 모두 같은 지점에서 이동 상태를 갱신한다.
        if (player != null && player.TryGetComponent(out PlayerMovement movement))
            movement.SetSlopeSlideEnabled(enableSection2SlopeSlide && CurrentArea == 2);
        int index = CurrentArea - 1;
        if (player == null || zoneStartPoints == null || index < 0 || index >= zoneStartPoints.Length) return;

        ZoneStartPoint start = zoneStartPoints[index];
        if (start == null) return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = start.transform.position;
        else player.position = start.transform.position;

        if (cameraFollow != null && zoneCameraBounds != null && index < zoneCameraBounds.Length)
        {
            cameraFollow.SetActiveZone(zoneCameraBounds[index]);
        }
    }

    // 5영역 종료점(BossZoneEndPoint, 보스 처치 후에만 활성화됨)에 플레이어가 도달했을 때 호출된다.
    // MapEdit.md 규칙 9, 10: 클리어 화면(5-5의 경우 게임 클리어 화면 후 엔딩 씬)을 먼저 보여준 뒤 다음 단계로 진행한다.
    public void OnZone5EndPointReached()
    {
        bool isFinalStage = stageIndex >= 5;

        if (isFinalStage)
        {
            // 5-5: 게임 클리어 화면(GAME CLEAR) → 엔터 → 엔딩 씬(Ending). 엔딩 전환은 GameClearManager 가 EndingManager 를 호출해 진행한다.
            GameClearManager.Instance.TriggerGameClear();
            return;
        }

        StageClearManager.Instance.TriggerStageClear(LoadNextStage);
    }

    private void LoadNextStage()
    {
        if (string.IsNullOrEmpty(nextStageSceneName)) return;
        GameManager.Instance.LoadScene(nextStageSceneName);
    }
}
