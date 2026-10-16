using UnityEngine;

// Worker record: Codex (GPT-6), 2026-10-15; gates mounted 4-1 projectile switches.

// Enables a mounted switch only near its designed outbound activation point.
// The player may board before the threshold without firing from the wrong position.
[DefaultExecutionOrder(100)]
public sealed class MovingPlatformSwitchGate : MonoBehaviour
{
    [SerializeField] private MovingPlatform platform;
    [SerializeField] private PlatformPath path;
    [SerializeField] private Collider2D switchCollider;
    [SerializeField, Range(0, 1)] private float activationFraction = 0.25f;
    private float previousFraction;

    private void Awake()
    {
        previousFraction = 0;
        switchCollider.enabled = false;
    }

    // Keep the switch open for a short interval on the outward trip only.
    private void FixedUpdate()
    {
        if (platform == null || path == null || path.PointCount < 2) return;
        Vector2 start = path.GetPoint(0), delta = (Vector2)path.GetPoint(1) - start;
        float fraction = Vector2.Dot((Vector2)platform.transform.position - start, delta) / delta.sqrMagnitude;
        switchCollider.enabled = fraction >= activationFraction && fraction <= activationFraction + 0.08f
            && fraction > previousFraction;
        previousFraction = fraction;
    }
}
