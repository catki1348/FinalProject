using UnityEngine;

// 테스트용: Inspector에 "스테이지-영역"(MapEdit.md 표기, 예: "1-4")을 적어두면 스테이지 시작 시
// 1영역 대신 그 영역의 시작 지점(★)에서 플레이어를 스폰시킬 수 있게 해주는 매니저(규칙 35).
// 실제 스폰 처리는 StageManager가 담당하고, 이 매니저는 "어디서 시작할지"만 파싱해서 알려준다
// (단일 책임 - 진행 상태/스폰 로직은 여전히 StageManager 소관).
// 비워두면(기본값) 평소처럼 1영역 시작 지점에서 시작한다.
public class WarpManager : MonoBehaviour
{
    public static WarpManager Instance { get; private set; }

    [Tooltip("테스트로 시작할 지점. \"스테이지-영역\" 형식(예: 1-1, 1-4). 비워두면 정상적으로 1영역에서 시작한다.")]
    [SerializeField] private string testStartZone = "";

    [Tooltip("테스트 목적지 발판의 Hierarchy 이름(정확히 일치). 지정하면 시작 영역보다 우선하며, 플레이 중 컨텍스트 메뉴로 다시 이동할 수 있다.")]
    [SerializeField] private string testPlatformName = "";

    [ContextMenu("테스트 발판으로 텔레포트")]
    private void TeleportToTestPlatform()
    {
        if (!Application.isPlaying) return;
        if (StageManager.Instance != null)
            StageManager.Instance.TryWarpToTestPlatform();
    }

    public bool TryGetTestPlatformPosition(Transform player, out Vector2 position)
    {
        position = default;
        if (string.IsNullOrWhiteSpace(testPlatformName) || player == null) return false;

        string targetName = testPlatformName.Trim();
        Transform target = null;
        // 플레이어가 있는 씬만 탐색한다. 일반 발판에는 PlatformCore가 없을 수도 있다.
        foreach (GameObject root in player.gameObject.scene.GetRootGameObjects())
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name != targetName) continue;
            if (target != null)
            {
                Debug.LogWarning($"WarpManager: '{targetName}' 이름이 중복됩니다. 발판에 고유한 이름을 지정하세요.", this);
                return false;
            }
            target = candidate;
        }

        Collider2D surface = null;
        if (target != null && target.gameObject.activeInHierarchy)
        foreach (Collider2D collider in target.GetComponentsInChildren<Collider2D>())
        {
            if (!collider.enabled || collider.isTrigger || collider.transform.IsChildOf(player)) continue;
            if (surface == null || collider.bounds.max.y > surface.bounds.max.y) surface = collider;
        }
        if (surface == null)
        {
            Debug.LogWarning($"WarpManager: '{targetName}' 발판을 찾을 수 없거나 활성화된 일반 Collider2D가 없습니다.", this);
            return false;
        }

        float footOffset = 0f;
        foreach (Collider2D collider in player.GetComponentsInChildren<Collider2D>())
        {
            if (collider.enabled && !collider.isTrigger)
                footOffset = Mathf.Max(footOffset, player.position.y - collider.bounds.min.y);
        }
        position = new Vector2(surface.bounds.center.x, surface.bounds.max.y + footOffset + 0.05f);
        return true;
    }

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
