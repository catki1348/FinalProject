using UnityEngine;

// 구간 배경의 실제 월드 크기(카메라가 이동 가능한 최대 범위)와, 이 구간에서 사용할 카메라 확대 크기를 정의
public class CameraBoundsZone : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(20f, 8f);
    [SerializeField] private float orthographicSize = 4f;

    public Bounds WorldBounds => new Bounds(transform.position, size);
    public float OrthographicSize => orthographicSize;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, size);
    }
}
