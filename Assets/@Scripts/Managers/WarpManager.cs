using UnityEngine;

// 테스트용: Inspector에 "스테이지-영역"(MapEdit.md 표기, 예: "1-4")을 적어두면 스테이지 시작 시
// 1영역 대신 그 영역의 시작 지점(★)에서 플레이어를 스폰시킬 수 있게 해주는 매니저(규칙 39).
// 실제 스폰 처리는 StageManager가 담당하고, 이 매니저는 "어디서 시작할지"만 파싱해서 알려준다
// (단일 책임 - 진행 상태/스폰 로직은 여전히 StageManager 소관).
// 비워두면(기본값) 평소처럼 1영역 시작 지점에서 시작한다.
public class WarpManager : MonoBehaviour
{
    public static WarpManager Instance { get; private set; }

    [Tooltip("테스트로 시작할 지점. \"스테이지-영역\" 형식(예: 1-1, 1-4). 비워두면 정상적으로 1영역에서 시작한다.")]
    [SerializeField] private string testStartZone = "";

    private void Awake()
    {
        Instance = this;
    }

    // testStartZone을 "스테이지-영역"으로 파싱한다. 비어있거나 형식이 잘못되면 false를 반환한다.
    public bool TryGetTestZone(out int stage, out int zone)
    {
        stage = 0;
        zone = 0;

        if (string.IsNullOrWhiteSpace(testStartZone)) return false;

        string[] parts = testStartZone.Split('-');
        if (parts.Length != 2) return false;

        return int.TryParse(parts[0].Trim(), out stage) && int.TryParse(parts[1].Trim(), out zone);
    }
}
