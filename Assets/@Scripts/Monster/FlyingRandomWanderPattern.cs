using UnityEngine;

// 비행형 랜덤 패턴: 지정된 영역 안에서 무작위 2D 위치(X,Y 모두)를 골라 이동하고, 도착하면 잠시 대기한 뒤 다시 무작위로 이동한다.
// 지상형 RandomWanderPattern은 X좌표만 무작위로 고르지만(높이는 중력이 결정), 비행형은 중력이 없으므로 Y도 함께 무작위로 고른다.
[RequireComponent(typeof(Rigidbody2D))]
public class FlyingRandomWanderPattern : MonoBehaviour, IMonsterPattern
{
    [SerializeField] private Vector2 areaSize = new Vector2(6f, 4f); // 이동 가능 영역 크기(스폰 위치가 중심)
    [SerializeField] private float moveSpeed = 1.5f;         // 이동 속도
    [SerializeField] private float arrivalThreshold = 0.15f; // 목표 지점 도착으로 판정할 거리
    [SerializeField] private float minWaitTime = 1f;         // 도착 후 대기 최소 시간(초)
    [SerializeField] private float maxWaitTime = 3f;         // 도착 후 대기 최대 시간(초)

    private Rigidbody2D rb;
    private Bounds area;
    private Vector2 target;
    private float waitUntil;

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
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toTarget = target - (Vector2)transform.position;
        if (toTarget.magnitude <= arrivalThreshold)
        {
            rb.linearVelocity = Vector2.zero;
            waitUntil = Time.time + Random.Range(minWaitTime, maxWaitTime);
            PickNewTarget();
            return;
        }

        rb.linearVelocity = toTarget.normalized * moveSpeed;
    }

    // 영역 안의 (x, y) 좌표를 무작위로 골라 다음 목표 지점으로 설정
    private void PickNewTarget()
    {
        target = new Vector2(Random.Range(area.min.x, area.max.x), Random.Range(area.min.y, area.max.y));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Application.isPlaying ? area.center : transform.position, areaSize);
    }
}
