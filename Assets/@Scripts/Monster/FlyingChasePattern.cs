using UnityEngine;

// 비행형 공격 패턴: 지정된 영역에 플레이어가 들어오면 곧바로 그 방향(상하좌우 전부)으로 날아가 쫓아간다.
// 지상형 ChasePattern과 달리 점프/바닥 감지가 필요 없다 - 이미 중력의 영향을 받지 않는 비행종이므로
// 목표 지점을 향한 방향 벡터를 그대로 속도로 사용한다.
//
// 플레이어가 감지 영역 밖에 있을 때(평소 상태): path가 따로 지정되어 있으면 그 경로를 왕복하고,
// 지정된 경로가 없으면 스폰 지점을 중심으로 한 원형 궤도를 자동으로 돈다(Monster.md 규칙 15 - 비행형
// 몬스터는 별도 경로 지정이 없으면 원형 경로로 동작하는 체이서 타입이 기본값).
[RequireComponent(typeof(Rigidbody2D))]
public class FlyingChasePattern : MonoBehaviour, IMonsterPattern
{
    [SerializeField] private Vector2 detectionSize = new Vector2(8f, 4f); // 플레이어 감지 영역 크기(스폰 위치가 중심)
    [SerializeField] private LayerMask playerLayer;  // 플레이어로 인식할 레이어(태그/Find 대신 레이어 사용 - 규칙 41)
    [SerializeField] private float moveSpeed = 3f;   // 추적 시 이동 속도

    [SerializeField] private PlatformPath path;              // 평소(비추적) 동선을 직접 지정하고 싶을 때만 연결(선택 사항)
    [SerializeField] private float circleRadius = 2f;         // path가 없을 때 기본으로 도는 원형 궤도의 반지름
    [SerializeField] private float circleAngularSpeed = 60f;  // 원형 궤도를 도는 각속도(초당 도, deg/s)

    private Rigidbody2D rb;
    private Vector2 detectionCenter; // 감지 영역 중심(스폰 위치에 고정, 몬스터가 움직여도 변하지 않음)
    private Vector2 circleCenter;    // 원형 궤도의 중심(스폰 위치)
    private float orbitAngle;        // 원형 궤도 위 현재 각도(라디안)
    private int pathTargetIndex = 1;
    private int pathDirection = 1;   // 마지막 지점에서 방향을 반전하는 PingPong 전용(경로는 왕복만 지원)

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        detectionCenter = transform.position;
        circleCenter = transform.position;
    }

    // 감지 영역 안에 플레이어가 있으면 그 방향(2D 벡터)으로 곧장 날아가고,
    // 없으면 지정된 경로(또는 지정이 없을 때의 기본 원형 궤도)를 따라 평소 동선을 유지한다
    public void UpdatePattern()
    {
        Collider2D player = Physics2D.OverlapBox(detectionCenter, detectionSize, 0f, playerLayer);
        if (player != null)
        {
            Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
            rb.linearVelocity = toPlayer.normalized * moveSpeed;
            return;
        }

        if (path != null && path.PointCount >= 2) FollowPath();
        else OrbitCircle();
    }

    // path가 지정된 경우: 경로 지점(X, Y)을 향해 날아가고, 끝에 닿으면 방향을 반전해 되돌아가는 왕복 이동
    private void FollowPath()
    {
        Vector2 target = path.GetPoint(pathTargetIndex);
        Vector2 toTarget = target - (Vector2)transform.position;

        if (toTarget.magnitude <= 0.1f)
        {
            int count = path.PointCount;
            if (pathTargetIndex == count - 1) pathDirection = -1;
            else if (pathTargetIndex == 0) pathDirection = 1;
            pathTargetIndex += pathDirection;
            return;
        }

        rb.linearVelocity = toTarget.normalized * moveSpeed;
    }

    // path가 없는 경우: 스폰 지점을 중심으로 반지름 circleRadius의 원을 그리며 자동으로 순회(기본값)
    private void OrbitCircle()
    {
        orbitAngle += circleAngularSpeed * Mathf.Deg2Rad * Time.deltaTime;
        rb.position = circleCenter + new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle)) * circleRadius;
    }

    // 씬 뷰에서 감지 영역과(경로가 없을 때는) 기본 원형 궤도를 시각적으로 확인하기 위한 디버그용 기즈모
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Application.isPlaying ? (Vector3)detectionCenter : transform.position, detectionSize);

        if (path == null)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = Application.isPlaying ? (Vector3)circleCenter : transform.position;
            DrawCircleGizmo(center, circleRadius);
        }
    }

    private static void DrawCircleGizmo(Vector3 center, float radius)
    {
        const int segments = 32;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
