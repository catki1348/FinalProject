using System.Collections;
using UnityEngine;

// 스테이지 2 보스(Stone Serpent, 돌 뱀)의 행동을 담당하는 스크립트.
// 스테이지 1 보스(BossController)의 기본 구조를 그대로 계승한다.
// - 플레이어 쪽으로 좌우 이동(추적), 발판 끝에서는 멈춤
// - 짧은 시간에 큰 피해가 누적되면 그로기(스턴), 사망 시 사망 애니메이션 후 충돌 해제
// 여기에 공격 패턴을 4종으로 확장했다.
// - 일반 공격 1: 물기(Bite)      - 가까운 거리, 머리를 앞으로 내질러 전방 중단 판정 1회
// - 일반 공격 2: 꼬리치기(Tail)   - 조금 먼 거리, 꼬리로 전방 바닥을 쓸어 지면 판정 1회(점프로 회피 가능)
// - 특수 공격 1: 독 브레스        - 입에서 전방으로 독 가스를 일정 시간 분사, 범위 안에 있으면 주기적으로 피해
// - 특수 공격 2: 석화 광선        - 눈에 빛을 모은 뒤 플레이어 방향으로 광선 발사, 맞으면 피해 + 일정 시간 석화(행동 불가)
// 체력/사망은 Boss.cs가 담당하고(SRP, 규칙 39), 이 스크립트는 Boss의 이벤트를 구독해 행동만 결정한다.
// 원점(transform.position)은 보스의 발밑(똬리 아래) 중앙이며, 애니메이션은 자식 Visual의 Animator가 재생한다.
// 스프라이트는 모두 오른쪽을 보는 옆모습 기준이며, 왼쪽을 볼 때는 Visual의 X 스케일을 반전한다.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Boss))]
public class StoneSerpentController : MonoBehaviour
{
    // 보스의 현재 행동 상태(외부 조회 및 디버그용)
    public enum SerpentState { Idle, Chase, Bite, TailSwipe, PoisonBreath, PetrifyGaze, Groggy, Dead }

    [Header("참조")]
    [SerializeField] private Animator animator;          // 자식 Visual의 Animator(Boss_Stage2_StoneSerpentAnimator)
    [SerializeField] private Transform visual;            // 좌우 반전 대상(자식 Visual)
    [SerializeField] private LayerMask targetLayer;       // 피해를 줄 대상 레이어(플레이어가 속한 Default)
    [SerializeField] private LayerMask groundLayer;       // 바닥 판정 레이어(Ground)
    [SerializeField] private SpriteRenderer breathVfx;    // 독 브레스 이펙트(자식, 평소 비활성). 원점은 입 위치, 오른쪽으로 뻗는 스프라이트
    [SerializeField] private SpriteRenderer gazeBeamVfx;  // 석화 광선 이펙트(자식, 평소 비활성). 피벗은 왼쪽 끝 중앙, 오른쪽으로 뻗는 스프라이트

    [Header("이동")]
    [SerializeField] private float moveSpeed = 2.2f;       // 추적 이동 속도(트렌트보다 약간 느리게 - 원거리 패턴이 있어서)
    [SerializeField] private float detectRange = 16f;      // 이 거리 안의 플레이어만 추적/공격
    [SerializeField] private float stopDistance = 2f;      // 이 거리 안이면 더 다가가지 않고 멈춤
    [SerializeField] private float edgeProbeDistance = 1f; // 진행 방향으로 이만큼 앞의 바닥 유무를 확인해 발판 끝에서 멈춤

