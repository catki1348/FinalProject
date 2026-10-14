using UnityEngine;

// 플레이어가 닿으면 체력을 회복시키고 사라지는 아이템.
// 프리팹 구성(규칙 38): HealItem + HoveringPickup + Collider2D(Is Trigger) + SpriteRenderer.
[RequireComponent(typeof(Collider2D))]
public class HealItem : MonoBehaviour
{
    [SerializeField] private int healAmount = 20;  // 회복량(최대 체력을 넘지 않음)
    [SerializeField] private AudioClip pickupSfx;  // 획득 효과음(비워두면 재생하지 않음)

    private bool consumed; // 중복 획득 방지

    // PlayerHealth를 가진 대상이 들어오면 회복 후 제거(태그/이름 비교 없이 컴포넌트로 판별 - 규칙 37)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (!other.TryGetComponent<PlayerHealth>(out var health)) return;

        consumed = true;
        health.Heal(healAmount);
        SoundManager.Instance.PlaySfx(pickupSfx);
        Destroy(gameObject);
    }
}
