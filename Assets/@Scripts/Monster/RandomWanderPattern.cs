using UnityEngine;

// 랜덤형 패턴: 지정된 영역 안에서 무작위 위치를 골라 이동하고, 도착하면 잠시 대기한 뒤 다시 무작위로 이동한다(Monster.md 패턴 3)
[RequireComponent(typeof(Rigidbody2D))]
public class RandomWanderPattern : MonoBehaviour, IMonsterPattern
{
    [SerializeField] private Vector2 areaSize = new Vector2(6f, 2f); // 이동 가능 영역 크기(스폰 위치가 중심)
    [SerializeField] private float moveSpeed = 1.5f;         // 이동 속도
    [SerializeField] private float arrivalThreshold = 0.1f;  // 목표 지점 도착으로 판정할 거리
    [SerializeField] private float minWaitTime = 1f;         // 도착 후 대기 최소 시간(초)
    [SerializeField] private float maxWaitTime = 3f;         // 도착 후 대기 최대 시간(초)

    private Rigidbody2D rb;
    private Bounds area;
    private float targetX;
    private float waitUntil;

    // 시작 시 Rigidbody2D를 캐싱하고, 현재 위치를 중심으로 한 이동 영역을 고정한 뒤 첫 목표 지점을 선택
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        area = new Bounds(transform.position, areaSize);
        PickNewTarget();
    }

    // 대기 중이면 정지, 목표 지점에 도착했으면 잠시 대기 후 새 목표를 선택, 그 외에는 목표 지점 방향으로 이동
    public void UpdatePattern()
    {
        if (Time.time < waitUntil)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float distance = targetX - transform.position.x;
        if (Mathf.Abs(distance) <= arrivalThreshold)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            waitUntil = Time.time + Random.Range(minWaitTime, maxWaitTime);
            PickNewTarget();
            return;
        }

        rb.linearVelocity = new Vector2(Mathf.Sign(distance) * moveSpeed, rb.linearVelocity.y);
    }

    // 영역 안의 x좌표 하나를 무작위로 골라 다음 목표 지점으로 설정
    private void PickNewTarget()
    {
        targetX = Random.Range(area.min.x, area.max.x);
    }

    // 씬 뷰에서 이동 가능 영역을 시각적으로 확인하기 위한 디버그용 기즈모
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Application.isPlaying ? area.center : transform.position, areaSize);
    }
}
