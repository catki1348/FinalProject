using UnityEngine;

// 몬스터가 플레이어(또는 targetLayer에 속한 대상)와 물리적으로 충돌하면 공격력만큼 피해를 입히는 스크립트
// 피해 판정만 담당하도록 스탯/패턴 스크립트와 분리한다(규칙 39 SRP)
[RequireComponent(typeof(MonsterStats))]
public class MonsterContactDamage : MonoBehaviour
{
    [SerializeField] private LayerMask targetLayer; // 피해를 줄 대상으로 인식할 레이어(태그 비교 대신 사용 - 규칙 41)

    private MonsterStats stats;

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

    // 충돌이 지속되는 동안에도 계속 시도 - 연속 피해 방지는 피격 측의 무적 시간(예: PlayerHealth.invincibleDuration)이 처리
    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDealDamage(collision.collider);
    }

    // targetLayer에 속하고 IDamageable을 구현한 대상에게만 공격력만큼 피해를 적용
    private void TryDealDamage(Collider2D other)
    {
        if (stats.IsDead) return;
        if (((1 << other.gameObject.layer) & targetLayer) == 0) return;

        if (other.attachedRigidbody != null &&
            other.attachedRigidbody.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(stats.Attack);
        }
    }
}
