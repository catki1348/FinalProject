using UnityEngine;

// 워프(텔레포트) 함정: 같은 오브젝트의 SwitchTrigger가 밟힘을 감지하면 플레이어를 영역 시작점(또는 지정 지점)으로 날려버린다.
// 별도 스위치 없이 마법진 자체가 발판(스위치) 역할을 하며, SwitchTrigger를 1회성(oneShot)으로 두면 한 번 발동한 뒤 더 이상 작동하지 않는다.
// 예고 없이 즉시 이동하고, 이동 직후 도착 이펙트가 플레이어 주변에 나타난다.
// 이동 방식은 TeleportTrap과 같다(rb.position 직접 이동 + 속도 초기화 + 카메라 즉시 이동).
// 이동 발판 탑승 상태는 MovingPlatform이 다음 스텝에 스스로 정리하므로 부모를 건드리지 않는다.
// 재발동 방지(쿨다운/1회성)는 SwitchTrigger가 담당한다.
public class SwitchTeleportTrap : MonoBehaviour
{
    [SerializeField] private SwitchTrigger switchTrigger;       // 이 함정을 발동시키는 스위치
    [SerializeField] private ZoneStartPoint startPoint;         // 기본 목적지(각 영역의 시작점)
    [SerializeField] private Transform customDestination;       // 지정 시 startPoint보다 우선
    [SerializeField] private CameraBoundsZone destinationZone;  // 목적지가 속한 카메라 구간
    [SerializeField] private PlayerCameraFollow cameraFollow;
    [SerializeField] private int penaltyDamage = 0;             // 0이면 피해 없음, 필요 시 인스펙터에서 조절
    [SerializeField] private AudioClip teleportSfx;
    [SerializeField] private TeleportArrivalEffect arrivalEffectPrefab; // 도착 후 플레이어 주변에 나타나는 이펙트(비우면 없음)

    // 목적지: 지정 지점 > 영역 시작점
    private Vector2 Destination => customDestination != null ? customDestination.position : startPoint.transform.position;

    private void OnEnable()
    {
        if (switchTrigger != null) switchTrigger.OnActivatedByPlayer += HandleActivated;
    }

    private void OnDisable()
    {
        if (switchTrigger != null) switchTrigger.OnActivatedByPlayer -= HandleActivated;
    }

    // 플레이어를 목적지로 옮기고 속도 초기화 + 카메라 즉시 이동 + 도착 이펙트 + 효과음 + (선택) 피해
    private void HandleActivated(Rigidbody2D playerBody)
    {
        Vector2 destination = Destination;
        playerBody.position = destination;
        playerBody.linearVelocity = Vector2.zero;
        cameraFollow.SnapTo(destination, destinationZone);

        if (arrivalEffectPrefab != null)
        {
            TeleportArrivalEffect effect = Instantiate(arrivalEffectPrefab, destination, Quaternion.identity);
            effect.Play(playerBody.transform);
        }
        SoundManager.Instance.PlaySfx(teleportSfx);

        if (penaltyDamage > 0 && playerBody.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(penaltyDamage);
    }

    // 필수 참조 누락 경고
    private void OnValidate()
    {
        if (!gameObject.scene.IsValid()) return; // 프리팹 에셋 자체에서는 씬 참조를 연결할 수 없으므로 검사 생략
        if (switchTrigger == null || cameraFollow == null || (startPoint == null && customDestination == null))
            Debug.LogWarning($"{name}: switchTrigger / cameraFollow / (startPoint 또는 customDestination) 연결이 필요합니다.", this);
    }

    // 선택 시 스위치→함정(노랑), 함정→목적지(자홍) 연결선 표시
    private void OnDrawGizmosSelected()
    {
        if (switchTrigger != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(switchTrigger.transform.position, transform.position);
        }

        Transform dest = customDestination != null ? customDestination : (startPoint != null ? startPoint.transform : null);
        if (dest == null) return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, dest.position);
        Gizmos.DrawWireSphere(dest.position, 0.4f);
    }
}
