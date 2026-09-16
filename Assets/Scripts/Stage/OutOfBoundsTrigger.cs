using UnityEngine;

// 필드(맵) 밖으로 나가면 게임오버를 발생시키는 트리거
[RequireComponent(typeof(BoxCollider2D))]
public class OutOfBoundsTrigger : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody == null || !other.attachedRigidbody.TryGetComponent<PlayerMovement>(out _)) return;
        GameOverManager.Instance.TriggerGameOver();
    }
}
