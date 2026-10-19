// AI work record: Claude Sonnet 5.5; 2026-10-18 (created), 2026-10-19 (fixed facing/walk animation, human-like attacks, boss fight mode, non-alloc enemy check for rule 41). Teaser trailer auto-play bot (Play Mode only, no game logic changes).
using System.Reflection;
using UnityEngine;

// 티저 촬영용 자동 조작: 입력 장치를 흉내 내지 않고, 플레이어 컴포넌트가 읽은 입력 값을 같은 프레임 안에 덮어쓴다.
// 달리기 모드: 진행 방향으로 달리며 앞 지형(발판 끝/벽)을 레이캐스트로 살펴 점프하고, 앞에 적이 있으면 검을 휘두른다.
// 보스전 모드(Target 지정): 보스에게 다가가 일정 간격을 유지하며 싸우고, 가끔 점프로 피한다.
// 게임 스크립트는 수정하지 않고 리플렉션으로 private 필드/메서드를 호출하며, 촬영용 임시 오브젝트에만 붙인다.
//
// 실행 순서 5: PlayerMovement/PlayerCombat(0) 뒤, PlayerSpriteAnimator(10) 앞이어야 한다.
// PlayerMovement.Update가 매 프레임 실제 키 입력(0)으로 moveInput과 바라보는 방향을 정하므로, 그 직후에 입력과 방향을 덮어써야
// 애니메이터가 걷기 모션을 보여 주고 캐릭터가 진행 방향을 바라본다(예전에는 1000이라 걷기 모션이 없고 방향이 어긋났다).
[DefaultExecutionOrder(5)]
public class TeaserBot : MonoBehaviour
{
    [SerializeField] private float lookAhead = 0.9f;      // 앞 발판 끝을 살필 거리
    [SerializeField] private float attackInterval = 1.6f; // 적이 없을 때 베기 연출 간격 / 적이 있을 때 최대 간격(초). 0이면 공격하지 않음
    [SerializeField] private float guardInterval = 0f;    // 방어 시연 간격(초). 0이면 방어하지 않음
    [SerializeField] private float guardDuration = 0.7f;  // 방어를 유지하는 시간(초)
    [SerializeField] private float jumpCooldown = 0.35f;  // 연속 점프 방지 시간(초)

    private PlayerMovement movement;
    private PlayerCombat combat;
    private PlayerDefense defense;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private LayerMask groundMask;
    private float jumpForce;

    private FieldInfo moveInputField, isJumpingField, isGroundedField;
    private MethodInfo swordAttack, setGuard, flip;

    private float nextAttack, nextGuard, guardEnd, lastJump = -99f;
    private float nextFlourish;            // 적이 없을 때 가끔 보여 주는 베기 연출 시각
    private float nextDodge;               // 보스전에서 가끔 점프로 피하는 시각
    private float lastX, stalledTime;      // 진행이 멈춘 시간 측정용(벽/단차에 마찰로 붙었을 때 점프로 탈출)
    private bool guarding;
    private readonly Collider2D[] enemyBuffer = new Collider2D[16];   // EnemyAhead 결과 버퍼(재사용)

    public float Direction { get; set; } = 1f;       // 진행 방향(+1 오른쪽, -1 왼쪽, 0 제자리)
    // 지정하면 달리기 대신 이 대상(보스)에게 다가가 일정 간격(StandOff)을 유지하며 싸운다(보스전 촬영용).
    public Transform Target { get; set; }
    public float StandOff { get; set; } = 2.6f;

