using UnityEngine;

// 발판이 따라 이동할 경로(패턴)를 정의하고 시각화하는 컴포넌트.
// 이 오브젝트의 자식 Transform 하나하나가 경로의 지점(Waypoint)이 되며,
// 씬 뷰에서 자식 오브젝트를 드래그해 위치를 옮기면 라인과 실제 이동 패턴이 함께 바뀐다.
// 플레이 중이 아닐 때도 항상 기즈모 라인이 보이므로 패턴을 미리 확인할 수 있다.
public class PlatformPath : MonoBehaviour
{
    [SerializeField] private bool closedLoop;          // true면 마지막 지점에서 첫 지점으로 선을 이어 순환 경로로 표시
    [SerializeField] private Color gizmoColor = Color.yellow;
    [SerializeField] private float pointGizmoRadius = 0.12f;

    public int PointCount => transform.childCount;
    public bool IsClosedLoop => closedLoop;

    // index번째 경로 지점의 월드 좌표(자식 Transform의 현재 위치를 그대로 읽으므로 에디터에서 옮긴 값이 즉시 반영됨)
    public Vector3 GetPoint(int index) => transform.GetChild(index).position;

    private void OnDrawGizmos()
    {
        int count = PointCount;
        if (count < 2) return;

        Gizmos.color = gizmoColor;
        for (int i = 0; i < count - 1; i++)
        {
            Gizmos.DrawLine(GetPoint(i), GetPoint(i + 1));
        }
        if (closedLoop) Gizmos.DrawLine(GetPoint(count - 1), GetPoint(0));

        for (int i = 0; i < count; i++)
        {
            Gizmos.DrawSphere(GetPoint(i), pointGizmoRadius);
        }
    }
}
