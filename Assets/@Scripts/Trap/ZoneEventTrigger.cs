using UnityEngine;

// 플레이어가 트리거 영역에 들어오면 파생 클래스가 정의한 동작을 실행하는 베이스 클래스.
// SectionEdgeTrigger/OutOfBoundsTrigger와 같은 방식(트리거 콜라이더 + OnTriggerEnter2D)으로 플레이어만 식별한다.
[RequireComponent(typeof(Collider2D))]
public abstract class ZoneEventTrigger : MonoBehaviour
{
    [SerializeField] private bool oneShot = true;              // 1회만 발동(앰부시 등). 텔레포트 프리팹은 false로 둔다
    [SerializeField] private float retriggerCooldown = 0.5f;   // 반복 발동 시 최소 간격(목적지가 영역에 걸쳐도 무한 루프 방지)

    private bool triggered;
    private float lastTriggerTime = -999f;

    protected virtual Color GizmoColor => Color.magenta;       // 씬 뷰 영역 색(파생 클래스가 구분용으로 override)

    // 콜라이더를 항상 트리거로 맞춘다
    protected virtual void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    // 플레이어만 통과시켜 파생 동작을 호출(oneShot/쿨다운 검사 포함)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (oneShot && triggered) return;
        if (Time.time - lastTriggerTime < retriggerCooldown) return;

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent<PlayerMovement>(out _)) return;

        triggered = true;
        lastTriggerTime = Time.time;
        OnPlayerEnterZone(body);
    }

    // 파생 클래스가 실제 발동 동작(텔레포트, 몬스터 스폰 등)을 정의
    protected abstract void OnPlayerEnterZone(Rigidbody2D playerBody);

    // 씬 뷰에서 발동 영역을 색 박스로 표시
    private void OnDrawGizmos()
    {
        if (!TryGetComponent<Collider2D>(out var col)) return;
        Gizmos.color = GizmoColor;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
