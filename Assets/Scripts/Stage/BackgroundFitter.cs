using UnityEngine;

// 배경 스프라이트가 카메라의 보이는 영역을 여백 없이 꽉 채우도록 크기를 자동 계산해 적용
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundFitter : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    private void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        Fit();
    }

    private void Fit()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        Vector2 nativeSize = spriteRenderer.sprite.bounds.size;

        float screenHeight = targetCamera.orthographicSize * 2f;
        float screenWidth = screenHeight * targetCamera.aspect;

        Vector3 scale = transform.localScale;
        scale.x = screenWidth / nativeSize.x;
        scale.y = screenHeight / nativeSize.y;
        transform.localScale = scale;

        Vector3 position = transform.position;
        position.x = targetCamera.transform.position.x;
        position.y = targetCamera.transform.position.y;
        transform.position = position;
    }
}
