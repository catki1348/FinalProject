using UnityEngine;

// 영역(Zone)의 골인 지점 마커. 기획 컨셉 이미지의 원(○)에 대응한다.
// 실제 플레이 화면에는 아무것도 표시되지 않는다 - 에디터 씬 뷰에서만 기즈모로 보이는 순수 마커(레벨 디자인용) + 트리거 콜라이더.
// 플레이어가 도달하면 카메라의 활성 구간을 다음 영역으로 전환한다(팔로우 카메라 클램프 범위 갱신).
// 5영역은 골인 지점 없이 별도 보스존을 사용하므로 이 컴포넌트를 두지 않는다(MapEdit.md 규칙 5, Boss.cs 참고).
[RequireComponent(typeof(BoxCollider2D))]
public class ZoneEndPoint : MonoBehaviour
{
    [SerializeField] private CameraBoundsZone nextZone;
    [SerializeField] private PlayerCameraFollow cameraFollow;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody == null || !other.attachedRigidbody.TryGetComponent<PlayerMovement>(out _)) return;

        cameraFollow.SetActiveZone(nextZone);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
