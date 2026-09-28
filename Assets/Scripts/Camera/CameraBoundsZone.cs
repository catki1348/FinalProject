using UnityEngine;

// 구간 배경의 실제 월드 크기(카메라가 이동 가능한 최대 범위)와, 이 구간에서 사용할 카메라 확대 크기를 정의.
// leftBoundary/rightBoundary에 해당 구간의 SectionEdgeTrigger 오브젝트(Boundary_x_y)를 연결하면,
// 그 경계가 나중에 옮겨지거나 구간 내용이 바뀌어도 가로 범위가 항상 그 경계와 정확히 맞물리도록 자동으로 따라간다.
// (맵/구간 크기를 다시 손볼 때 수치를 다시 맞추지 않아도 경계-카메라 어긋남으로 인한 "카메라 멈춤" 버그가 재발하지 않게 하기 위함)
public class CameraBoundsZone : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(20f, 8f);
    [SerializeField] private float orthographicSize = 4f;
    [SerializeField] private Transform leftBoundary;
    [SerializeField] private Transform rightBoundary;

    public Bounds WorldBounds
    {
        get
        {
            Bounds manual = new Bounds(transform.position, size);
            float minX = manual.min.x;
            float maxX = manual.max.x;
            if (leftBoundary != null) minX = leftBoundary.position.x;
            if (rightBoundary != null) maxX = rightBoundary.position.x;
            if (minX > maxX) { float t = minX; minX = maxX; maxX = t; }

            float centerX = (minX + maxX) * 0.5f;
            float width = maxX - minX;
            return new Bounds(new Vector3(centerX, transform.position.y, transform.position.z), new Vector3(width, size.y, 0f));
        }
    }

    public float OrthographicSize => orthographicSize;

    private void OnDrawGizmos()
    {
        bool bothAuto = leftBoundary != null && rightBoundary != null;
        bool oneAuto = leftBoundary != null || rightBoundary != null;
        Gizmos.color = bothAuto ? Color.cyan : (oneAuto ? Color.green : Color.yellow);
        Bounds b = WorldBounds;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
