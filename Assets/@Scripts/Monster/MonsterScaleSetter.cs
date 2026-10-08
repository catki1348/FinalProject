using UnityEngine;

// 몬스터가 배치(스폰)될 때 크기를 플레이어 크기의 일정 비율로 맞추는 스크립트
// 크기 설정만 담당하도록 스탯/패턴/이동 스크립트와 분리한다(규칙 39 SRP)
// 스프라이트마다 캔버스 여백이 달라 transform 스케일 비율로는 실제 크기가 맞지 않으므로,
// 플레이어 외형의 월드 높이 × 비율 = 몬스터 외형의 월드 높이가 되도록 스케일을 계산한다.
public class MonsterScaleSetter : MonoBehaviour
{
    [SerializeField] private float scaleRatio = 0.5f; // 플레이어 크기 대비 비율(기본값: 절반)

    // 모든 Awake(플레이어의 GameManager 등록 포함)가 끝난 뒤 실행되도록 Start에서 처리
    // (GameManager.PlayerTransform은 PlayerMovement.Awake에서 등록됨 - Find 없이 조회, 규칙 40, 41)
    private void Start()
    {
        Transform player = GameManager.Instance.PlayerTransform;
        ApplyScale(player);
    }

    // 스폰 시 외형 높이를 맞추고 발밑 위치를 유지한다. 부모/Visual 자식의 배율도 월드 크기에 반영한다.
    public void ApplyScale(Transform player)
    {
        if (player == null) return;

        SpriteRenderer playerRenderer = player.GetComponentInChildren<SpriteRenderer>();
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (playerRenderer == null || playerRenderer.sprite == null ||
            spriteRenderer == null || spriteRenderer.sprite == null) return;

        float height = spriteRenderer.bounds.size.y;
        if (height <= Mathf.Epsilon || scaleRatio <= 0f) return;

        Collider2D body = GetComponent<Collider2D>();
        float feetY = body != null ? body.bounds.min.y : spriteRenderer.bounds.min.y;
        float factor = playerRenderer.bounds.size.y * scaleRatio / height;
        Vector3 scale = transform.localScale;
        transform.localScale = new Vector3(scale.x * factor, scale.y * factor, scale.z);
        Physics2D.SyncTransforms();
        float newFeetY = body != null ? body.bounds.min.y : spriteRenderer.bounds.min.y;
        transform.position += Vector3.up * (feetY - newFeetY);
    }
}
