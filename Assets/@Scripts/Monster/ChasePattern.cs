using UnityEngine;

// 공격형 패턴: 지정된 영역에 플레이어가 들어오면 점프하며 그쪽으로 쫓아간다(Monster.md 패턴 2)
[RequireComponent(typeof(Rigidbody2D))]
public class ChasePattern : MonoBehaviour, IMonsterPattern
{
    [SerializeField] private Vector2 detectionSize = new Vector2(8f, 4f); // 플레이어 감지 영역 크기(스폰 위치가 중심)
    [SerializeField] private LayerMask playerLayer;     // 플레이어로 인식할 레이어(태그/Find 대신 레이어 사용 - 규칙 37)
    [SerializeField] private float moveSpeed = 3f;      // 추적 시 좌우 이동 속도
    [SerializeField] private float jumpForce = 7f;      // 점프 시 가하는 상승 힘
    [SerializeField] private float jumpInterval = 0.8f; // 점프 재사용 간격(초)
    [SerializeField] private Transform groundCheck;     // 바닥 감지 기준점(발밑에 배치)
    [SerializeField] private float groundCheckRadius = 0.1f; // 바닥 감지 원의 반지름
    [SerializeField] private LayerMask groundLayer;     // 바닥으로 인식할 레이어

    private Rigidbody2D rb;
    private Vector2 detectionCenter; // 감지 영역 중심(스폰 위치에 고정, 몬스터가 움직여도 변하지 않음)
    private float lastJumpTime = -999f;

    // 시작 시 Rigidbody2D를 캐싱하고, 현재 위치를 감지 영역의 중심으로 고정
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        detectionCenter = transform.position;
    }

    // 감지 영역 안에 플레이어가 있으면 그 방향으로 이동하며 주기적으로 점프하고, 없으면 제자리에 정지
    public void UpdatePattern()
    {
        Collider2D player = Physics2D.OverlapBox(detectionCenter, detectionSize, 0f, playerLayer);
        if (player == null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float direction = Mathf.Sign(player.transform.position.x - transform.position.x);
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);

        bool isGrounded = groundCheck != null &&
            Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (isGrounded && Time.time - lastJumpTime >= jumpInterval)
        {
            lastJumpTime = Time.time;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    // 씬 뷰에서 감지 영역을 시각적으로 확인하기 위한 디버그용 기즈모
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Application.isPlaying ? (Vector3)detectionCenter : transform.position, detectionSize);
    }
}
