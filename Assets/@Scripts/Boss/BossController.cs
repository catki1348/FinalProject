using System.Collections;
using UnityEngine;

// 스테이지 1 보스(Ancient Treant Guardian)의 행동을 담당하는 스크립트.
// - 플레이어 쪽으로 좌우 이동(추적)
// - 근접하면 양손을 번갈아 주먹질(일반 공격)
// - 일정 쿨다운마다 플레이어 쪽으로 점프해 내려찍기(특수 공격, 지면 충격파)
// - 짧은 시간에 큰 피해가 누적되면 그로기(스턴) 상태가 되어 잠시 무방비
// 체력/사망은 Boss.cs가 담당하고(SRP, 규칙 35), 이 스크립트는 Boss의 이벤트를 구독해 행동만 결정한다.
// 원점(transform.position)은 보스의 발밑 중앙이며, 애니메이션은 자식 Visual의 Animator가 재생한다.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Boss))]
public class BossController : MonoBehaviour
{
    // 보스의 현재 행동 상태(외부 조회 및 디버그용)
    public enum BossState { Idle, Chase, Attack, Special, Groggy, Dead }

    [Header("참조")]
    [SerializeField] private Animator animator;      // 자식 Visual의 Animator(Boss_Stage1_TreantAnimator)
    [SerializeField] private Transform visual;        // 좌우 반전 대상(자식 Visual)
    [SerializeField] private LayerMask targetLayer;   // 피해를 줄 대상 레이어(플레이어가 속한 Default)
    [SerializeField] private LayerMask groundLayer;   // 바닥 판정 레이어(Ground)

    [Header("이동")]
    [SerializeField] private float moveSpeed = 2.5f;      // 추적 이동 속도
    [SerializeField] private float detectRange = 16f;     // 이 거리 안의 플레이어만 추적/공격
    [SerializeField] private float stopDistance = 1.8f;   // 이 거리 안이면 더 다가가지 않고 멈춤
    [SerializeField] private float edgeProbeDistance = 1f; // 진행 방향으로 이만큼 앞의 바닥 유무를 확인해 발판 끝에서 멈춤

    [Header("일반 공격 (양손 번갈아 주먹질)")]
    [SerializeField] private float meleeRange = 2.6f;         // 일반 공격을 시작하는 거리
    [SerializeField] private int punchDamage = 10;            // 주먹 1회 피해량
    [SerializeField] private float punchReach = 2.6f;         // 주먹 판정 상자의 전방 길이
    [SerializeField] private float punchHeight = 3f;          // 주먹 판정 상자의 높이(발밑부터)
    [SerializeField] private float firstPunchTime = 0.2f;     // 공격 시작 후 왼손 타격이 들어가는 시각(클립: 왼손 준비 0.2초 뒤)
    [SerializeField] private float secondPunchTime = 0.55f;   // 오른손 타격이 들어가는 시각
    [SerializeField] private float attackDuration = 0.72f;    // Boss_Attack 클립 전체 길이
    [SerializeField] private float attackCooldown = 1.2f;     // 일반 공격 종료 후 재사용 대기시간

    [Header("특수 공격 (점프 내려찍기)")]
    [SerializeField] private float specialCooldown = 8f;      // 특수 공격 재사용 대기시간
    [SerializeField] private float firstSpecialDelay = 4f;    // 전투 시작 후 첫 특수 공격까지의 대기시간
    [SerializeField] private float specialMinRange = 3f;      // 이 거리보다 가까우면 특수 공격을 쓰지 않음(일반 공격 우선)
    [SerializeField] private float crouchTime = 0.35f;        // 도약 전 웅크리는 시간(클립: 웅크림 프레임 길이)
    [SerializeField] private float airTime = 0.6f;            // 체공 시간(클립: 공중 프레임 길이)
    [SerializeField] private float specialDuration = 1.55f;   // Boss_SpecialSlam 클립 전체 길이
    [SerializeField] private float leapGravityScale = 4f;     // 도약 중 중력 배율(짧은 시간에 높이 뛰어오르게 함)
    [SerializeField] private float maxLeapDistance = 9f;      // 한 번에 도약할 수 있는 최대 수평 거리
    [SerializeField] private float landingStandoff = 2f;      // 플레이어 위치보다 이만큼 앞에서 착지(몸이 플레이어를 깔아뭉개지 않게 함)
    [SerializeField] private int slamDamage = 25;             // 내려찍기 충격파 피해량
    [SerializeField] private float slamRadius = 3.6f;         // 충격파 반경
    [SerializeField] private float slamHeightTolerance = 0.2f; // 충격파가 닿는 높이(플레이어 발이 이 높이 이상이면 점프로 회피 성공. 플레이어 점프 최고점이 약 0.55라 착지 직전에 타이밍 맞춰 점프해야 함)

