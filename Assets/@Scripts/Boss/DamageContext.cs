using UnityEngine;

// 공격자 문맥. 환경 피해는 기존 IDamageable 경로를 유지한다.
// OpenAI Codex (GPT-6), 2026-10-14.
public readonly struct DamageContext
{
    public readonly int BaseDamage;
    public readonly Collider2D AttackerBody;
    public readonly int AttackId; // 공격 1회 식별자(0 = 식별 불가). 같은 공격이 여러 콜라이더에 닿아도 동일 값
    public DamageContext(int baseDamage, Collider2D attackerBody, int attackId = 0)
    { BaseDamage = baseDamage; AttackerBody = attackerBody; AttackId = attackId; }
}
public interface IContextDamageable { void TakeDamage(DamageContext context); }
public static class DamageDelivery
{
    private static int nextAttackId;
    // 지원하는 대상만 공격자 정보를 수신하고 피해는 한 번만 전달된다. 호출마다 새 공격 식별자를 부여한다.
    // Claude Sonnet 5.5, 2026-10-16: 방어 성공 넉백용 공격 식별자 추가.
    public static void Deliver(IDamageable target, int amount, Collider2D attacker)
    {
        if (amount <= 0) return;
        if (target is IContextDamageable contextual) contextual.TakeDamage(new DamageContext(amount, attacker, ++nextAttackId));
        else target.TakeDamage(amount);
    }
}
