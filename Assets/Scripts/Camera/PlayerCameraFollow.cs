using System.Collections.Generic;
using UnityEngine;

// 플레이어를 연속으로 추적하되, 현재 활성 구간(CameraBoundsZone) 범위를 벗어나지 않도록 클램프하는 카메라 컨트롤러.
// 구간 전환은 SectionEdgeTrigger가 SetActiveZone 호출로 알려준다(규칙 36: 이벤트/직접 호출 기반, Find 금지).
[RequireComponent(typeof(Camera))]
public class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset;
    [SerializeField] private float followLerpSpeed = 8f;
    [SerializeField] private float zoomLerpSpeed = 4f;
    [SerializeField] private CameraBoundsZone activeZone;

    private Camera cam;

    // 플레이어가 들어가 있는 확대 구역(CameraZoomVolume). 구간 기본 크기보다 큰 값이 있으면 그 중 최댓값으로 확대한다.
    private readonly List<CameraZoomVolume> zoomVolumes = new List<CameraZoomVolume>();

    public void AddZoomVolume(CameraZoomVolume volume)
    {
        if (volume != null && !zoomVolumes.Contains(volume)) zoomVolumes.Add(volume);
    }

    public void RemoveZoomVolume(CameraZoomVolume volume)
    {
        zoomVolumes.Remove(volume);
    }

    // 현재 목표 크기: 구간 기본 크기와 플레이어가 들어간 확대 구역 중 가장 큰 값
    private float TargetOrthographicSize()
    {
        float size = activeZone.OrthographicSize;
        for (int i = zoomVolumes.Count - 1; i >= 0; i--)
        {
            if (zoomVolumes[i] == null) { zoomVolumes.RemoveAt(i); continue; }
            size = Mathf.Max(size, zoomVolumes[i].OrthographicSize);
        }
        return size;
    }

    // Background managers read the camera's actual selected zone, including teleports and backtracking.
    public CameraBoundsZone ActiveZone => activeZone;

    // 마지막으로 카메라 추적을 반영한 시점의 물리 시간(Time.fixedTime).
    // 플레이어/이동 발판은 물리 스텝(기본 0.02초)마다 한 번씩만 위치가 바뀌는데(Rigidbody2D 보간 None),
    // 카메라가 매 렌더 프레임(예: 230fps) 부드럽게 따라가면 물리 스텝 사이 프레임마다 카메라만 움직여
    // 화면상 캐릭터가 조금씩 밀리다가 다음 스텝에 툭 튀는 "톱니" 모양 진동이 생긴다(3-2 승강 발판에서
    // 실측: 239프레임 중 187프레임은 캐릭터가 그대로인데 카메라만 이동). 그래서 물리 스텝이 진행된
    // 프레임에서만, 그 사이 흐른 물리 시간만큼 추적을 반영해 카메라와 캐릭터가 항상 같은 순간에 움직이게 한다.
    private float lastFollowFixedTime;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        lastFollowFixedTime = Time.fixedTime;
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

    // 텔레포트처럼 플레이어가 순간이동했을 때 보간 없이 카메라를 즉시 새 위치/구간으로 옮긴다.
    // 호출 시점이 물리 콜백 안이라 target.position이 아직 갱신 전일 수 있어 목적지 좌표를 직접 받는다.
    public void SnapTo(Vector2 targetPosition, CameraBoundsZone zone)
    {
        SetActiveZone(zone);
        if (activeZone != null) cam.orthographicSize = activeZone.OrthographicSize;

        Vector3 pos = ClampToZone((Vector3)targetPosition + (Vector3)offset, activeZone);
        pos.z = transform.position.z;
        transform.position = pos;
        lastFollowFixedTime = Time.fixedTime;
    }

    // 물리 스텝이 진행된 프레임에서만 추적/줌을 갱신한다(lastFollowFixedTime 주석 참고).
    // 한 프레임에 물리 스텝이 여러 번 돈 경우에도 흐른 물리 시간(elapsed) 전체를 한 번에 반영하므로
    // 프레임레이트와 무관하게 기존과 같은 추적 속도(followLerpSpeed/zoomLerpSpeed)가 유지된다.
    // 게임오버 등으로 timeScale이 0이면 물리 시간이 멈추므로 카메라도 그대로 멈춘다.
    private void LateUpdate()
    {
        if (target == null) return;

        float elapsed = Time.fixedTime - lastFollowFixedTime;
        if (elapsed <= 0f) return;
        lastFollowFixedTime = Time.fixedTime;

        Vector3 desired = target.position + (Vector3)offset;
        desired.z = transform.position.z;

        if (activeZone != null)
        {
            float zoomT = 1f - Mathf.Exp(-zoomLerpSpeed * elapsed);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, TargetOrthographicSize(), zoomT);
        }

        float followT = 1f - Mathf.Exp(-followLerpSpeed * elapsed);
        Vector3 next = Vector3.Lerp(transform.position, desired, followT);
        transform.position = ClampToZone(next, activeZone);
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
