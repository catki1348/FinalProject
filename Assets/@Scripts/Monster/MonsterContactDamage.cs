using System;
using UnityEngine;

// 몬스터가 플레이어(또는 targetLayer에 속한 대상)와 물리적으로 충돌하면 공격력만큼 피해를 입히는 스크립트
// 피해 판정만 담당하도록 스탯/패턴 스크립트와 분리한다(규칙 35 SRP)
[RequireComponent(typeof(MonsterStats))]
public class MonsterContactDamage : MonoBehaviour
{
    [SerializeField] private LayerMask targetLayer;     // 피해를 줄 대상으로 인식할 레이어(태그 비교 대신 사용 - 규칙 37)
    [SerializeField] private float attackCooldown = 1f; // 충돌 공격 재사용 간격(초) - 붙어 있는 동안 이 간격마다 공격

    private MonsterStats stats;
    private float lastAttackTime = -999f;

    // 공격이 들어간 순간 발행(인자: 대상이 있는 방향, +1 = 오른쪽 / -1 = 왼쪽) - 공격 모션 재생용(MonsterSpriteAnimator)
    public event Action<float> OnAttack;

    // 시작 시 같은 오브젝트의 MonsterStats를 캐싱(공격력 조회용)
    private void Awake()
    {
        stats = GetComponent<MonsterStats>();
    }

    // 충돌이 처음 시작되는 순간 피해 판정 시도
    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDealDamage(collision.collider);
    }

    // 충돌이 지속되는 동안에도 계속 시도 - 실제 공격은 attackCooldown 간격으로만 발생
    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDealDamage(collision.collider);
    }

    // targetLayer에 속하고 IDamageable을 구현한 대상에게만 공격력만큼 피해를 적용하고 공격 이벤트를 발행
    private void TryDealDamage(Collider2D other)
    {
        if (stats.IsDead) return;
        if (Time.time - lastAttackTime < attackCooldown) return;
        if (((1 << other.gameObject.layer) & targetLayer) == 0) return;

        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(stats.Attack);
            lastAttackTime = Time.time;
            OnAttack?.Invoke(Mathf.Sign(other.transform.position.x - transform.position.x));
        }
    }
}
