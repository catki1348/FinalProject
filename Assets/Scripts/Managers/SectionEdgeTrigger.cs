using UnityEngine;

// 구간 경계 트리거: 플레이어가 지나가는 방향(이동 속도 부호)에 따라 이전/다음 구간으로 카메라 전환을 요청
[RequireComponent(typeof(BoxCollider2D))]
public class SectionEdgeTrigger : MonoBehaviour
{
    [SerializeField] private int previousSectionIndex;
    [SerializeField] private int nextSectionIndex;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Rigidbody2D playerBody = other.attachedRigidbody;
        if (playerBody == null || !playerBody.TryGetComponent<PlayerMovement>(out _)) return;

        int targetSection = playerBody.linearVelocity.x >= 0f ? nextSectionIndex : previousSectionIndex;
        StageManager.Instance.MoveToSection(targetSection);
    }
}
