using UnityEngine;

// 카메라가 대상(플레이어)을 부드럽게 따라가게 하는 최소 구현의 추적 스크립트
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset = new Vector2(0f, 1f);
    [SerializeField] private float smoothTime = 0.15f;

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
    }
}
