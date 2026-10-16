using System;
using UnityEngine;

// Codex, model unknown, 2026-10-15. Zone-scoped additive attack; never edits equipment.
[RequireComponent(typeof(PlayerStats), typeof(PlayerHealth))]
public sealed class PlayerFuryBuff : MonoBehaviour
{
    public event Action<long, long> OnChanged;
    public long Stacks { get; private set; }
    public long AttackBonus => Stacks * 10L;
    public bool AreaActive { get; private set; }
    private PlayerStats stats;
    private PlayerHealth health;

    // Cache dependencies and clear the zone bonus on player death.
    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        health = GetComponent<PlayerHealth>();
        health.OnDied += HandleDeath;
    }

    // Only arithmetic overflow has a ceiling; no gameplay stack cap or expiry.
    public bool AddStack()
    {
        if (!AreaActive) return false;
        if (Stacks < long.MaxValue / 10L) Stacks++;
        stats.SetFuryAttackBonus(AttackBonus);
        OnChanged?.Invoke(Stacks, AttackBonus);
        return true;
    }

    // Area changes leave the independent speed timer untouched.
    public void SetAreaActive(bool active)
    {
        AreaActive = active;
        if (!active) Clear();
    }

    private void HandleDeath() { SetAreaActive(false); }

    // Scene reload/disable cannot leave a bonus on a surviving player object.
    public void Clear()
    {
        Stacks = 0;
        if (stats != null) stats.SetFuryAttackBonus(0);
        OnChanged?.Invoke(0, 0);
    }

    private void OnDisable() { SetAreaActive(false); }
    private void OnDestroy() { if (health != null) health.OnDied -= HandleDeath; }
}
