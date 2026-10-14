using UnityEngine;

// 플레이어가 무장해제 상태일 때 닿으면 무장 상태로 복귀시키고 체력을 전부 회복한 뒤 사라지는 아이템.
// 무장 상태의 플레이어가 닿으면 소비되지 않고 필드에 남는다(이미 다른 경로로 무장한 경우의 낭비 방지).
// 프리팹 구성(규칙 38): RearmItem + HoveringPickup + Collider2D(Is Trigger) + SpriteRenderer.
// 작업 AI: Claude Sonnet 5.5, 2026-10-14 (@Edit/ItemDropDesign.md §6-4)
[RequireComponent(typeof(Collider2D))]
public class RearmItem : MonoBehaviour
{
    [SerializeField] private AudioClip pickupSfx; // 획득 효과음(비워두면 재생하지 않음)

    private bool consumed; // 중복 획득 방지

    // PlayerHealth를 가진 대상이 들어오면 Rearm 시도, 성공했을 때만 제거(태그/이름 비교 없이 컴포넌트로 판별 - 규칙 37)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (!other.TryGetComponent<PlayerHealth>(out var health)) return;
        if (!health.Rearm()) return;

        consumed = true;
        SoundManager.Instance.PlaySfx(pickupSfx);
        Destroy(gameObject);
    }
}
