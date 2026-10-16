using UnityEngine;

// Codex (GPT-6), 2026-10-15. A reusable support; timing belongs to SuperRunManager.
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
public sealed class SequentialFallingPlatform : MonoBehaviour
{
    [SerializeField] private float fallAcceleration = 20f;
    [SerializeField] private float hideBelowDistance = 18f;
    private BoxCollider2D support;
    private SpriteRenderer visual;
    private Vector3 origin;
    private bool cached;
    public bool HasFallen { get; private set; }

    // Cache before the first fall/reset, including when tests call us before Awake.
    private void Cache()
    {
        if (cached) return;
        support = GetComponent<BoxCollider2D>();
        visual = GetComponent<SpriteRenderer>();
        origin = transform.localPosition;
        cached = true;
    }
    private void Awake() { Cache(); }

    // Disable support at the deadline. Visual motion never carries the player or pushes items.
    public void Fall(float elapsedSinceFall)
    {
        Cache();
        HasFallen = true;
        support.enabled = false;
        float drop = .5f * fallAcceleration * elapsedSinceFall * elapsedSinceFall;
        transform.localPosition = origin + Vector3.down * Mathf.Min(drop, hideBelowDistance);
        if (visual != null) visual.enabled = drop < hideBelowDistance;
    }

    // Restore the exact serialized pose for a fresh attempt; no contact-based timers exist.
    public void ResetSupport()
    {
        Cache();
        HasFallen = false;
        transform.localPosition = origin;
        support.enabled = true;
        if (visual != null) visual.enabled = true;
    }
}
