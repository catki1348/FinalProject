using UnityEngine;

// 비행형 경로 정찰 패턴: 움직이는 발판과 동일한 PlatformPath 컴포넌트로 이동 동선을 정의한다(Monster.md 규칙 12).
// 지상형 PathPatrolPattern과 달리 경로 지점의 X·Y 좌표를 모두 목표로 삼아 직선으로 날아가며,
// 중력의 영향을 받지 않으므로 도착 후에도 발판에 내려앉지 않고 그대로 다음 지점으로 향한다(규칙 10).
[RequireComponent(typeof(Rigidbody2D))]
public class FlyingPathPatrolPattern : MonoBehaviour, IMonsterPattern
{
    [SerializeField] private PlatformPath path;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float arrivalThreshold = 0.1f; // 목표 지점 도착으로 판정할 거리

    private int targetIndex = 1;
    private int direction = 1; // 마지막 지점에서 방향을 반전하는 PingPong 전용(경로는 왕복만 지원)
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // 시작 시 첫 지점 위치로 정확히 맞춘다(비행형은 중력이 없어 Y도 직접 스냅해야 함)
    private void Start()
    {
        if (path == null || path.PointCount == 0) return;
        transform.position = path.GetPoint(0);
    }

    // 목표 지점(X,Y)을 향해 직선으로 날아가고, 도착하면 다음 지점(왕복)으로 목표를 갱신한다
    public void UpdatePattern()
    {
        if (path == null || path.PointCount < 2)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 target = path.GetPoint(targetIndex);
        Vector2 toTarget = target - (Vector2)transform.position;

        if (toTarget.magnitude <= arrivalThreshold)
        {
            AdvanceTarget();
            return;
        }

        rb.linearVelocity = toTarget.normalized * moveSpeed;
    }

    // 마지막 지점에 닿으면 방향을 반전해 되돌아가는 PingPong 방식으로 다음 목표를 고른다
    private void AdvanceTarget()
    {
        int count = path.PointCount;
        int current = targetIndex;

        if (current == count - 1) direction = -1;
        else if (current == 0) direction = 1;

        targetIndex = current + direction;
    }
}
