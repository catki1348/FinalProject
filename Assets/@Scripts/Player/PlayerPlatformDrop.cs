using UnityEngine;

// 아래+점프로 현재 보스존 발판만 통과한다. 바닥과 보스의 충돌은 변경하지 않는다.
// OpenAI Codex (GPT-6), 2026-10-14.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D))]
public class PlayerPlatformDrop : MonoBehaviour
{
    private PlayerMovement movement;
    private Rigidbody2D body;
    private Collider2D[] ownColliders;
    private Collider2D bodyCollider, ignored;
    private bool[] previousIgnore;
    private readonly Collider2D[] supports = new Collider2D[8];
    private float started;
    public bool IsIgnoring(Collider2D surface) => ignored == surface;

    // 플레이어 자체의 콜라이더를 한 번 캐시한다.
    private void Awake()
    {
        movement = GetComponent<PlayerMovement>(); body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>(); ownColliders = GetComponentsInChildren<Collider2D>();
        previousIgnore = new bool[ownColliders.Length];
    }
    private void Update()
    {
        if (Input.GetAxisRaw("Vertical") < -0.5f && Input.GetButtonDown("Jump")) TryDrop();
    }
    // 발 바로 아래에 닿은 전용 발판만 선택한다. 일반 지면에서는 동작하지 않는다.
    public bool TryDrop()
    {
        if (bodyCollider == null) return false;
        if (ignored != null)
        {
            if (bodyCollider.bounds.min.y >= ignored.bounds.min.y - 0.2f) return false;
            Restore(); // 아래층 지지 발판으로 다음 하강을 시작할 수 있게 이전 무시를 끝낸다.
        }
        Bounds b = bodyCollider.bounds;
        int count = Physics2D.OverlapBoxNonAlloc(new Vector2(b.center.x, b.min.y - 0.015f),
            new Vector2(b.size.x * 0.9f, 0.12f), 0, supports);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = supports[i];
            if (!c.TryGetComponent<BossArenaLedge>(out _) || c.isTrigger ||
                Mathf.Abs(b.min.y - c.bounds.max.y) > 0.12f || body.linearVelocity.y > 0.1f) continue;
            ignored = c; started = Time.time;
            for (int j = 0; j < ownColliders.Length; j++)
            {
                previousIgnore[j] = Physics2D.GetIgnoreCollision(ownColliders[j], ignored);
                Physics2D.IgnoreCollision(ownColliders[j], ignored, true);
            }
            movement.GroundingSuppressed = true;
            body.linearVelocity = new Vector2(body.linearVelocity.x, -1f);
            return true;
        }
        return false;
    }
    // 몸이 발판 아래로 빠지거나 겹침 없는 시간 제한에 도달하면 충돌을 복구한다.
    private void FixedUpdate()
    {
        if (ignored == null) { if (movement != null) movement.GroundingSuppressed = false; return; }
        // 키가 층간 간격보다 크므로 몸 위쪽이 이전 발판과 겹쳐도 아래층 접지는 허용한다.
        movement.GroundingSuppressed = bodyCollider.bounds.min.y >= ignored.bounds.min.y - 0.05f;
        if (!ignored.enabled || !ignored.gameObject.activeInHierarchy ||
            bodyCollider.bounds.max.y < ignored.bounds.min.y - 0.02f ||
            body.linearVelocity.y > 0 && bodyCollider.bounds.min.y > ignored.bounds.max.y + 0.02f ||
            Time.time - started > 0.35f && !bodyCollider.bounds.Intersects(ignored.bounds)) Restore();
    }
    private void OnDisable() { Restore(); }
    // 워프/리스폰/비활성화 뒤 충돌 무시가 남지 않게 이전 상태로 복구한다.
    public void Restore()
    {
        if (ignored != null && ownColliders != null)
            for (int i = 0; i < ownColliders.Length; i++)
                if (ownColliders[i] != null) Physics2D.IgnoreCollision(ownColliders[i], ignored, previousIgnore[i]);
        ignored = null;
        if (movement != null) movement.GroundingSuppressed = false;
    }
}
