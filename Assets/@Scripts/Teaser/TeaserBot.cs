// AI work record: Claude Sonnet 5.5; 2026-10-18. Teaser trailer auto-play bot (Play Mode only, no game logic changes).
using System.Reflection;
using UnityEngine;

// 티저 촬영용 자동 조작: 입력 장치를 흉내 내지 않고, 플레이어 컴포넌트가 읽은 입력 값을 같은 프레임 뒤에 덮어쓴다.
// 오른쪽으로 달리며 앞 지형(발판 끝/벽)을 레이캐스트로 살펴 점프하고, 일정 간격으로 검 공격과 방어를 보여 준다.
// 게임 스크립트는 수정하지 않고 리플렉션으로 private 필드/메서드를 호출하며, 촬영용 임시 오브젝트에만 붙인다.
[DefaultExecutionOrder(1000)]
public class TeaserBot : MonoBehaviour
{
    [SerializeField] private float lookAhead = 0.9f;      // 앞 발판 끝을 살필 거리
    [SerializeField] private float attackInterval = 1.6f; // 검 공격 간격(초). 0이면 공격하지 않음
    [SerializeField] private float guardInterval = 7f;    // 방어 시연 간격(초). 0이면 방어하지 않음
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
    private MethodInfo swordAttack, setGuard;

    private float nextAttack, nextGuard, guardEnd, lastJump = -99f;
    private float lastX, stalledTime;      // 진행이 멈춘 시간 측정용(벽/단차에 마찰로 붙었을 때 점프로 탈출)
    private bool guarding;
    public float Direction { get; set; } = 1f;       // 진행 방향(+1 오른쪽, -1 왼쪽)

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
        jumpForce = (float)typeof(PlayerMovement).GetField("jumpForce", flags).GetValue(movement);
        groundMask = (LayerMask)typeof(PlayerMovement).GetField("groundLayer", flags).GetValue(movement);
        swordAttack = typeof(PlayerCombat).GetMethod("TryPerformSwordAttack", flags);
        if (defense != null) setGuard = typeof(PlayerDefense).GetMethod("SetGuard", flags);
    }

    // 플레이어 입력 처리가 끝난 뒤(실행 순서 1000) 이동/점프/공격/방어 입력을 덮어쓴다.
    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        moveInputField.SetValue(movement, Direction);

        // 이동 입력을 주는데 위치가 거의 변하지 않으면 정지로 보고, 접지 여부와 관계없이 점프해 단차/벽에서 빠져나온다.
        float x = transform.position.x;
        stalledTime = Mathf.Abs(x - lastX) < 0.005f ? stalledTime + Time.deltaTime : 0f;
        lastX = x;
        bool stalled = stalledTime > 0.2f && Mathf.Abs(body.linearVelocity.y) < 0.2f;

        bool grounded = movement.IsGrounded;
        if ((grounded || stalled) && Time.time - lastJump > jumpCooldown && (stalled || ShouldJump()))
        {
            stalledTime = 0f;
            lastJump = Time.time;
            isJumpingField.SetValue(movement, true);
            isGroundedField.SetValue(movement, false);
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
        }

        if (attackInterval > 0f && Time.time >= nextAttack && !guarding)
        {
            nextAttack = Time.time + attackInterval;
            swordAttack.Invoke(combat, null);
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
