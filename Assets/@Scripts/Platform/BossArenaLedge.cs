using UnityEngine;

// 보스존 한 방향 발판 표시. 보스 충돌/상호작용은 레이어와 excludeLayers로 제외한다.
// Ground 레이어를 강제하는 일반 PlatformCore는 이 전용 발판에 붙이지 않는다.
// OpenAI Codex (GPT-6), 2026-10-14.
[RequireComponent(typeof(BoxCollider2D), typeof(PlatformEffector2D))]
[DefaultExecutionOrder(-150)]
public class BossArenaLedge : MonoBehaviour
{
    private Collider2D surface;
    private Transform cachedPlayer;
    private Collider2D playerCollider;
    private PlayerPlatformDrop drop;

    private void Awake() { surface = GetComponent<Collider2D>(); }
    private void FixedUpdate() { UpdatePlayerCollision(); }

    // 단차보다 플레이어 키가 커도 위층에 밀려 올라가지 않게 발 높이로 한 방향 충돌을 보완한다.
    // 명시적 하강 무시와 함께 판단하여 두 기능이 서로 충돌 설정을 덮어쓰지 않는다.
    public void UpdatePlayerCollision()
    {
        Transform player = GameManager.Instance.PlayerTransform;
        if (player != cachedPlayer)
        {
            if (playerCollider != null && surface != null) Physics2D.IgnoreCollision(playerCollider, surface, false);
            cachedPlayer = player; playerCollider = null; drop = null;
            if (player != null) { player.TryGetComponent(out playerCollider); player.TryGetComponent(out drop); }
        }
        if (surface == null || playerCollider == null) return;
        bool ignore = playerCollider.bounds.min.y < surface.bounds.max.y - 0.025f ||
            drop != null && drop.IsIgnoring(surface);
        if (Physics2D.GetIgnoreCollision(playerCollider, surface) != ignore)
            Physics2D.IgnoreCollision(playerCollider, surface, ignore);
    }

    // 씬 비활성화나 파괴 뒤 플레이어 충돌 무시가 남지 않게 정리한다.
    private void OnDisable()
    {
        if (playerCollider != null && surface != null) Physics2D.IgnoreCollision(playerCollider, surface, false);
    }
}
