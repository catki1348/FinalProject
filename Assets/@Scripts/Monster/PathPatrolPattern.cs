using UnityEngine;

// 지상형 경로 정찰 패턴: 움직이는 발판과 동일한 PlatformPath 컴포넌트로 이동 동선을 정의한다(Monster.md 규칙 12).
// 경로 지점의 X좌표만 목표로 삼아 좌우로 걸어서 왕복하고, 높이(Y)는 중력과 바닥 충돌이 그대로 결정한다
// (발판 위를 실제로 걸어 다니는 지상종이므로, 발판 굴곡을 따라 자연스럽게 오르내린다).
[RequireComponent(typeof(Rigidbody2D))]
public class PathPatrolPattern : MonoBehaviour, IMonsterPattern
{
    [SerializeField] private PlatformPath path;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float arrivalThreshold = 0.1f; // 목표 X좌표 도착으로 판정할 거리

    private int targetIndex = 1;
    private int direction = 1; // 마지막 지점에서 방향을 반전하는 PingPong 전용(경로는 왕복만 지원)
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // 시작 시 첫 지점의 X좌표로 위치를 맞춘다(Y는 그대로 두어 스폰 시 중력으로 바닥에 안착)
    private void Start()
    {
        if (path == null || path.PointCount == 0) return;
        Vector3 pos = transform.position;
        pos.x = path.GetPoint(0).x;
        transform.position = pos;
    }

    // 목표 지점의 X좌표를 향해 좌우로 이동하고, 도착하면 다음 지점(왕복)으로 목표를 갱신한다
    public void UpdatePattern()
    {
        if (path == null || path.PointCount < 2)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float targetX = path.GetPoint(targetIndex).x;
        float distance = targetX - transform.position.x;

        if (Mathf.Abs(distance) <= arrivalThreshold)
        {
            AdvanceTarget();
            return;
        }

        rb.linearVelocity = new Vector2(Mathf.Sign(distance) * moveSpeed, rb.linearVelocity.y);
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