    [Header("일반 공격 1 - 물기")]
    [SerializeField] private float biteRange = 2.4f;        // 물기를 시작하는 거리
    [SerializeField] private int biteDamage = 12;           // 물기 1회 피해량
    [SerializeField] private float biteReach = 2.6f;        // 물기 판정 상자의 전방 길이
    [SerializeField] private float biteBottom = 0.8f;       // 물기 판정 상자의 아래쪽 높이(발밑 기준) - 머리가 중단을 노림
    [SerializeField] private float biteTop = 2.6f;          // 물기 판정 상자의 위쪽 높이(발밑 기준)
    [SerializeField] private float biteHitTime = 0.35f;     // 공격 시작 후 타격 시각(클립: 준비 프레임 0.35초 뒤 내지르기 프레임)
    [SerializeField] private float biteDuration = 0.7f;     // Boss_Bite 클립 전체 길이

    [Header("일반 공격 2 - 꼬리치기")]
    [SerializeField] private float tailRange = 3.4f;        // 꼬리치기를 시작하는 거리(물기보다 길다)
    [SerializeField] private int tailDamage = 10;           // 꼬리치기 피해량
    [SerializeField] private float tailReach = 3.6f;        // 꼬리 판정 상자의 전방 길이
    [SerializeField] private float tailHeight = 1f;         // 꼬리 판정 상자의 높이(발밑부터) - 낮게 쓸기 때문에 점프로 피할 수 있음
    [SerializeField] private float tailHitTime = 0.4f;      // 공격 시작 후 타격 시각(클립: 치켜들기 0.4초 뒤 휘두르기 프레임)
    [SerializeField] private float tailDuration = 0.8f;     // Boss_TailSwipe 클립 전체 길이
    [SerializeField] private float attackCooldown = 1.2f;   // 일반 공격 종료 후 재사용 대기시간(물기/꼬리치기 공용)

    [Header("특수 공격 공통")]
    [SerializeField] private float specialCooldown = 7f;    // 특수 공격 재사용 대기시간(독 브레스와 석화 광선을 번갈아 사용)
    [SerializeField] private float firstSpecialDelay = 4f;  // 전투 시작 후 첫 특수 공격까지의 대기시간
    [SerializeField] private float specialMinRange = 2.5f;  // 이 거리보다 가까우면 특수 공격을 쓰지 않음(일반 공격 우선)

    [Header("특수 공격 1 - 독 브레스")]
    [SerializeField] private float breathMaxRange = 7f;       // 이 거리 안에 플레이어가 있어야 독 브레스를 사용
    [SerializeField] private float breathChargeTime = 0.5f;   // 분사 전 머리를 젖혀 독을 모으는 시간(클립: 충전 프레임 길이)
    [SerializeField] private float breathFireTime = 1.2f;     // 분사 지속 시간
    [SerializeField] private float breathDuration = 1.9f;     // Boss_PoisonBreath 클립 전체 길이(충전 + 분사 + 마무리)
    [SerializeField] private float breathLength = 6.5f;       // 브레스 판정 상자의 전방 길이(입 위치부터)
    [SerializeField] private float breathHeight = 2.2f;       // 브레스 판정 상자의 높이
    [SerializeField] private Vector2 mouthOffset = new Vector2(1.15f, 2f);  // 오른쪽을 볼 때 원점 기준 입 위치(12_breath_fire 프레임, Visual 스케일 0.35 기준 실측. 왼쪽을 보면 x 반전)
    [SerializeField] private float breathDropOffset = 1.2f;   // 브레스가 입보다 이만큼 아래 높이를 중심으로 퍼짐(살짝 아래로 뿜음)
    [SerializeField] private int breathTickDamage = 14;       // 브레스 1틱 피해량(플레이어 방어력만큼 경감됨)
    [SerializeField] private float breathTickInterval = 1f;   // 브레스 피해 간격 - 플레이어 피격 무적(1초)과 같게 맞춤. 더 짧으면 무적에 막혀 틱이 버려진다(분사 1.2초 동안 최대 2회)

