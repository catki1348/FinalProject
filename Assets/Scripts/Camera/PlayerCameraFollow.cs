using UnityEngine;

// 플레이어를 연속으로 추적하되, 현재 활성 구간(CameraBoundsZone) 범위를 벗어나지 않도록 클램프하는 카메라 컨트롤러.
// 구간 전환은 SectionEdgeTrigger가 SetActiveZone 호출로 알려준다(규칙 40: 이벤트/직접 호출 기반, Find 금지).
[RequireComponent(typeof(Camera))]
public class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset;
    [SerializeField] private float followLerpSpeed = 8f;
    [SerializeField] private float zoomLerpSpeed = 4f;
    [SerializeField] private CameraBoundsZone activeZone;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (activeZone == null || target == null) return;

        cam.orthographicSize = activeZone.OrthographicSize;

        Vector3 startPosition = ClampToZone(target.position + (Vector3)offset, activeZone);
        startPosition.z = transform.position.z;
        transform.position = startPosition;
    }

    // 구간 경계를 넘을 때 SectionEdgeTrigger가 호출
    public void SetActiveZone(CameraBoundsZone zone)
    {
        if (zone != null) activeZone = zone;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + (Vector3)offset;
        desired.z = transform.position.z;

        float followT = 1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime);
        Vector3 next = Vector3.Lerp(transform.position, desired, followT);
        transform.position = ClampToZone(next, activeZone);

        if (activeZone != null)
        {
            float zoomT = 1f - Mathf.Exp(-zoomLerpSpeed * Time.deltaTime);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, activeZone.OrthographicSize, zoomT);
        }
    }

    // 배경 폭/높이가 화면보다 좁으면(스크롤 여유 없음) 해당 축은 자동으로 중앙 고정
    private Vector3 ClampToZone(Vector3 pos, CameraBoundsZone zone)
    {
        if (zone == null) return pos;

        Bounds bounds = zone.WorldBounds;
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        pos.x = ClampAxis(pos.x, bounds.min.x, bounds.max.x, halfWidth);
        pos.y = ClampAxis(pos.y, bounds.min.y, bounds.max.y, halfHeight);
        return pos;
    }

    private float ClampAxis(float value, float min, float max, float half)
    {
        if (max - min <= half * 2f) return (min + max) * 0.5f;
        return Mathf.Clamp(value, min + half, max - half);
    }
}
