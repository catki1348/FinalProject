using UnityEngine;

// 스테이지 진행 상태(1영역 시작 지점 스폰, 보스 처치 시 다음 스테이지 로드)를 관리하는 매니저(규칙 39).
// 카메라 이동/줌 책임은 PlayerCameraFollow + CameraBoundsZone으로 이전됨(팔로우 카메라 구조 도입, @Edit/CameraDesign_FollowCamera.md 참고).
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [SerializeField] private string nextStageSceneName; // 5영역 보스 처치 시 이동할 다음 스테이지 씬 이름(비워두면 이동하지 않음)
    [SerializeField] private Transform player;           // 씬의 플레이어(Inspector 연결)
    [SerializeField] private int stageIndex = 1;         // 이 스테이지 번호(MapEdit.md 규칙 3 "스테이지-영역" 표기용)
    [SerializeField] private ZoneStartPoint[] zoneStartPoints = new ZoneStartPoint[5]; // 1~5영역 시작 지점(★), 인덱스 0=1영역

    // 현재 진행 중인 영역(1~5). 골인 지점(ZoneEndPoint)에 도달할 때마다 갱신된다(MapEdit.md 규칙 4).
    public int CurrentArea { get; private set; } = 1;

    // 영역이 바뀔 때 구독자(디버그 UI 등)에게 알리는 이벤트(규칙 40: Find 대신 이벤트 기반 통신)
    public event System.Action<int> OnAreaChanged;

    // "스테이지-영역" 표기(규칙 3), 예: 1스테이지 2영역 -> "1-2"
    public string CurrentAreaLabel => $"{stageIndex}-{CurrentArea}";

    private void Awake()
    {
        Instance = this;
    }

    // 스테이지 진입 시 플레이어를 1영역 시작 지점에 스폰
    private void Start()
    {
        if (player == null || zoneStartPoints == null || zoneStartPoints.Length == 0 || zoneStartPoints[0] == null) return;
        player.position = zoneStartPoints[0].transform.position;
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

    // CurrentArea(1~5)에 대응하는 시작 지점으로 플레이어를 이동시킨다.
    // Rigidbody2D가 있으면 물리 트리거 콜백 도중에도 안전하게 rb.position으로 이동시키고,
    // 없으면 transform.position으로 이동시킨다.
    private void MovePlayerToStartOfCurrentArea()
    {
        int index = CurrentArea - 1;
        if (player == null || zoneStartPoints == null || index < 0 || index >= zoneStartPoints.Length) return;

        ZoneStartPoint start = zoneStartPoints[index];
        if (start == null) return;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = start.transform.position;
        else player.position = start.transform.position;
    }

    // 5영역(마지막 구간)의 보스를 처치했을 때 보스 스크립트가 호출하는 진입점
    public void NotifyBossDefeated()
    {
        if (string.IsNullOrEmpty(nextStageSceneName)) return;
        GameManager.Instance.LoadScene(nextStageSceneName);
    }
}