    [Header("특수 공격 2 - 석화 광선")]
    [SerializeField] private float gazeChargeTime = 0.8f;     // 눈에 빛을 모으는 시간(클립: 충전 프레임 길이) - 이 시간이 회피 예고
    [SerializeField] private float gazeLockTime = 0.25f;      // 발사 이 시간 전에 조준 방향을 고정(고정 후 움직이면 피할 수 있음)
    [SerializeField] private float gazeBeamTime = 0.35f;      // 광선이 화면에 보이는 시간
    [SerializeField] private float gazeDuration = 1.4f;       // Boss_PetrifyGaze 클립 전체 길이
    [SerializeField] private float gazeRange = 14f;           // 광선 최대 길이
    [SerializeField] private float gazeWidth = 0.5f;          // 광선 판정 두께
    [SerializeField] private float gazeMaxAngle = 35f;        // 수평 기준으로 위아래 조준 가능한 최대 각도(뒤쪽/수직 방향으로는 쏘지 않음)
    [SerializeField] private Vector2 eyeOffset = new Vector2(0.6f, 3f);      // 오른쪽을 볼 때 원점 기준 눈 위치(14_gaze_fire 프레임, Visual 스케일 0.35 기준 실측. 왼쪽을 보면 x 반전)
    [SerializeField] private int gazeDamage = 12;             // 광선 피해량(플레이어 방어력만큼 경감됨)
    [SerializeField] private float petrifyDuration = 1.5f;    // 석화(행동 불가) 지속 시간

    [Header("그로기")]
    [SerializeField] private int staggerThreshold = 30;        // 짧은 시간에 누적된 피해가 이 값 이상이면 그로기
    [SerializeField] private int bigHitDamage = 20;            // 단일 피격이 이 값 이상이면 즉시 그로기
    [SerializeField] private float staggerDecayPerSecond = 5f; // 누적 피해가 시간당 줄어드는 양
    [SerializeField] private float groggyDuration = 3f;        // 그로기 지속 시간
    [SerializeField] private float groggyImmunity = 4f;        // 그로기가 풀린 뒤 다시 그로기에 걸리지 않는 시간(스턴락 방지)

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsGroggyHash = Animator.StringToHash("IsGroggy");
    private static readonly int BiteHash = Animator.StringToHash("Bite");
    private static readonly int TailSwipeHash = Animator.StringToHash("TailSwipe");
    private static readonly int PoisonBreathHash = Animator.StringToHash("PoisonBreath");
    private static readonly int PetrifyGazeHash = Animator.StringToHash("PetrifyGaze");
    private static readonly int DieHash = Animator.StringToHash("Die");

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private Boss boss;
    private Transform target;                                 // 추적 대상(GameManager에 등록된 플레이어)
    private float baseVisualScaleX;                           // Visual의 원래 X 스케일 크기(좌우 반전에 사용)
    private bool facingRight = true;
    private bool lastWalking;                                 // 마지막으로 Animator에 보낸 IsWalking 값(불필요한 SetBool 방지)
    private bool nextSpecialIsBreath = true;                  // 다음 특수 공격 종류(독 브레스 ↔ 석화 광선 번갈아 사용)
    private float nextAttackTime;
    private float nextSpecialTime;
    private float staggerGauge;                               // 누적 피해량(그로기 게이지)
    private float groggyImmuneUntil;
    private readonly Collider2D[] hitBuffer = new Collider2D[8];   // 판정 결과 버퍼(매번 새로 할당하지 않기 위해 캐싱 - 규칙 43)
    private readonly Rigidbody2D[] hitBodies = new Rigidbody2D[8]; // 한 번의 판정에서 같은 대상을 중복 타격하지 않기 위한 목록

    public SerpentState State { get; private set; } = SerpentState.Idle;

    // 컴포넌트 캐싱 및 이벤트 구독
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        boss = GetComponent<Boss>();
        if (visual != null) baseVisualScaleX = Mathf.Abs(visual.localScale.x);
        boss.OnDamaged += HandleDamaged;
        boss.OnDied += HandleDied;
        HideVfx();
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
        if (State == SerpentState.Dead) return;

