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
    [SerializeField, Min(0f)] private float attackKnockbackSpeed = 2f;
    [SerializeField, Min(0f)] private float attackKnockbackDuration = 0.12f;

    private readonly Collider2D[] hitBuffer = new Collider2D[256]; // Dense fury crowds: preallocate for 105 monsters with multiple colliders.
    private PlayerMovement movement;             // 바라보는 방향(FacingRight)을 참조하기 위한 캐시
    private PlayerStats stats;                   // 공격력(FinalAttack, 장비 반영)을 참조하기 위한 캐시
    private Collider2D attackerBody;
    private readonly Rigidbody2D[] hitBodies = new Rigidbody2D[256];
    private Vector3 attackPointBaseLocalPos;      // attackPoint의 원래(오른쪽 기준) 로컬 위치 - 좌우 반전 시 x부호만 뒤집어 재사용
    private float lastAttackTime = -999f;        // 마지막으로 검 공격을 사용한 시각(쿨다운 계산용)
    private float lastSpecialActionTime = -999f; // 마지막으로 특수 액션을 사용한 시각(쿨다운 계산용)
    private float attackVisualTimer;             // 검 스윙 연출 남은 시간(0 이하면 종료) - PlayerSpriteAnimator가 IsAttacking/AttackProgress01로 참조
    private int swingHitCount;
    private float previousSwingProgress;

    public bool IsAttacking => attackVisualTimer > 0f; // 현재 검 스윙 연출 중인지(외부 조회용, 예: 스프라이트 상태 전환)

    // 스윙 연출 진행률(0~1). 스프라이트 애니메이터가 이 값으로 여러 프레임 중 몇 번째를 보여줄지 계산한다
    public float AttackProgress01 =>
        attackVisualDuration > 0f ? 1f - Mathf.Clamp01(attackVisualTimer / attackVisualDuration) : 0f;

    // 시작 시 PlayerMovement/PlayerStats 컴포넌트를 미리 캐싱
    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stats = GetComponent<PlayerStats>();
        attackerBody = GetComponent<Collider2D>();
        if (attackPoint != null) attackPointBaseLocalPos = attackPoint.localPosition;
    }

    // 매 프레임 입력을 읽어 좌클릭은 검 공격, 우클릭은 특수 액션을 시도
    private void Update()
    {
        UpdateAttackPointFacing();

        if (attackVisualTimer > 0f)
        {
            attackVisualTimer = Mathf.Max(0f, attackVisualTimer - Time.deltaTime);
            PerformSwordAttack();
        }

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
        swingHitCount = 0;
        previousSwingProgress = 0f;
        SoundManager.Instance.PlaySfx(attackSfx);

        PerformSwordAttack();
    }

    // OpenAI Codex (GPT-6), 2026-10-16: 기존 전방 판정과 스윙 호를 함께 검사한다.
    // 프레임 사이의 호도 촘촘히 검사해 낮은 프레임에서도 경로 중간의 적을 놓치지 않는다.
    private void PerformSwordAttack()
    {
        if (attackPoint == null) return;

        CheckSwordHits(attackPoint.position, attackRange);
        float progress = AttackProgress01;
        Vector2 pivot = new Vector2(transform.position.x, attackPoint.position.y);
        float reach = Mathf.Abs(attackPoint.position.x - transform.position.x);
        float radius = Mathf.Max(0.05f, attackRange * 0.5f);
        int steps = Mathf.Max(1, Mathf.CeilToInt((progress - previousSwingProgress) * Mathf.PI * reach / radius));
        for (int step = 0; step <= steps; step++)
        {
            float t = Mathf.Lerp(previousSwingProgress, progress, (float)step / steps);
            float angle = Mathf.Lerp(90f, -90f, t) * Mathf.Deg2Rad;
            float sign = movement.FacingRight ? 1f : -1f;
            Vector2 tip = pivot + new Vector2(Mathf.Cos(angle) * sign, Mathf.Sin(angle)) * reach;
            // 검 끝뿐 아니라 칼날 안쪽도 포함한다.
            int bladeSteps = Mathf.Max(1, Mathf.CeilToInt(reach / radius));
            for (int blade = 1; blade <= bladeSteps; blade++)
                CheckSwordHits(Vector2.Lerp(pivot, tip, (float)blade / bladeSteps), radius);
        }
        previousSwingProgress = progress;
    }

    // 중복 기록은 프레임마다 지우지 않고 스윙 시작 시에만 초기화한다.
    private void CheckSwordHits(Vector2 center, float radius)
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(center, radius, hitBuffer, enemyLayer);
        for (int i = 0; i < hitCount; i++)
        {
            // OpenAI Codex (GPT-6), 2026-10-14: 검 한 번당 동일 몸에 단일 피해.
            Rigidbody2D targetBody = hitBuffer[i].attachedRigidbody;
            if (targetBody == null || targetBody.gameObject == gameObject) continue;
            bool duplicate = false;
            for (int j = 0; j < swingHitCount; j++) if (hitBodies[j] == targetBody) { duplicate = true; break; }
            if (duplicate) continue;
            if (targetBody.TryGetComponent<IDamageable>(out var damageable))
            {
                if (swingHitCount >= hitBodies.Length) continue;
                hitBodies[swingHitCount++] = targetBody;
                DamageDelivery.Deliver(damageable, stats.FinalAttack, attackerBody);
                if (stats.FinalAttack > 0 && targetBody.TryGetComponent<MonsterController>(out var monster))
                    monster.ApplyKnockback(movement.FacingRight ? 1f : -1f,
                        attackKnockbackSpeed, attackKnockbackDuration);
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
