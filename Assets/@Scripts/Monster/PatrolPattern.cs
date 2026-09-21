using UnityEngine;

// 정찰형 패턴: 스폰 위치를 중심으로 한 지정 영역 안에서 좌우로 왕복 이동한다(Monster.md 패턴 1)
[RequireComponent(typeof(Rigidbody2D))]
public class PatrolPattern : MonoBehaviour, IMonsterPattern
{
    [SerializeField] private float moveSpeed = 2f;                     // 이동 속도
    [SerializeField] private Vector2 areaSize = new Vector2(6f, 2f);   // 정찰 영역 크기(스폰 위치가 중심)

    private Rigidbody2D rb;
    private Bounds area;
    private float direction = 1f; // 1 = 오른쪽, -1 = 왼쪽

    // 시작 시 Rigidbody2D를 캐싱하고, 현재 위치를 중심으로 한 정찰 영역을 고정
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        area = new Bounds(transform.position, areaSize);
    }

    // 영역의 좌우 경계에 닿으면 방향을 반전하고, 그 외에는 현재 방향으로 계속 이동
    public void UpdatePattern()
    {
        if (transform.position.x >= area.max.x) direction = -1f;
        else if (transform.position.x <= area.min.x) direction = 1f;

        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
    }

    // 씬 뷰에서 정찰 영역을 시각적으로 확인하기 위한 디버그용 기즈모
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(Application.isPlaying ? area.center : transform.position, areaSize);
    }
}
