using UnityEngine;

// 구간 경계 트리거: 플레이어가 지나가는 방향(이동 속도 부호)에 따라 카메라의 활성 구간(CameraBoundsZone)을 전환
[RequireComponent(typeof(BoxCollider2D))]
public class SectionEdgeTrigger : MonoBehaviour
{
    [SerializeField] private CameraBoundsZone previousZone;
    [SerializeField] private CameraBoundsZone nextZone;
    [SerializeField] private PlayerCameraFollow cameraFollow;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Rigidbody2D playerBody = other.attachedRigidbody;
        if (playerBody == null || !playerBody.TryGetComponent<PlayerMovement>(out _)) return;

        cameraFollow.SetActiveZone(playerBody.linearVelocity.x >= 0f ? nextZone : previousZone);
    }
}
