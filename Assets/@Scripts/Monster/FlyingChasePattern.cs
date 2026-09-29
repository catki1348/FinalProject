using UnityEngine;

// 비행형 공격 패턴: 지정된 영역에 플레이어가 들어오면 곧바로 그 방향(상하좌우 전부)으로 날아가 쫓아간다.
// 지상형 ChasePattern과 달리 점프/바닥 감지가 필요 없다 - 이미 중력의 영향을 받지 않는 비행종이므로
// 목표 지점을 향한 방향 벡터를 그대로 속도로 사용한다.
[RequireComponent(typeof(Rigidbody2D))]
public class FlyingChasePattern : MonoBehaviour, IMonsterPattern
{
    [SerializeField] private Vector2 detectionSize = new Vector2(8f, 4f); // 플레이어 감지 영역 크기(스폰 위치가 중심)
    [SerializeField] private LayerMask playerLayer;  // 플레이어로 인식할 레이어(태그/Find 대신 레이어 사용 - 규칙 41)
    [SerializeField] private float moveSpeed = 3f;   // 추적 시 이동 속도

    private Rigidbody2D rb;
    private Vector2 detectionCenter; // 감지 영역 중심(스폰 위치에 고정, 몬스터가 움직여도 변하지 않음)

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        detectionCenter = transform.position;
    }

    // 감지 영역 안에 플레이어가 있으면 그 방향(2D 벡터)으로 곧장 날아가고, 없으면 제자리에 정지한다
    public void UpdatePattern()
    {
        Collider2D player = Physics2D.OverlapBox(detectionCenter, detectionSize, 0f, playerLayer);
        if (player == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
        rb.linearVelocity = toPlayer.normalized * moveSpeed;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Application.isPlaying ? (Vector3)detectionCenter : transform.position, detectionSize);
    }
}
