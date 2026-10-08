using UnityEngine;

// 플레이어가 영역에 들어오면 목적지로 즉시 이동시키고 카메라도 함께 옮기는 함정(낙하 포착/복귀형).
// StageManager.MovePlayerToStartOfCurrentArea와 같은 방식(rb.position + 카메라 구간 전환)을 따른다.
// 이동 발판 탑승 상태는 MovingPlatform이 다음 스텝에 현재 위치 기준으로 스스로 정리하므로 부모를 직접 건드리지 않는다.
public class TeleportTrap : ZoneEventTrigger
{
    [SerializeField] private Transform destination;
    [SerializeField] private CameraBoundsZone destinationZone; // 목적지가 속한 카메라 구간
    [SerializeField] private PlayerCameraFollow cameraFollow;
    [SerializeField] private int penaltyDamage = 0;            // 0이면 피해 없는 순수 복귀
    [SerializeField] private AudioClip teleportSfx;

    protected override Color GizmoColor => Color.cyan;

    // 플레이어를 목적지로 옮기고 속도 초기화 + 카메라 즉시 이동 + 효과음 + (선택) 벌칙 피해
    protected override void OnPlayerEnterZone(Rigidbody2D playerBody)
    {
        playerBody.position = destination.position;
        playerBody.linearVelocity = Vector2.zero; // 낙하 속도가 새 위치로 이어지지 않도록 초기화
        cameraFollow.SnapTo(destination.position, destinationZone);
        SoundManager.Instance.PlaySfx(teleportSfx);

        if (penaltyDamage > 0 && playerBody.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(penaltyDamage);
    }

    // 목적지가 발동 영역 안이면 이동 즉시 재발동 루프가 생기므로 경고
    private void OnValidate()
    {
        if (!gameObject.scene.IsValid()) return; // 프리팹 에셋 자체에서는 씬 참조를 연결할 수 없으므로 검사 생략
        if (destination == null || cameraFollow == null) { Debug.LogWarning($"{name}: destination / cameraFollow 연결이 필요합니다.", this); return; }

        if (TryGetComponent<Collider2D>(out var col) && col.OverlapPoint(destination.position))
            Debug.LogWarning($"{name}: 목적지가 발동 영역 안에 있습니다(재발동 루프 위험).", this);
    }

    // 선택 시 발동 영역→목적지 연결선 표시
    private void OnDrawGizmosSelected()
    {
        if (destination == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireSphere(destination.position, 0.4f);
    }
}