    // 필요한 컴포넌트와 리플렉션 핸들을 한 번만 찾아 둔다.
    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        defense = GetComponent<PlayerDefense>();
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        moveInputField = typeof(PlayerMovement).GetField("moveInput", flags);
        isJumpingField = typeof(PlayerMovement).GetField("isJumping", flags);
        isGroundedField = typeof(PlayerMovement).GetField("isGrounded", flags);
        flip = typeof(PlayerMovement).GetMethod("Flip", flags);
        jumpForce = (float)typeof(PlayerMovement).GetField("jumpForce", flags).GetValue(movement);
        groundMask = (LayerMask)typeof(PlayerMovement).GetField("groundLayer", flags).GetValue(movement);
        swordAttack = typeof(PlayerCombat).GetMethod("TryPerformSwordAttack", flags);
        if (defense != null) setGuard = typeof(PlayerDefense).GetMethod("SetGuard", flags);
    }

    // 플레이어 입력 처리가 끝난 뒤(실행 순서 5) 이동/방향/점프/공격/방어 입력을 덮어쓴다.
    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        float face = Direction;
        if (Target != null && Target.gameObject.activeInHierarchy)
        {
            // 보스전: 대상 쪽으로 다가가고, 너무 가까우면 물러나며, 적당한 간격에서는 제자리에서 싸운다.
            float dx = Target.position.x - transform.position.x;
            float want = dx >= 0f ? 1f : -1f;
            float dist = Mathf.Abs(dx);
            Direction = dist > StandOff ? want : (dist < StandOff * 0.5f ? -want : 0f);
            face = want;
        }

        moveInputField.SetValue(movement, Direction);
        // PlayerMovement.Update는 실제 키 입력(0)으로 방향을 판단하므로, 바라볼 방향과 다르면 직접 방향을 전환한다.
        if (face != 0f && movement.FacingRight != (face > 0f)) flip.Invoke(movement, null);

        // 이동 입력을 주는데 위치가 거의 변하지 않으면 정지로 보고, 접지 여부와 관계없이 점프해 단차/벽에서 빠져나온다.
        float x = transform.position.x;
        stalledTime = (Mathf.Abs(Direction) > 0.5f && Mathf.Abs(x - lastX) < 0.005f) ? stalledTime + Time.deltaTime : 0f;
        lastX = x;
        bool stalled = stalledTime > 0.2f && Mathf.Abs(body.linearVelocity.y) < 0.2f;

        bool grounded = movement.IsGrounded;
        if (Target != null)
        {
            // 보스전: 가끔(2~3.5초마다) 점프로 공격을 피하는 것처럼 보이게 한다.
            if (grounded && Time.time >= nextDodge)
            {
                nextDodge = Time.time + Random.Range(2f, 3.5f);
                Jump();
            }
        }
        else if ((grounded || stalled) && Time.time - lastJump > jumpCooldown && (stalled || ShouldJump()))
        {
            stalledTime = 0f;
            Jump();
        }

        // 사람처럼: 앞에 적이 가까울 때 휘두르고, 적이 없을 때는 가끔(약 5초마다) 한 번만 휘두른다.
        // 보스전(Target 지정)에서는 공격하지 않는다: 맞은 보스가 그로기(쓰러진 자세)에 빠져 장면이 밋밋해지기 때문이다.
        if (attackInterval > 0f && Time.time >= nextAttack && !guarding && Target == null)
        {
            bool enemyAhead = EnemyAhead();
            if (enemyAhead || Time.time >= nextFlourish)
            {
                // 보스전에서는 보스가 계속 쓰러져 있지 않도록 공격을 드물게(2.5~4초) 한다.
                nextAttack = Time.time + (Target != null ? Random.Range(2.5f, 4f) : (enemyAhead ? Random.Range(0.45f, attackInterval) : attackInterval));
                nextFlourish = Time.time + Random.Range(4f, 6f);
                swordAttack.Invoke(combat, null);
            }
        }

        if (setGuard != null && guardInterval > 0f)
        {
            if (!guarding && Time.time >= nextGuard)
            {
                guarding = true;
                guardEnd = Time.time + guardDuration;
                setGuard.Invoke(defense, new object[] { true });
            }
            else if (guarding && Time.time >= guardEnd)
            {
                guarding = false;
                nextGuard = Time.time + guardInterval;
                setGuard.Invoke(defense, new object[] { false });
            }
        }
    }

    // 점프 입력과 같은 효과: 점프 상태 플래그를 켜고 위쪽 속도를 준다.
    private void Jump()
    {
        lastJump = Time.time;
        isJumpingField.SetValue(movement, true);
        isGroundedField.SetValue(movement, false);
        body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
    }

    // 바라보는 방향 앞쪽 가까이에 몬스터/보스가 있는지 확인한다(검 공격 타이밍 판단용).
    private bool EnemyAhead()
    {
        Bounds b = bodyCollider.bounds;
        float side = movement.FacingRight ? 1f : -1f;
        Vector2 center = new Vector2(b.center.x + side * (b.extents.x + 1.3f), b.center.y);
        // 미리 만들어 둔 배열에 결과를 받아 호출할 때마다 새 배열이 할당되지 않게 한다(수칙 41).
        int count = Physics2D.OverlapCircle(center, 1.4f, ContactFilter2D.noFilter, enemyBuffer);
        for (int i = 0; i < count; i++)
        {
            if (enemyBuffer[i].GetComponentInParent<MonsterController>() != null || enemyBuffer[i].GetComponentInParent<Boss>() != null) return true;
        }
        return false;
    }

    // 발판 끝(앞 바닥 없음)이나 막힌 벽이 코앞이면 점프해야 한다고 판단한다.
    private bool ShouldJump()
    {
        Bounds b = bodyCollider.bounds;
        float feetY = b.min.y;
        Vector2 ahead = new Vector2(b.center.x + Direction * (b.extents.x + lookAhead), feetY + 0.25f);
        bool groundAhead = Physics2D.Raycast(ahead, Vector2.down, 1.6f, groundMask).collider != null;
        if (!groundAhead) return true;

        Vector2 chest = new Vector2(b.center.x, feetY + 0.35f);
        RaycastHit2D wall = Physics2D.Raycast(chest, Vector2.right * Direction, b.extents.x + 0.45f, groundMask);
        return wall.collider != null && Mathf.Abs(wall.normal.y) < 0.6f;
    }
}
