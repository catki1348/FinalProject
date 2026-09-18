using UnityEngine;

// 영역(Zone)의 시작 지점 마커. 기획 컨셉 이미지의 별표(★)에 대응한다.
// 실제 플레이 화면에는 아무것도 표시되지 않는다 - 에디터 씬 뷰에서만 기즈모로 보이는 순수 마커(레벨 디자인용).
// 1영역(스테이지 시작)의 경우 StageManager가 이 위치로 플레이어를 스폰시키는 기준점으로 사용한다.
public class ZoneStartPoint : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.45f, transform.position + Vector3.down * 0.45f);
        Gizmos.DrawLine(transform.position + Vector3.left * 0.45f, transform.position + Vector3.right * 0.45f);
    }
}
