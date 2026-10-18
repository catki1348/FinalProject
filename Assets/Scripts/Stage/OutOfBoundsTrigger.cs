using UnityEngine;

// 필드(맵) 밖으로 나가면 낙사 처리를 요청하는 트리거.
// 2026-10-17 설계 변경(@Edit/SystemDesign_FallRecovery.md): 바로 게임오버가 아니라 체력 35 손실 + 마지막 안전 발판으로 복귀한다.
// 이 낙사선만 예전처럼 즉사시키려면 instantGameOver를 켠다.
[RequireComponent(typeof(BoxCollider2D))]
public class OutOfBoundsTrigger : MonoBehaviour
{
    [SerializeField] private bool instantGameOver = false; // true면 이 낙사선은 복귀 없이 즉시 게임오버

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody == null || !other.attachedRigidbody.TryGetComponent<PlayerMovement>(out _)) return;
        FallRecoveryManager.Instance.HandleFall(instantGameOver);
    }

    // Play 모드가 아닐 때도 Scene 뷰에서 추락 사망 판정 범위를 확인할 수 있도록 콜라이더 영역을 표시
    private void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Vector3 center = transform.TransformPoint(col.offset);
        Vector3 size = Vector3.Scale(new Vector3(col.size.x, col.size.y, 0.1f), transform.lossyScale);
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0.1f));
        Gizmos.color = new Color(1f, 0f, 0f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 0.1f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