        staggerGauge = Mathf.Max(0f, staggerGauge - staggerDecayPerSecond * Time.deltaTime);

        if (State != SerpentState.Idle && State != SerpentState.Chase) return;

        if (target == null)
        {
            target = GameManager.Instance.PlayerTransform;
            if (target == null) return;
        }

        float dx = target.position.x - transform.position.x;
        float dist = Mathf.Abs(dx);

        if (dist > detectRange)
        {
            SetState(SerpentState.Idle);
            return;
        }

        Face(dx > 0f);

        if (Time.time >= nextSpecialTime && dist >= specialMinRange)
        {
            StartSpecial(dist);
            return;
        }

        if (Time.time >= nextAttackTime && dist <= tailRange)
        {
            // 물기 사거리 안이면 물기/꼬리치기 중 무작위, 그보다 멀면(꼬리 사거리 안) 꼬리치기
            bool bite = dist <= biteRange && Random.value < 0.5f;
            StartCoroutine(bite ? BiteRoutine() : TailSwipeRoutine());
        }
        else if (dist > stopDistance && HasGroundAt(transform.position.x + (facingRight ? 1f : -1f) * edgeProbeDistance))
        {
            // 진행 방향 발밑에 바닥이 있을 때만 추적(발판 끝에서 낙사하지 않도록 멈춤)
            SetState(SerpentState.Chase);
        }
        else
        {
            SetState(SerpentState.Idle);
        }
    }

    // 물리 갱신에서 추적 이동만 적용(공격 중에는 제자리에 멈춘다)
    private void FixedUpdate()
    {
        float vx = State == SerpentState.Chase ? (facingRight ? 1f : -1f) * moveSpeed : 0f;
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    // 특수 공격 시작: 독 브레스와 석화 광선을 번갈아 사용. 독 브레스 차례라도 플레이어가 브레스 사거리 밖이면
    // 사거리가 긴 석화 광선으로 대체한다(광선은 detectRange 안이면 항상 사용 가능)
    private void StartSpecial(float dist)
    {
        bool useBreath = nextSpecialIsBreath && dist <= breathMaxRange;
        nextSpecialIsBreath = !useBreath; // 방금 쓴 것의 반대를 다음 차례로
        StartCoroutine(useBreath ? PoisonBreathRoutine() : PetrifyGazeRoutine());
    }

    // 일반 공격 1 - 물기: 준비(목을 움츠림) → 내지르기 프레임에 전방 중단 상자 판정 1회
    private IEnumerator BiteRoutine()
    {
        SetState(SerpentState.Bite);
        animator.SetTrigger(BiteHash);

        yield return new WaitForSeconds(biteHitTime);
        DealBoxDamage(biteReach, biteBottom, biteTop, biteDamage);
        yield return new WaitForSeconds(Mathf.Max(0f, biteDuration - biteHitTime));

        nextAttackTime = Time.time + attackCooldown;
        SetState(SerpentState.Idle);
    }

    // 일반 공격 2 - 꼬리치기: 꼬리를 치켜듦 → 휘두르기 프레임에 전방 바닥 상자 판정 1회(낮은 판정이라 점프로 회피 가능)
    private IEnumerator TailSwipeRoutine()
    {
        SetState(SerpentState.TailSwipe);
        animator.SetTrigger(TailSwipeHash);

        yield return new WaitForSeconds(tailHitTime);
        DealBoxDamage(tailReach, 0f, tailHeight, tailDamage);
        yield return new WaitForSeconds(Mathf.Max(0f, tailDuration - tailHitTime));

        nextAttackTime = Time.time + attackCooldown;
        SetState(SerpentState.Idle);
    }

    // 특수 공격 1 - 독 브레스: 충전(머리를 젖힘) → 분사 동안 이펙트를 켜고 일정 간격으로 전방 상자 범위에 피해
    private IEnumerator PoisonBreathRoutine()
    {
        SetState(SerpentState.PoisonBreath);
        animator.SetTrigger(PoisonBreathHash);

        yield return new WaitForSeconds(breathChargeTime);

        if (breathVfx != null) breathVfx.enabled = true;
        float elapsed = 0f;
        float nextTick = 0f;
        while (elapsed < breathFireTime)
        {
            if (elapsed >= nextTick)
            {
                DealBreathDamage();
                nextTick += breathTickInterval;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (breathVfx != null) breathVfx.enabled = false;

        float remaining = breathDuration - breathChargeTime - breathFireTime;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        FinishSpecial();
    }

    // 특수 공격 2 - 석화 광선: 눈에 빛을 모음 → 발사 직전 조준 고정 → 광선 발사(첫 대상 1명에게 피해 + 석화)
    private IEnumerator PetrifyGazeRoutine()
    {
        SetState(SerpentState.PetrifyGaze);
        animator.SetTrigger(PetrifyGazeHash);

        yield return new WaitForSeconds(Mathf.Max(0f, gazeChargeTime - gazeLockTime));

        // 조준 고정: 이 시점의 플레이어 몸 중심을 향하되, 바라보는 방향 기준 ±gazeMaxAngle로 제한
        Vector2 eye = GetEyePosition();
        Vector2 aim = facingRight ? Vector2.right : Vector2.left;
        if (target != null)
        {
            Vector2 toTarget = (Vector2)target.position + Vector2.up * 0.5f - eye;
            float angle = Mathf.Atan2(toTarget.y, Mathf.Abs(toTarget.x)) * Mathf.Rad2Deg;
            angle = Mathf.Clamp(angle, -gazeMaxAngle, gazeMaxAngle) * Mathf.Deg2Rad;
            aim = new Vector2((facingRight ? 1f : -1f) * Mathf.Cos(angle), Mathf.Sin(angle));
        }

        yield return new WaitForSeconds(gazeLockTime);

        float beamLength = FireGaze(eye, aim);
        ShowGazeBeam(eye, aim, beamLength);
        yield return new WaitForSeconds(gazeBeamTime);
        if (gazeBeamVfx != null) gazeBeamVfx.enabled = false;

        float remaining = gazeDuration - gazeChargeTime - gazeBeamTime;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);

        FinishSpecial();
    }

    // 특수 공격 종료 처리(다음 특수/일반 공격 대기시간 설정)
    private void FinishSpecial()
    {
        nextSpecialTime = Time.time + specialCooldown;
        nextAttackTime = Time.time + attackCooldown;
        SetState(SerpentState.Idle);
    }

    // 그로기: 모든 행동을 멈추고 일정 시간 무방비 상태로 있다가 회복
    private IEnumerator GroggyRoutine()
    {
        SetState(SerpentState.Groggy);
        if (target != null) Face(target.position.x > transform.position.x);
        rb.linearVelocity = Vector2.zero;
        HideVfx();
        ResetAttackTriggers();
        animator.SetBool(IsGroggyHash, true);

        yield return new WaitForSeconds(groggyDuration);

        animator.SetBool(IsGroggyHash, false);
        groggyImmuneUntil = Time.time + groggyImmunity;
        staggerGauge = 0f;
        nextAttackTime = Time.time + 0.5f;
        SetState(SerpentState.Idle);
    }

    // 피격 시 피해를 그로기 게이지에 누적. 큰 한 방이거나 누적치가 임계값을 넘으면 그로기 진입
    private void HandleDamaged(int amount)
    {
        if (State == SerpentState.Dead || State == SerpentState.Groggy) return;

        staggerGauge += amount;
        bool shouldStagger = amount >= bigHitDamage || staggerGauge >= staggerThreshold;
        if (!shouldStagger || Time.time < groggyImmuneUntil) return;

        StopAllCoroutines();
        StartCoroutine(GroggyRoutine());
    }

    // 사망: 모든 행동을 멈추고 사망 애니메이션(쓰러짐 → 돌로 굳은 채 정지)을 한 번 재생
    private void HandleDied()
    {
        StopAllCoroutines();
        HideVfx();
        rb.linearVelocity = Vector2.zero;
        ResetAttackTriggers();
        animator.SetBool(IsWalkingHash, false);
        animator.SetBool(IsGroggyHash, false);
        animator.SetTrigger(DieHash);
        // 쓰러진 보스의 몸이 종료점으로 가는 길을 막지 않도록 물리를 멈추고 충돌을 끈다(모습은 그대로 남김)
        rb.bodyType = RigidbodyType2D.Kinematic;
        bodyCollider.enabled = false;
        SetState(SerpentState.Dead);
    }

    // 바라보는 방향 앞쪽, 발밑 기준 [bottom, top] 높이의 상자 범위 안 대상에게 피해(물기/꼬리치기 공용)
    private void DealBoxDamage(float reach, float bottom, float top, int damage)
    {
        Vector2 feet = new Vector2(transform.position.x, bodyCollider.bounds.min.y);
        float dir = facingRight ? 1f : -1f;
        Vector2 center = feet + new Vector2(dir * reach * 0.5f, (bottom + top) * 0.5f);
        int count = Physics2D.OverlapBoxNonAlloc(center, new Vector2(reach, top - bottom), 0f, hitBuffer, targetLayer);
        ApplyDamage(count, damage);
    }

    // 독 브레스 1틱: 입 앞쪽부터 breathLength 길이의 상자 범위(입보다 살짝 아래 중심)에 피해
    private void DealBreathDamage()
    {
        Vector2 mouth = GetMouthPosition();
        float dir = facingRight ? 1f : -1f;
        Vector2 center = mouth + new Vector2(dir * breathLength * 0.5f, -breathDropOffset);
        int count = Physics2D.OverlapBoxNonAlloc(center, new Vector2(breathLength, breathHeight), 0f, hitBuffer, targetLayer);
        ApplyDamage(count, breathTickDamage);
    }

    // 석화 광선 판정: 눈에서 조준 방향으로 두께 있는 원형 캐스트. 바닥에 막히면 그 지점까지만,
    // 대상에 맞으면 피해 + 석화를 주고 거기서 멈춘다. 실제로 그릴 광선 길이를 반환
    private float FireGaze(Vector2 eye, Vector2 aim)
    {
        float length = gazeRange;
        RaycastHit2D wall = Physics2D.Raycast(eye, aim, gazeRange, groundLayer);
        if (wall.collider != null) length = wall.distance;

        RaycastHit2D hit = Physics2D.CircleCast(eye, gazeWidth * 0.5f, aim, length, targetLayer);
        if (hit.collider == null) return length;

        Rigidbody2D body = hit.collider.attachedRigidbody;
        if (body == null || body == rb) return length;

        if (body.TryGetComponent<IDamageable>(out var damageable)) damageable.TakeDamage(gazeDamage);
        if (body.TryGetComponent<IPetrifiable>(out var petrifiable)) petrifiable.Petrify(petrifyDuration);
        return hit.distance;
    }

    // 석화 광선 이펙트 배치: 피벗(왼쪽 끝)을 눈 위치에 두고 조준 방향으로 회전, 길이에 맞게 X 스케일을 늘린다.
    // gazeBeamVfx는 Visual의 형제(보스 루트의 자식)로 두어 좌우 반전 스케일의 영향을 받지 않게 한다
    private void ShowGazeBeam(Vector2 eye, Vector2 aim, float length)
    {
        if (gazeBeamVfx == null) return;
        Transform t = gazeBeamVfx.transform;
        t.position = eye;
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        float spriteWidth = gazeBeamVfx.sprite != null ? gazeBeamVfx.sprite.bounds.size.x : 1f;
        Vector3 s = t.localScale;
        s.x = length / spriteWidth;
        t.localScale = s;
        gazeBeamVfx.enabled = true;
    }

    // 판정 결과 중 IDamageable을 가진 대상에게 한 번씩만 피해를 준다(자기 자신 제외)
    private void ApplyDamage(int count, int damage)
    {
        int uniqueCount = 0;
        for (int i = 0; i < count; i++)
        {
            Rigidbody2D body = hitBuffer[i].attachedRigidbody;
            if (body == null || body == rb) continue;

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

    // 바라보는 방향을 반영한 입/눈의 월드 위치
    private Vector2 GetMouthPosition() => (Vector2)transform.position + new Vector2((facingRight ? 1f : -1f) * mouthOffset.x, mouthOffset.y);
    private Vector2 GetEyePosition() => (Vector2)transform.position + new Vector2((facingRight ? 1f : -1f) * eyeOffset.x, eyeOffset.y);

    // 그로기/사망 시 남아있을 수 있는 공격 트리거를 모두 지운다(회복 직후 엉뚱한 공격 모션이 재생되지 않도록)
    private void ResetAttackTriggers()
    {
        animator.ResetTrigger(BiteHash);
        animator.ResetTrigger(TailSwipeHash);
        animator.ResetTrigger(PoisonBreathHash);
        animator.ResetTrigger(PetrifyGazeHash);
    }

    // 브레스/광선 이펙트를 모두 끈다(공격이 중간에 끊겼을 때 이펙트가 남지 않도록)
    private void HideVfx()
    {
        if (breathVfx != null) breathVfx.enabled = false;
        if (gazeBeamVfx != null) gazeBeamVfx.enabled = false;
    }

    // 주어진 x 좌표 발밑 근처에 바닥이 있는지 확인(발판 가장자리 낙사 방지용)
    private bool HasGroundAt(float x)
    {
        Vector2 origin = new Vector2(x, bodyCollider.bounds.min.y + 0.5f);
        return Physics2D.Raycast(origin, Vector2.down, 1.5f, groundLayer).collider != null;
    }

    // 상태를 바꾸고 이동 애니메이션(IsWalking)을 함께 갱신
    private void SetState(SerpentState next)
    {
        State = next;
        bool walking = next == SerpentState.Chase;
        if (walking != lastWalking)
        {
            lastWalking = walking;
            animator.SetBool(IsWalkingHash, walking);
        }
    }

    // 스프라이트는 오른쪽을 보는 기준으로 그려져 있으므로 왼쪽을 볼 때 Visual의 X 스케일을 반전.
    // 독 브레스 이펙트는 Visual의 자식이라 함께 반전된다
    private void Face(bool right)
    {
        if (right == facingRight || visual == null) return;
        facingRight = right;
        Vector3 s = visual.localScale;
        s.x = baseVisualScaleX * (right ? 1f : -1f);
        visual.localScale = s;
    }

#if UNITY_EDITOR
    // 씬 뷰에서 입/눈 위치와 공격 범위를 확인하기 위한 기즈모(오른쪽을 볼 때 기준)
    private void OnDrawGizmosSelected()
    {
        Vector2 o = transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(o + new Vector2(biteReach * 0.5f, (biteBottom + biteTop) * 0.5f), new Vector3(biteReach, biteTop - biteBottom));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(o + new Vector2(tailReach * 0.5f, tailHeight * 0.5f), new Vector3(tailReach, tailHeight));
        Gizmos.color = Color.green;
        Vector2 mouth = o + mouthOffset;
        Gizmos.DrawWireCube(mouth + new Vector2(breathLength * 0.5f, -breathDropOffset), new Vector3(breathLength, breathHeight));
        Gizmos.color = Color.yellow;
        Vector2 eye = o + eyeOffset;
        Gizmos.DrawWireSphere(eye, 0.15f);
        Gizmos.DrawLine(eye, eye + Vector2.right * gazeRange);
    }
#endif
}
