using UnityEngine;

// 플레이어가 도달하면 스테이지 클리어를 알리는 최소 구현의 목표 지점(트리거 영역)
[RequireComponent(typeof(Collider2D))]
public class GoalZone : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody == null || !other.attachedRigidbody.TryGetComponent<PlayerMovement>(out _)) return;

        Debug.Log("STAGE CLEAR");
    }
}
