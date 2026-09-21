using UnityEngine;

// 플레이어의 좌우 이동, 점프, 방향 전환(Flip)을 담당하는 스크립트
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;         // 좌우 이동 속도
    [SerializeField] private float jumpForce = 6f;         // 점프 시 가하는 상승 힘
    [SerializeField] private Transform groundCheck;         // 바닥 감지 기준점(발밑에 배치)
    [SerializeField] private float groundCheckRadius = 0.1f; // 바닥 감지 원의 반지름
    [SerializeField] private LayerMask groundLayer;         // 바닥으로 인식할 레이어

    [SerializeField] private float slopeCheckDistance = 1f;    // 경사면 감지용 레이캐스트 길이(발밑 기준)
    [SerializeField] private float maxWalkableSlopeAngle = 60f; // 이 각도 이하만 걸어서 오를 수 있는 경사로 인정(그보다 급하면 일반 벽으로 처리)

    private Rigidbody2D rb;      // 이동/점프에 사용하는 물리 컴포넌트 캐시
    private float moveInput;     // 좌우 입력값(-1 ~ 1)
    private bool isGrounded;     // 현재 바닥에 닿아있는지 여부

    public bool FacingRight { get; private set; } = true; // 현재 캐릭터가 오른쪽을 보고 있는지 여부(외부에서 조회용)
    public float MoveInput => moveInput;   // 좌우 입력값(외부 조회용, 예: 스프라이트 상태 전환)
    public bool IsGrounded => isGrounded;  // 접지 여부(외부 조회용, 예: 스프라이트 상태 전환)
    public bool MovementLocked { get; set; } // true인 동안 FixedUpdate의 좌우 속도 적용을 건너뜀(대시 등 다른 스크립트가 속도를 직접 제어할 때 사용)

    // 시작 시 Rigidbody2D 컴포넌트를 미리 캐싱(매 프레임 GetComponent 호출 방지)
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // 매 프레임 입력을 읽어 방향 전환, 바닥 감지, 점프 입력을 처리
    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        // 입력 방향과 현재 바라보는 방향이 다르면 스프라이트를 뒤집음
        if (moveInput > 0f && !FacingRight) Flip();
        else if (moveInput < 0f && FacingRight) Flip();

        // groundCheck 위치를 기준으로 원형 범위 안에 groundLayer가 있으면 접지 상태로 판단
        isGrounded = groundCheck != null &&
            Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // 점프 버튼 입력 + 접지 상태일 때만 위쪽으로 속도를 부여해 점프
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    // 물리 연산 주기에 맞춰 실제 좌우 이동 속도를 적용(수직 속도는 유지)
    private void FixedUpdate()
    {
        if (MovementLocked) return;

        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
        SnapToSlope();
    }

    // 1-2영역처럼 회전된 발판 여러 개를 이어 붙여 경사로를 표현한 지형에서는, 타일 사이의
    // 미세한 경계마다 충돌 처리가 조금씩 튀어 이동이 부자연스러워진다. 그래서 접지 중이고
    // 위로 뛰어오르는 중이 아니면, 발밑으로 레이캐스트를 쏴서 실제 경사면 표면에 y좌표를
    // 매 프레임 맞춰 붙인다(타일 경계와 무관하게 항상 표면을 따라 매끄럽게 미끄러지듯 이동).
    // 경사가 너무 급하면(벽에 가까우면) 걸어 오를 수 없는 지형으로 보고 보정하지 않는다.
    private void SnapToSlope()
    {
        if (!isGrounded || rb.linearVelocity.y > 0.01f || groundCheck == null) return;

        RaycastHit2D hit = Physics2D.Raycast(groundCheck.position, Vector2.down, slopeCheckDistance, groundLayer);
        if (hit.collider == null) return;

        float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
        if (slopeAngle < 0.5f || slopeAngle > maxWalkableSlopeAngle) return; // 평지거나 걸어 오를 수 없는 급경사면 보정 생략

        float groundCheckOffsetY = groundCheck.position.y - rb.position.y; // 회전이 없으므로 항상 일정한 값
        rb.position = new Vector2(rb.position.x, hit.point.y - groundCheckOffsetY);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
    }

    // 좌우 반전은 스프라이트가 손에 든 무기/방패 위치까지 바뀌어버리므로 스케일 미러링 대신
    // 방향 상태만 전환한다. 실제 표시되는 스프라이트 교체는 PlayerSpriteAnimator가 담당한다.
    private void Flip()
    {
        FacingRight = !FacingRight;
    }
}