    [Header("그로기")]
    [SerializeField] private int staggerThreshold = 30;        // 짧은 시간에 누적된 피해가 이 값 이상이면 그로기
    [SerializeField] private int bigHitDamage = 20;            // 단일 피격이 이 값 이상이면 즉시 그로기
    [SerializeField] private float staggerDecayPerSecond = 5f; // 누적 피해가 시간당 줄어드는 양
    [SerializeField] private float groggyDuration = 3f;        // 그로기 지속 시간
    [SerializeField] private float groggyImmunity = 4f;        // 그로기가 풀린 뒤 다시 그로기에 걸리지 않는 시간(스턴락 방지)

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsGroggyHash = Animator.StringToHash("IsGroggy");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int SpecialAttackHash = Animator.StringToHash("SpecialAttack");
    private static readonly int DieHash = Animator.StringToHash("Die");

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Boss boss;
    private BossSound sound;                             // 보스 사운드(없으면 소리 없이 동작)
    private Transform target;                                // 추적 대상(GameManager에 등록된 플레이어)
    private float defaultGravityScale;                       // 도약 후 복구할 원래 중력 배율
    private float baseVisualScaleX;                          // Visual의 원래 X 스케일 크기(좌우 반전에 사용)
    private bool facingRight = true;
    private bool lastWalking;                                // 마지막으로 Animator에 보낸 IsWalking 값(불필요한 SetBool 방지)
    private float nextAttackTime;
    private float nextSpecialTime;
    private float staggerGauge;                              // 누적 피해량(그로기 게이지)
    private float groggyImmuneUntil;
    private readonly Collider2D[] hitBuffer = new Collider2D[8]; // 판정 결과 버퍼(매번 새로 할당하지 않기 위해 캐싱 - 규칙 39)
    private readonly Rigidbody2D[] hitBodies = new Rigidbody2D[8]; // 한 번의 판정에서 같은 대상을 중복 타격하지 않기 위한 목록

    public BossState State { get; private set; } = BossState.Idle;

