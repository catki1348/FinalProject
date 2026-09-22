using UnityEngine;

// 플레이어의 검 공격(근접)과 쿨다운이 있는 특수 액션을 담당하는 스크립트
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private Transform attackPoint;              // 검 공격 판정의 중심점(캐릭터 자식, 정면에 배치)
    [SerializeField] private float attackRange = 0.75f;          // 검 공격 판정 반경
    [SerializeField] private LayerMask enemyLayer;                // 공격 판정에 포함될 레이어
    [SerializeField] private float attackCooldown = 0.4f;         // 검 공격 재사용 대기시간(초)
    [SerializeField] private float attackVisualDuration = 0.3f;   // 검 스윙 연출(스프라이트 애니메이션)을 보여줄 시간(초)
    [SerializeField] private float specialActionCooldown = 1f;    // 특수 액션 재사용 대기시간(초)
    [SerializeField] private AudioClip attackSfx;                 // 검 공격 시 재생할 효과음(비워두면 재생하지 않음)

    private readonly Collider2D[] hitBuffer = new Collider2D[8]; // 공격 판정 결과를 담을 버퍼(매 공격마다 새로 할당하지 않기 위해 캐싱)
    private PlayerMovement movement;             // 바라보는 방향(FacingRight)을 참조하기 위한 캐시
    private PlayerStats stats;                   // 공격력(FinalAttack, 장비 반영)을 참조하기 위한 캐시
    private Vector3 attackPointBaseLocalPos;      // attackPoint의 원래(오른쪽 기준) 로컬 위치 - 좌우 반전 시 x부호만 뒤집어 재사용
    private float lastAttackTime = -999f;        // 마지막으로 검 공격을 사용한 시각(쿨다운 계산용)
    private float lastSpecialActionTime = -999f; // 마지막으로 특수 액션을 사용한 시각(쿨다운 계산용)
    private float attackVisualTimer;             // 검 스윙 연출 남은 시간(0 이하면 종료) - PlayerSpriteAnimator가 IsAttacking/AttackProgress01로 참조

    public bool IsAttacking => attackVisualTimer > 0f; // 현재 검 스윙 연출 중인지(외부 조회용, 예: 스프라이트 상태 전환)

    // 스윙 연출 진행률(0~1). 스프라이트 애니메이터가 이 값으로 여러 프레임 중 몇 번째를 보여줄지 계산한다
    public float AttackProgress01 =>
        attackVisualDuration > 0f ? 1f - Mathf.Clamp01(attackVisualTimer / attackVisualDuration) : 0f;

    // 시작 시 PlayerMovement/PlayerStats 컴포넌트를 미리 캐싱
    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stats = GetComponent<PlayerStats>();
        if (attackPoint != null) attackPointBaseLocalPos = attackPoint.localPosition;
    }

    // 매 프레임 입력을 읽어 좌클릭은 검 공격, 우클릭은 특수 액션을 시도
    private void Update()
    {
        UpdateAttackPointFacing();

        if (attackVisualTimer > 0f) attackVisualTimer -= Time.deltaTime;

        if (Input.GetMouseButtonDown(0))
        {
            TryPerformSwordAttack();
        }

        if (Input.GetMouseButtonDown(1))
        {
            TryPerformSpecialAction();
        }
    }

    // 이 스크립트는 스프라이트를 스케일 반전이 아니라 좌우 전용 스프라이트 교체로 뒤집으므로(PlayerSpriteAnimator 참고),
    // attackPoint 위치도 캐릭터를 따라 자동으로 뒤집히지 않는다 - 바라보는 방향에 맞춰 매 프레임 x부호를 직접 갱신한다
    private void UpdateAttackPointFacing()
    {
        if (attackPoint == null) return;

        float sign = movement.FacingRight ? 1f : -1f;
        attackPoint.localPosition = new Vector3(
            Mathf.Abs(attackPointBaseLocalPos.x) * sign,
            attackPointBaseLocalPos.y,
            attackPointBaseLocalPos.z);
    }

    // 쿨다운이 끝났는지 확인한 뒤 검 공격을 실행(쿨다운 중이면 무시)
    private void TryPerformSwordAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown) return;
        lastAttackTime = Time.time;
        attackVisualTimer = attackVisualDuration;
        SoundManager.Instance.PlaySfx(attackSfx);

        PerformSwordAttack();
    }

    // attackPoint를 중심으로 한 원형 범위 안의 대상에게 피해를 입힘
    private void PerformSwordAttack()
    {
        if (attackPoint == null) return;

        int hitCount = Physics2D.OverlapCircleNonAlloc(attackPoint.position, attackRange, hitBuffer, enemyLayer);
        for (int i = 0; i < hitCount; i++)
        {
            if (hitBuffer[i].attachedRigidbody != null &&
                hitBuffer[i].attachedRigidbody.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(stats.FinalAttack);
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
