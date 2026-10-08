using UnityEngine;

// 한 구간(CameraBoundsZone)에서 카메라가 비출 수 있는 범위 전체를 테마 배경 스프라이트로 빈틈없이 덮는다
// (CameraDesign_FollowCamera.md 3항: 플레이 화면 어디에도 여백이 비치면 안 된다).
// - 세로: 그 범위의 높이에 맞춰 원본 비율 그대로 균일 확대(찌그러짐 없음)
// - 가로: SpriteRenderer의 Tiled 모드로 반복(좌우 끝이 자연스럽게 이어지는 이미지를 사용해야 이음매가 안 보임)
// 구간 경계(Boundary)나 크기를 옮기면 에디터에서 즉시 다시 맞춰지므로, 배치 상태와 플레이 상태의 배경이 항상 같다.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class ZoneBackground : MonoBehaviour
{
    // 구간이 화면보다 좁은 축은 카메라가 구간 중앙에 고정된 채 화면 크기만큼 구간 바깥까지 비춘다
    // (PlayerCameraFollow.ClampAxis). 지원할 가장 넓은 화면비(21:9) 기준으로 그 바깥까지 미리 덮어 둔다.
    private const float MaxScreenAspect = 21f / 9f;

    [SerializeField] private CameraBoundsZone zone; // 이 배경이 덮을 구간

    private SpriteRenderer spriteRenderer;

    // 씬을 열거나 오브젝트가 켜질 때 한 번 맞춘다
    private void OnEnable()
    {
        Fit();
    }

    // 에디터에서만: 구간 경계를 옮기는 등 씬이 바뀔 때마다 다시 맞춘다(플레이 중에는 구간이 고정이라 불필요).
    // 값이 실제로 달라졌을 때만 대입하므로 매번 씬이 변경 상태로 표시되지 않는다.
    private void Update()
    {
        if (!Application.isPlaying) Fit();
    }

    // 구간에서 카메라가 비출 수 있는 월드 범위를 계산해, 배경을 그 범위와 정확히 같은 크기로 배치한다
    private void Fit()
    {
        if (zone == null) return;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        Sprite sprite = spriteRenderer.sprite;
        if (sprite == null) return;

        Bounds zoneBounds = zone.WorldBounds;
        float halfHeight = zone.OrthographicSize;
        float halfWidth = halfHeight * MaxScreenAspect;
        float minX = Mathf.Min(zoneBounds.min.x, zoneBounds.center.x - halfWidth);
        float maxX = Mathf.Max(zoneBounds.max.x, zoneBounds.center.x + halfWidth);
        float minY = Mathf.Min(zoneBounds.min.y, zoneBounds.center.y - halfHeight);
        float maxY = Mathf.Max(zoneBounds.max.y, zoneBounds.center.y + halfHeight);

        Vector2 spriteSize = sprite.bounds.size;
        float scale = (maxY - minY) / spriteSize.y;
        Vector2 size = new Vector2((maxX - minX) / scale, spriteSize.y);

        // 부모가 늘어나 있어도 최종 월드 배율이 균일(scale)하도록 보정
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        Vector3 localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, 1f);

        // Tiled 모드는 피벗 기준으로 그려지므로, 피벗이 중앙이 아니어도 범위 중심에 오도록 보정
        Vector2 pivotOffset = (sprite.pivot / sprite.rect.size - new Vector2(0.5f, 0.5f)) * size * scale;
        Vector3 position = new Vector3((minX + maxX) * 0.5f + pivotOffset.x, (minY + maxY) * 0.5f + pivotOffset.y, transform.position.z);

        if (transform.position != position) transform.position = position;
        if (transform.localScale != localScale) transform.localScale = localScale;
        if (spriteRenderer.drawMode != SpriteDrawMode.Tiled) spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        if (spriteRenderer.tileMode != SpriteTileMode.Continuous) spriteRenderer.tileMode = SpriteTileMode.Continuous;
        if (spriteRenderer.size != size) spriteRenderer.size = size;
    }
}
