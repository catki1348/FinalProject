using UnityEngine;

// Worker record: Codex (GPT-6), 2026-10-15; bounded 4-1 monster placement behavior.

// A placement-specific leash: keeps chase and patrol patterns inside their design area.
// Existing shared patterns remain unchanged; dead flying monsters can still fall.
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(Rigidbody2D), typeof(MonsterStats))]
public sealed class MonsterMovementBounds : MonoBehaviour
{
    [SerializeField] private Transform areaOrigin;
    [SerializeField] private Vector2 min, max;
    [SerializeField] private bool constrainY;
    private Rigidbody2D body;
    private MonsterStats stats;
    private Collider2D bodyCollider;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        stats = GetComponent<MonsterStats>();
        bodyCollider = GetComponent<Collider2D>();
    }

    // Clamp the body as well as outward velocity, leaving gravity/jumps free on ground monsters.
    private void LateUpdate()
    {
        if (stats.IsDead || areaOrigin == null) return;
        Vector2 origin = areaOrigin.position;
        Vector2 padding = bodyCollider.bounds.extents;
        Vector2 low = origin + min + padding, high = origin + max - padding;
        Vector2 pos = body.position, velocity = body.linearVelocity;
        float x = low.x > high.x ? (low.x + high.x) * 0.5f : Mathf.Clamp(pos.x, low.x, high.x);
        if ((pos.x <= low.x && velocity.x < 0) || (pos.x >= high.x && velocity.x > 0)) velocity.x = 0;
        pos.x = x;
        if (constrainY)
        {
            float y = low.y > high.y ? (low.y + high.y) * 0.5f : Mathf.Clamp(pos.y, low.y, high.y);
            if ((pos.y <= low.y && velocity.y < 0) || (pos.y >= high.y && velocity.y > 0)) velocity.y = 0;
            pos.y = y;
        }
        body.position = pos;
        body.linearVelocity = velocity;
    }
}