    // 컴포넌트 캐싱 및 이벤트 구독
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        boss = GetComponent<Boss>();
        TryGetComponent(out sound);
        defaultGravityScale = rb.gravityScale;
        if (visual != null) baseVisualScaleX = Mathf.Abs(visual.localScale.x);
        boss.OnDamaged += HandleDamaged;
        boss.OnDied += HandleDied;
    }

    private void Start()
    {
        nextSpecialTime = Time.time + firstSpecialDelay;
    }

    private void OnDestroy()
    {
        if (boss == null) return;
        boss.OnDamaged -= HandleDamaged;
        boss.OnDied -= HandleDied;
    }

    // 매 프레임 그로기 게이지를 줄이고, 자유로운 상태(Idle/Chase)일 때만 다음 행동을 결정
    private void Update()
    {
        if (State == BossState.Dead) return;

        staggerGauge = Mathf.Max(0f, staggerGauge - staggerDecayPerSecond * Time.deltaTime);

        if (State != BossState.Idle && State != BossState.Chase) return;

        if (target == null)
        {
            target = GameManager.Instance.PlayerTransform;
            if (target == null) return;
        }

        float dx = target.position.x - transform.position.x;
        float dist = Mathf.Abs(dx);

        if (dist > detectRange)
        {
            SetState(BossState.Idle);
            return;
        }

        if (sound != null) sound.StartBattle();
        Face(dx > 0f);

        if (Time.time >= nextSpecialTime && dist >= specialMinRange && IsGrounded())
        {
            StartCoroutine(SpecialRoutine());
        }
        else if (dist <= meleeRange && Time.time >= nextAttackTime)
        {
            StartCoroutine(AttackRoutine());
        }
        else if (dist > stopDistance && HasGroundAt(transform.position.x + (facingRight ? 1f : -1f) * edgeProbeDistance))
        {
            // 진행 방향 발밑에 바닥이 있을 때만 추적(발판 끝에서 낙사하지 않도록 멈춤)
            SetState(BossState.Chase);
        }
        else
        {
            SetState(BossState.Idle);
        }
    }

    // 물리 갱신에서 추적 이동만 적용(공격/특수/그로기 중에는 각 루틴이 속도를 직접 제어)
    private void FixedUpdate()
    {
        if (State == BossState.Chase)
        {
            rb.linearVelocity = new Vector2((facingRight ? 1f : -1f) * moveSpeed, rb.linearVelocity.y);
        }
        else if (State == BossState.Idle || State == BossState.Attack || State == BossState.Groggy || State == BossState.Dead)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    // 일반 공격: 왼손 타격 → 오른손 타격 두 번의 주먹 판정을 클립 타이밍에 맞춰 적용
    private IEnumerator AttackRoutine()
    {
        SetState(BossState.Attack);
        animator.SetTrigger(AttackHash);

        yield return new WaitForSeconds(firstPunchTime);
        if (sound != null) sound.Play(BossSfx.Attack);
        DealPunchDamage();
        yield return new WaitForSeconds(secondPunchTime - firstPunchTime);
        if (sound != null) sound.Play(BossSfx.Attack);
        DealPunchDamage();
        yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - secondPunchTime));

        nextAttackTime = Time.time + attackCooldown;
        SetState(BossState.Idle);
    }

    // 특수 공격: 웅크림 → 플레이어 위치로 도약 → 착지 시 충격파. 타이밍은 Boss_SpecialSlam 클립(웅크림→공중→내려찍기)과 맞춘다
    private IEnumerator SpecialRoutine()
    {
        SetState(BossState.Special);
        animator.SetTrigger(SpecialAttackHash);

        yield return new WaitForSeconds(crouchTime);

        // 도약: 체공 시간 동안 현재 플레이어 위치까지 수평 이동, 같은 높이로 착지하도록 초기 상승 속도를 계산
        float dx = target != null ? target.position.x - transform.position.x : 0f;
        // 플레이어 몸 위에 그대로 내려앉지 않도록 landingStandoff만큼 앞에서 착지(충격파 반경 안이라 피해는 그대로 닿는다)
        dx = Mathf.Abs(dx) > landingStandoff ? dx - Mathf.Sign(dx) * landingStandoff : 0f;
        dx = Mathf.Clamp(dx, -maxLeapDistance, maxLeapDistance);
        // 착지 지점에 바닥이 없으면(발판 밖) 바닥이 있는 지점까지 도약 거리를 줄인다
        while (Mathf.Abs(dx) > 0.5f && !HasGroundAt(transform.position.x + dx + Mathf.Sign(dx) * edgeProbeDistance))
        {
            dx -= Mathf.Sign(dx) * 0.5f;
        }
        float vx = dx / airTime;
        rb.gravityScale = leapGravityScale;
        float gravity = Mathf.Abs(Physics2D.gravity.y) * leapGravityScale;
        rb.linearVelocity = new Vector2(vx, gravity * airTime * 0.5f);
        Face(vx >= 0f);

        // 지면에 닿을 때까지 대기(상승 직후의 오판을 막기 위해 최소 시간 이후부터 판정, 최대 시간 초과 시 강제 종료)
        float elapsed = 0f;
        while (elapsed < airTime * 2.5f)
        {
            elapsed += Time.deltaTime;
            if (elapsed > airTime * 0.5f && rb.linearVelocity.y <= 0f && IsGrounded()) break;
            yield return null;
        }

        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = Vector2.zero;
        if (sound != null) sound.Play(BossSfx.SpecialSlam);
        DealSlamDamage();

        float remaining = specialDuration - crouchTime - elapsed;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        nextSpecialTime = Time.time + specialCooldown;
        nextAttackTime = Time.time + attackCooldown;
        SetState(BossState.Idle);
    }

    // 그로기: 모든 행동을 멈추고 일정 시간 무방비 상태로 있다가 회복
    private IEnumerator GroggyRoutine()
    {
        SetState(BossState.Groggy);
        if (sound != null) sound.Play(BossSfx.Groggy);
        // 공격/도약 도중이거나 뒤에서 맞아 반대쪽을 보고 있을 수 있으므로, 그로기 연출은 항상 플레이어 쪽을 향한 상태로 재생
        if (target != null) Face(target.position.x > transform.position.x);
        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = Vector2.zero;
        animator.ResetTrigger(AttackHash);
        animator.ResetTrigger(SpecialAttackHash);
        animator.SetBool(IsGroggyHash, true);

        yield return new WaitForSeconds(groggyDuration);

        animator.SetBool(IsGroggyHash, false);
        groggyImmuneUntil = Time.time + groggyImmunity;
        staggerGauge = 0f;
        nextAttackTime = Time.time + 0.5f;
        SetState(BossState.Idle);
    }

    // 피격 시 피해를 그로기 게이지에 누적. 큰 한 방이거나 누적치가 임계값을 넘으면 그로기 진입
    private void HandleDamaged(int amount)
    {
        if (State == BossState.Dead || State == BossState.Groggy) return;

        staggerGauge += amount;
        bool shouldStagger = amount >= bigHitDamage || staggerGauge >= staggerThreshold;
        if (!shouldStagger || Time.time < groggyImmuneUntil) return;

        StopAllCoroutines();
        StartCoroutine(GroggyRoutine());
    }

    // 사망: 모든 행동을 멈추고 사망 애니메이션(Boss_Death: 휘청임 → 쓰러짐 → 누운 채 정지)을 한 번 재생
    private void HandleDied()
    {
        StopAllCoroutines();
        rb.gravityScale = defaultGravityScale;
        rb.linearVelocity = Vector2.zero;
        animator.ResetTrigger(AttackHash);
        animator.ResetTrigger(SpecialAttackHash);
        animator.SetBool(IsWalkingHash, false);
        animator.SetBool(IsGroggyHash, false);
        animator.SetTrigger(DieHash);
        // 쓰러진 보스의 몸이 종료점으로 가는 길을 막지 않도록 물리를 멈추고 충돌을 끈다(모습은 그대로 남김)
        rb.bodyType = RigidbodyType2D.Kinematic;
        bodyCollider.enabled = false;
        SetState(BossState.Dead);
    }

    // 바라보는 방향 앞쪽 상자 범위 안의 대상에게 주먹 피해를 적용
    private void DealPunchDamage()
    {
        Vector2 feet = new Vector2(transform.position.x, bodyCollider.bounds.min.y);
        float dir = facingRight ? 1f : -1f;
        Vector2 center = feet + new Vector2(dir * punchReach * 0.5f, punchHeight * 0.5f);
        int count = Physics2D.OverlapBoxNonAlloc(center, new Vector2(punchReach, punchHeight), 0f, hitBuffer, targetLayer);
        ApplyDamage(count, punchDamage, false, feet.y);
    }

    // 착지 지점 주변 원형 범위에 충격파 피해를 적용. 플레이어 발이 충분히 높으면(점프 회피) 피해를 받지 않는다
    private void DealSlamDamage()
    {
        Vector2 feet = new Vector2(transform.position.x, bodyCollider.bounds.min.y);
        int count = Physics2D.OverlapCircleNonAlloc(feet, slamRadius, hitBuffer, targetLayer);
        ApplyDamage(count, slamDamage, true, feet.y);
    }

    // 판정 결과 중 IDamageable을 가진 대상에게 한 번씩만 피해를 준다(자기 자신 제외)
    private void ApplyDamage(int count, int damage, bool checkHeight, float feetY)
    {
        int uniqueCount = 0;
        for (int i = 0; i < count; i++)
        {
            Rigidbody2D body = hitBuffer[i].attachedRigidbody;
            if (body == null || body == rb) continue;
            if (checkHeight && hitBuffer[i].bounds.min.y > feetY + slamHeightTolerance) continue;

            bool duplicate = false;
            for (int j = 0; j < uniqueCount; j++) if (hitBodies[j] == body) { duplicate = true; break; }
            if (duplicate) continue;

            if (body.TryGetComponent<IDamageable>(out var damageable))
            {
                hitBodies[uniqueCount++] = body;
                damageable.TakeDamage(damage);
            }
        }
    }

    // 발밑에 바닥 레이어가 있는지 확인
    private bool IsGrounded()
    {
        Bounds b = bodyCollider.bounds;
        Vector2 center = new Vector2(b.center.x, b.min.y - 0.05f);
        return Physics2D.OverlapBox(center, new Vector2(b.size.x * 0.8f, 0.12f), 0f, groundLayer) != null;
    }

    // 주어진 x 좌표 발밑 근처에 바닥이 있는지 확인(발판 가장자리 낙사 방지용)
    private bool HasGroundAt(float x)
    {
        Vector2 origin = new Vector2(x, bodyCollider.bounds.min.y + 0.5f);
        return Physics2D.Raycast(origin, Vector2.down, 1.5f, groundLayer).collider != null;
    }

    // 상태를 바꾸고 이동 애니메이션(IsWalking)을 함께 갱신
    private void SetState(BossState next)
    {
        State = next;
        bool walking = next == BossState.Chase;
        if (walking != lastWalking)
        {
            lastWalking = walking;
            animator.SetBool(IsWalkingHash, walking);
        }
    }

    // 스프라이트는 오른쪽을 보는 기준으로 그려져 있으므로 왼쪽을 볼 때 Visual의 X 스케일을 반전
    private void Face(bool right)
    {
        if (right == facingRight || visual == null) return;
        facingRight = right;
        Vector3 s = visual.localScale;
        s.x = baseVisualScaleX * (right ? 1f : -1f);
        visual.localScale = s;
    }
}
