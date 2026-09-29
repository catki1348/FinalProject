using UnityEngine;

// 비행형 정찰 패턴: 스폰 위치를 중심으로 한 지정 영역 안에서 대각선으로 왕복하며(상하좌우 전부 사용) 이동한다.
// 지상형 PatrolPattern과 달리 좌우 경계뿐 아니라 상하 경계에서도 반사되어, 벽에 튕기는 공처럼 영역을 순회한다.
// 중력을 받지 않는 비행종(Monster.md 규칙 10)을 전제로 하며, Rigidbody2D.gravityScale은 0으로 설정해서 사용한다.
[RequireComponent(typeof(Rigidbody2D))]
public class FlyingPatrolPattern : MonoBehaviour, IMonsterPattern
{
    [SerializeField] private float moveSpeed = 2f;                     // 이동 속도
    [SerializeField] private Vector2 areaSize = new Vector2(6f, 4f);   // 정찰 영역 크기(스폰 위치가 중심)

    private Rigidbody2D rb;
    private Bounds area;
    private Vector2 direction = new Vector2(1f, 1f).normalized; // 대각선 시작 방향

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        area = new Bounds(transform.position, areaSize);
    }

    // 영역의 좌우 경계에 닿으면 X 방향을, 상하 경계에 닿으면 Y 방향을 각각 독립적으로 반전한다
    public void UpdatePattern()
    {
        Vector2 pos = transform.position;
        if (pos.x >= area.max.x) direction.x = -Mathf.Abs(direction.x);
        else if (pos.x <= area.min.x) direction.x = Mathf.Abs(direction.x);
        if (pos.y >= area.max.y) direction.y = -Mathf.Abs(direction.y);
        else if (pos.y <= area.min.y) direction.y = Mathf.Abs(direction.y);

        rb.linearVelocity = direction.normalized * moveSpeed;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Application.isPlaying ? area.center : transform.position, areaSize);
    }
}
