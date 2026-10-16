using UnityEngine;

// Codex, model unknown, 2026-10-15. Effect independent of drop/lifetime behavior.
[RequireComponent(typeof(Collider2D))]
public sealed class FuryItem : MonoBehaviour
{
    [SerializeField] private AudioClip pickupSfx;
    private bool consumed;

    // A pickup cannot grant attack outside its owning area.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        var body = other.attachedRigidbody;
        var target = body != null ? body.gameObject : other.gameObject;
        if (!target.TryGetComponent<PlayerFuryBuff>(out var buff) || !buff.AddStack()) return;
        consumed = true;
        SoundManager.Instance.PlaySfx(pickupSfx);
        Destroy(gameObject);
    }
}
