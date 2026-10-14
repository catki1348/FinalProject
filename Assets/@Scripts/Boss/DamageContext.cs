using UnityEngine;

// 공격자 문맥. 환경 피해는 기존 IDamageable 경로를 유지한다.
// OpenAI Codex (GPT-6), 2026-10-14.
public readonly struct DamageContext
{
    public readonly int BaseDamage;
    public readonly Collider2D AttackerBody;
    public DamageContext(int baseDamage, Collider2D attackerBody)
    { BaseDamage = baseDamage; AttackerBody = attackerBody; }
}
public interface IContextDamageable { void TakeDamage(DamageContext context); }
public static class DamageDelivery
{
    // 지원하는 대상만 공격자 정보를 수신하고 피해는 한 번만 전달된다.
    public static void Deliver(IDamageable target, int amount, Collider2D attacker)
    {
        if (amount <= 0) return;
        if (target is IContextDamageable contextual) contextual.TakeDamage(new DamageContext(amount, attacker));
        else target.TakeDamage(amount);
    }
}
