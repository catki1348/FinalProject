using UnityEngine;

// 플레이어가 이 영역(트리거) 안에 있는 동안 메인 카메라를 더 넓게(orthographicSize 이상) 보여 주는 확대 구역.
// 구간(CameraBoundsZone)의 기본 크기보다 큰 값만 의미가 있다. 여러 구역이 겹치면 가장 큰 값을 쓴다.
// 이동형 발판이 위아래로 길게 이어져 "다음 발판이 화면 밖에 있는" 곳(예: 5-2의 승강 발판 구간)에서
// 현재 서 있는 발판에서 다음 발판이 보이도록 카메라를 넓히는 용도다(규칙 36: 참조는 Inspector 할당, Find 금지).
[RequireComponent(typeof(BoxCollider2D))]
public class CameraZoomVolume : MonoBehaviour
{
    [SerializeField] private float orthographicSize = 8f;
    [SerializeField] private PlayerCameraFollow cameraFollow;

    public float OrthographicSize => orthographicSize;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other)) cameraFollow.AddZoomVolume(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other)) cameraFollow.RemoveZoomVolume(this);
    }

    // 비활성화/파괴되면 카메라가 계속 넓은 상태로 남지 않게 목록에서 뺀다
    private void OnDisable()
    {
        if (cameraFollow != null) cameraFollow.RemoveZoomVolume(this);
    }

    private static bool IsPlayer(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody;
        return body != null && body.TryGetComponent<PlayerMovement>(out _);
    }

    private void OnDrawGizmos()
    {
        if (!TryGetComponent<BoxCollider2D>(out var box)) return;
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
    }
}
