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
    [SerializeField] private AudioClip jumpSfx;                 // 점프 시 재생할 효과음(비워두면 재생하지 않음)

    private Rigidbody2D rb;      // 이동/점프에 사용하는 물리 컴포넌트 캐시
    private float moveInput;     // 좌우 입력값(-1 ~ 1)
    private bool isGrounded;     // 현재 바닥에 닿아있는지 여부
    private Vector2 groundNormal = Vector2.up; // 현재 서 있는 지면의 법선(평지는 (0,1), 경사면이면 기울어진 값)
    private bool isJumping;      // 점프 버튼으로 상승 중인지(경사면에 부딪혀 생기는 순간적인 튕김과 구분하기 위한 명시적 플래그)

    public bool FacingRight { get; private set; } = true; // 현재 캐릭터가 오른쪽을 보고 있는지 여부(외부에서 조회용)
    public float MoveInput => moveInput;   // 좌우 입력값(외부 조회용, 예: 스프라이트 상태 전환)
    public bool IsGrounded => isGrounded;  // 접지 여부(외부 조회용, 예: 스프라이트 상태 전환)
    public bool MovementLocked { get; set; } // true인 동안 FixedUpdate의 좌우 속도 적용을 건너뜀(대시 등 다른 스크립트가 속도를 직접 제어할 때 사용)

    // 시작 시 Rigidbody2D 컴포넌트를 미리 캐싱(매 프레임 GetComponent 호출 방지)
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        GameManager.Instance.RegisterPlayer(transform); // 몬스터 크기 산정 등에서 Find 없이 조회할 수 있도록 등록
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
            isJumping = true;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            SoundManager.Instance.PlaySfx(jumpSfx);
        }

        // 정점을 찍고 하강하기 시작하면 더 이상 "점프 중"이 아니므로 해제한다(착지 시 경사 보정이
        // 다시 정상 동작해야 하기 때문). isGrounded로 판단하지 않는 이유는, 착지 순간에는 이미
        // isGrounded가 true가 되어 있어 그걸로는 상승 구간과 구분할 수 없기 때문이다.
        if (isJumping && rb.linearVelocity.y <= 0f) isJumping = false;
    }

    // 물리 연산 주기에 맞춰 실제 이동 속도를 적용 - 평지에서는 기존처럼 수평으로,
    // 경사면 위에서는 그 접선 방향으로 이동시켜 대각선/곡선 발판 위에서도 자연스럽게 오르내리게 한다.
    private void FixedUpdate()
    {
        if (MovementLocked) return;

        UpdateGroundNormalAndSnap();

        if (isGrounded && groundNormal != Vector2.up)
        {
            // 접선 방향 = 법선을 -90도 회전한 벡터. 평지(법선 (0,1))일 때는 (1,0)이 되어
            // 이동 속도가 그대로 수평 이동과 같아지므로 기존 평지 동작과 자연스럽게 이어진다.
            // 속도 크기(moveSpeed)를 그대로 접선에 투영하기 때문에, 가파른 경사를 오를수록
            // 수평 이동량은 줄고 수직 이동량이 늘어 실제 체감 속도가 평지와 비슷하게 유지된다
            // (수평 속도만 고정한 채 y만 눌러 붙이면 경사가 급할수록 이동 거리가 늘어나 부자연스럽다).
            Vector2 slopeDirection = new Vector2(groundNormal.y, -groundNormal.x);
            rb.linearVelocity = slopeDirection * (moveInput * moveSpeed);
        }
        else
        {
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
        }
    }

    // 1-2영역처럼 회전된 발판 여러 개를 이어 붙이거나 Polygon/EdgeCollider2D로 곡선 발판을
    // 만든 지형에서는, 발밑으로 레이캐스트를 쏴서 실제 지면의 법선(기울기)을 구한다 - 콜라이더
    // 종류와 무관하게 맞은 지점의 법선만 사용하므로 박스든 회전된 박스든 곡선이든 동일하게 동작한다.
    // 걸어서 오를 수 있는 경사(maxWalkableSlopeAngle 이하)일 때는 y좌표도 매 프레임 실제 표면
    // 높이에 맞춰 살짝 눌러 붙인다(타일 이음매마다 뜨거나 파고드는 것을 방지, 접선 이동만으로는
    // 이음매의 미세한 높이 차이까지 완전히 없애지 못하기 때문).
    // 점프해서 위로 상승 중(isJumping)이거나 접지 중이 아니면 평지(법선 (0,1))로 되돌려 일반
    // 포물선 운동과 걸을 수 없는 급경사(벽에 가까움)에서의 기존 수평 이동 방식을 그대로 유지한다.
    // 주의: 예전에는 "velocity.y > 0.01f"로 상승 여부를 판단했는데, 그러면 경사면 시작 지점에
    // 빠른 속도로 부딪혔을 때 물리 엔진이 만드는 순간적인 튕김(점프가 아닌데 y속도가 생기는 경우)도
    // "점프 중"으로 오판해서 경사 보정이 꺼져버리고, 보정이 꺼진 채 계속 튕기며 미끄러지다 걸려서
    // 멈추는 문제가 있었다(실제 테스트로 재현). isJumping은 점프 버튼 입력에서만 켜지므로 이 오판이
    // 없고, 오히려 충돌로 인한 튕김은 바로 다음 프레임부터 접선 방향 이동 + y 스냅으로 즉시 흡수된다.
    private void UpdateGroundNormalAndSnap()
    {
        groundNormal = Vector2.up;
        if (!isGrounded || isJumping || groundCheck == null) return;

        // groundCheck는 발이 표면에 딱 붙어있는 지점이라, 그 자리에서 바로 레이캐스트를 쏘면 시작점이
        // 이미 콜라이더 안/표면에 있는 것으로 처리되어 Physics2D가 실제 표면 법선 대신 distance=0과
        // 함께 광선의 반대 방향(항상 (0,1))을 돌려준다 - 경사면 위에서 매번 평지로 오판하는 원인이었다
        // (실측으로 확인). 그래서 살짝 위에서 쏴 시작점이 콜라이더 밖에 있도록 만든다.
        Vector2 rayOrigin = (Vector2)groundCheck.position + Vector2.up * 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, slopeCheckDistance + 0.1f, groundLayer);
        if (hit.collider == null) return;

        float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
        if (slopeAngle < 0.5f || slopeAngle > maxWalkableSlopeAngle) return; // 평지거나 걸어 오를 수 없는 급경사면은 보정 생략

        groundNormal = hit.normal;

        float groundCheckOffsetY = groundCheck.position.y - rb.position.y; // 회전이 없으므로 항상 일정한 값
        rb.position = new Vector2(rb.position.x, hit.point.y - groundCheckOffsetY);
    }

    // 좌우 반전은 스프라이트가 손에 든 무기/방패 위치까지 바뀌어버리므로 스케일 미러링 대신
    // 방향 상태만 전환한다. 실제 표시되는 스프라이트 교체는 PlayerSpriteAnimator가 담당한다.
    private void Flip()
    {
        FacingRight = !FacingRight;
    }
}
