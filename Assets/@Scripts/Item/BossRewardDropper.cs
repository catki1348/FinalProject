using UnityEngine;

// 보스가 처치되면 보스존 한가운데(dropPoint)에 보상 아이템(BossRewardItem 프리팹)을 생성하는 스크립트.
// 몬스터 드로퍼(MonsterItemDropper)와 달리 보스 사망 위치가 아니라 고정된 보스존 중앙에 생성한다.
// Boss.OnBossDefeated(정적 이벤트)만 구독한다 - 기존 BossZoneEndPoint와 같은 연결 방식(규칙 36).
// 사용법: 보스존(5영역) 오브젝트 아래에 이 스크립트를 붙이고, dropPoint는 보스존 중앙 바닥 위
// (플레이어가 걸어서 닿을 수 있는 높이)의 빈 Transform으로, rewardPrefab은 P_Item_BossReward로 지정한다.
// 작업 AI: Claude Sonnet 5.5, 2026-10-18 (@Edit/BossRewardItemDesign.md)
public class BossRewardDropper : MonoBehaviour
{
    [SerializeField] private GameObject rewardPrefab; // 생성할 보상 아이템 프리팹(BossRewardItem 포함)
    [SerializeField] private Transform dropPoint;     // 보스존 한가운데 생성 위치(Inspector 지정 - Find 미사용)

    private bool dropped; // 보스 처치 이벤트가 중복 발행되어도 1개만 생성

    private void OnEnable()
    {
        Boss.OnBossDefeated += HandleBossDefeated;
    }

    private void OnDisable()
    {
        Boss.OnBossDefeated -= HandleBossDefeated;
    }

    // 보스 처치 시 1회: dropPoint 위치에 보상 아이템 생성
    private void HandleBossDefeated()
    {
        if (dropped || rewardPrefab == null || dropPoint == null) return;

        dropped = true;
        Instantiate(rewardPrefab, dropPoint.position, Quaternion.identity);
    }

    // 씬 뷰에서 생성 위치를 노란 와이어 구로 표시
    private void OnDrawGizmosSelected()
    {
        if (dropPoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(dropPoint.position, 0.4f);
    }
}
