using UnityEngine;

// 플레이어가 닿으면 이동속도 버프를 1중첩 부여하고 사라지는 아이템.
// 1중첩당 이동속도 +5%, 최대 5중첩, 유지 5초(습득마다 전체 중첩의 타이머 갱신) - 중첩 계산은 PlayerSpeedBuff가 담당한다.
// 프리팹 구성(규칙 38): SpeedBoostItem + HoveringPickup + Collider2D(Is Trigger) + SpriteRenderer.
// Rigidbody2D는 필요 없다(플레이어에 Rigidbody2D가 있어 트리거 충돌이 발생한다).
// 작업 AI: Claude Sonnet 5.5, 2026-10-14 (@Edit/ItemDropDesign.md §6-1)
[RequireComponent(typeof(Collider2D))]
public class SpeedBoostItem : MonoBehaviour
{
    [SerializeField, Min(0f)] private float stackBonus = 0.05f; // 1중첩당 이동속도 증가량(0.05 = +5%)
    [SerializeField, Min(1)] private int maxStacks = 5;         // 최대 중첩 수
    [SerializeField, Min(0.1f)] private float duration = 5f;    // 버프 지속시간(초, 습득마다 갱신)
    [SerializeField] private AudioClip pickupSfx;               // 획득 효과음(비워두면 재생하지 않음)
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.3f; // 획득음 음량 배율(원본 클립이 가장 커서 크게 줄임. 옵션의 효과음 볼륨과 별개로 곱해짐)

    private bool consumed; // 같은 프레임에 중복 획득되는 것 방지

    // 플레이어(PlayerMovement 보유)가 트리거에 들어오면 1중첩 부여 후 제거. 태그/이름 비교 없이 컴포넌트로 판별(규칙 37)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (!other.TryGetComponent<PlayerMovement>(out var player)) return;

        consumed = true;
        PlayerSpeedBuff.GetOrAdd(player).AddStack(stackBonus, maxStacks, duration);
        SoundManager.Instance.PlaySfx(pickupSfx, pickupVolume);
        Destroy(gameObject);
    }
}
