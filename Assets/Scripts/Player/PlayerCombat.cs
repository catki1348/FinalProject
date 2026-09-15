using UnityEngine;

// 플레이어의 검 공격(근접)과 쿨다운이 있는 특수 액션을 담당하는 스크립트
[RequireComponent(typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Transform attackPoint;              // 검 공격 판정의 중심점(캐릭터 자식, 정면에 배치)
    [SerializeField] private float attackRange = 0.75f;          // 검 공격 판정 반경
    [SerializeField] private int attackDamage = 1;                // 검 공격 피해량
    [SerializeField] private LayerMask enemyLayer;                // 공격 판정에 포함될 레이어
    [SerializeField] private float attackCooldown = 0.4f;         // 검 공격 재사용 대기시간(초)
    [SerializeField] private float specialActionCooldown = 1f;    // 특수 액션 재사용 대기시간(초)

    private readonly Collider2D[] hitBuffer = new Collider2D[8]; // 공격 판정 결과를 담을 버퍼(매 공격마다 새로 할당하지 않기 위해 캐싱)
    private PlayerMovement movement;             // 바라보는 방향(FacingRight)을 참조하기 위한 캐시
    private float lastAttackTime = -999f;        // 마지막으로 검 공격을 사용한 시각(쿨다운 계산용)
    private float lastSpecialActionTime = -999f; // 마지막으로 특수 액션을 사용한 시각(쿨다운 계산용)

    // 시작 시 PlayerMovement 컴포넌트를 미리 캐싱
    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    // 매 프레임 입력을 읽어 좌클릭은 검 공격, 우클릭은 특수 액션을 시도
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryPerformSwordAttack();
        }

        if (Input.GetMouseButtonDown(1))
        {
            TryPerformSpecialAction();
        }
    }

    // 쿨다운이 끝났는지 확인한 뒤 검 공격을 실행(쿨다운 중이면 무시)
    private void TryPerformSwordAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        lastAttackTime = Time.time;

        PerformSwordAttack();
    }

    // attackPoint를 중심으로 한 원형 범위 안의 대상에게 피해를 입힘
    // attackPoint는 캐릭터의 자식이므로 Flip(스케일 반전) 시 위치가 자동으로 정면을 따라감
    private void PerformSwordAttack()
    {
        if (attackPoint == null) return;

        int hitCount = Physics2D.OverlapCircleNonAlloc(attackPoint.position, attackRange, hitBuffer, enemyLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (hitBuffer[i].attachedRigidbody != null &&
                hitBuffer[i].attachedRigidbody.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(attackDamage);
            }
        }
    }

    // 쿨다운이 끝났는지 확인한 뒤 특수 액션을 실행(쿨다운 중이면 무시)
    private void TryPerformSpecialAction()
    {
        if (Time.time - lastSpecialActionTime < specialActionCooldown) return;
        lastSpecialActionTime = Time.time;

        PerformSpecialAction();
    }

    // 실제 특수 액션의 내용은 비어있음 - 하위 클래스에서 override하여 구현하는 확장 지점
    protected virtual void PerformSpecialAction()
    {
    }

    // attackPoint 기준 공격 판정 범위를 씬 뷰에서 시각적으로 확인하기 위한 디버그용 기즈모
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}
