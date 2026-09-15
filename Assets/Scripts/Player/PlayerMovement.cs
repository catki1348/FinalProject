using UnityEngine;

// 플레이어의 좌우 이동, 점프, 방향 전환(Flip)을 담당하는 스크립트
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;         // 좌우 이동 속도
    [SerializeField] private float jumpForce = 12f;        // 점프 시 가하는 상승 힘
    [SerializeField] private Transform groundCheck;         // 바닥 감지 기준점(발밑에 배치)
    [SerializeField] private float groundCheckRadius = 0.1f; // 바닥 감지 원의 반지름
    [SerializeField] private LayerMask groundLayer;         // 바닥으로 인식할 레이어

    private Rigidbody2D rb;      // 이동/점프에 사용하는 물리 컴포넌트 캐시
    private float moveInput;     // 좌우 입력값(-1 ~ 1)
    private bool isGrounded;     // 현재 바닥에 닿아있는지 여부

    public bool FacingRight { get; private set; } = true; // 현재 캐릭터가 오른쪽을 보고 있는지 여부(외부에서 조회용)
    public float MoveInput => moveInput;   // 좌우 입력값(외부 조회용, 예: 스프라이트 상태 전환)
    public bool IsGrounded => isGrounded;  // 접지 여부(외부 조회용, 예: 스프라이트 상태 전환)

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
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    // 좌우 반전은 스프라이트가 손에 든 무기/방패 위치까지 바뀌어버리므로 스케일 미러링 대신
    // 방향 상태만 전환한다. 실제 표시되는 스프라이트 교체는 PlayerSpriteAnimator가 담당한다.
    private void Flip()
    {
        FacingRight = !FacingRight;
    }
}
