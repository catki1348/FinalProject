using UnityEngine;

// 5영역(보스존)의 골인 지점 마커. 기획 컨셉 이미지의 원(○)에 대응한다.
// 일반 ZoneEndPoint와 달리 처음에는 비활성 상태이며, Boss.OnBossDefeated 이벤트를 받은 뒤에만
// 콜라이더가 켜져 실제로 통과할 수 있다(MapEdit.md 규칙 8: 보스를 처치해야 종료점이 생성됨).
// 규칙 36: GameObject.Find로 보스를 찾지 않고 정적 이벤트 구독만으로 연결한다.
[RequireComponent(typeof(BoxCollider2D))]
public class BossZoneEndPoint : MonoBehaviour
{
    private BoxCollider2D endPointCollider;
    private bool triggered;

    private void Awake()
    {
        endPointCollider = GetComponent<BoxCollider2D>();
        endPointCollider.isTrigger = true;
        endPointCollider.enabled = false; // 보스 처치 전에는 통과할 수 없음
    }

    private void OnEnable()
    {
        Boss.OnBossDefeated += Activate;
    }

    private void OnDisable()
    {
        Boss.OnBossDefeated -= Activate;
    }

    private void Activate()
    {
        endPointCollider.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (other.attachedRigidbody == null || !other.attachedRigidbody.TryGetComponent<PlayerMovement>(out _)) return;

        triggered = true;
        StageManager.Instance.OnZone5EndPointReached();
    }

    private void OnDrawGizmos()
    {
        bool active = endPointCollider != null && endPointCollider.enabled;
        Gizmos.color = active ? Color.yellow : Color.gray;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
