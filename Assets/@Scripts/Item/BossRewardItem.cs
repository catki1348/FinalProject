using UnityEngine;

// 보스 처치 보상 아이템 - 플레이어가 닿으면 ① 체력을 전부 회복하고 ② 영역 종료점 역할로 다음 스테이지 진행을 시작한다.
// 기존 BossZoneEndPoint(보이지 않는 종료점 마커)를 대체한다(MapEdit.md 규칙 7~10의 흐름은 그대로:
// 클리어 화면 -> 다음 스테이지, 5-5는 엔딩). 두 오브젝트가 한 씬에 함께 있으면 진행이 중복되므로 보스존에는 하나만 둔다.
// 일반 드랍 아이템과 달리 수명(ItemLifetime)을 붙이지 않는다 - 사라지면 스테이지를 넘길 수 없기 때문.
// 프리팹 구성(규칙 38): BossRewardItem + HoveringPickup + Collider2D(Is Trigger) + SpriteRenderer.
// 작업 AI: Claude Sonnet 5.5, 2026-10-18 (@Edit/BossRewardItemDesign.md)
[RequireComponent(typeof(Collider2D))]
public class BossRewardItem : MonoBehaviour
{
    [SerializeField] private AudioClip pickupSfx; // 획득 효과음(비워두면 재생하지 않음)
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.5f; // 획득음 음량 배율

    private bool consumed; // 클리어 연출 중 중복 진행 방지

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    // 플레이어(PlayerHealth 보유)가 닿으면 체력 전체 회복 -> 효과음 -> 스테이지 진행 -> 아이템 제거.
    // 판별은 컴포넌트로 한다(태그/이름 비교 금지 - 규칙 37).
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed) return;
        if (!other.TryGetComponent<PlayerHealth>(out var health)) return;

        consumed = true;
        health.HealToFull();
        SoundManager.Instance.PlaySfx(pickupSfx, pickupVolume);
        StageManager.Instance.OnZone5EndPointReached(); // 마지막 스테이지면 엔딩, 아니면 클리어 화면 후 다음 스테이지
        Destroy(gameObject);
    }
}
