using UnityEngine;

// 보스가 처치되면 보스존 한가운데(dropPoint)를 향해 보상 아이템(BossRewardItem 프리팹)을 떨어뜨리는 스크립트.
// 몬스터 드로퍼(MonsterItemDropper)와 달리 보스 사망 위치가 아니라 고정된 보스존 중앙으로 떨어진다.
// 아이템은 dropPoint에 바로 생기지 않고, 화면 위쪽 밖에서 생성되어 dropPoint까지 천천히 내려온다(DescendingDrop).
// Boss.OnBossDefeated(정적 이벤트)만 구독한다 - 기존 BossZoneEndPoint와 같은 연결 방식(규칙 36).
// 사용법: 보스존(5영역) 오브젝트 아래에 이 스크립트를 붙이고, dropPoint는 보스존 중앙 바닥 위
// (플레이어가 걸어서 닿을 수 있는 높이)의 빈 Transform으로, rewardPrefab은 P_Item_BossReward로 지정한다.
// 작업 AI: Claude Sonnet 5.5, 2026-10-18 (@Edit/BossRewardItemDesign.md)
public class BossRewardDropper : MonoBehaviour
{
    [SerializeField] private GameObject rewardPrefab; // 생성할 보상 아이템 프리팹(BossRewardItem 포함)
    [SerializeField] private Transform dropPoint;     // 보스존 한가운데 생성 위치(Inspector 지정 - Find 미사용)

    [SerializeField, Min(0f)] private float spawnMarginAboveScreen = 1.5f; // 화면 위쪽 가장자리보다 이만큼 더 위에서 시작
    [SerializeField, Min(0f)] private float minSpawnHeight = 4f;           // 카메라를 못 찾을 때/화면이 낮을 때 드롭 지점 위 최소 높이

    private bool dropped; // 보스 처치 이벤트가 중복 발행되어도 1개만 생성

    private void OnEnable()
    {
        Boss.OnBossDefeated += HandleBossDefeated;
    }

    private void OnDisable()
    {
        Boss.OnBossDefeated -= HandleBossDefeated;
    }

    // 보스 처치 시 1회: 화면 위쪽 밖(dropPoint와 같은 x)에 생성하고 dropPoint까지 내려오게 한다.
    // 프리팹에 DescendingDrop이 없으면 예전처럼 dropPoint에 바로 생성한다.
    private void HandleBossDefeated()
    {
        if (dropped || rewardPrefab == null || dropPoint == null) return;

        dropped = true;
        Vector3 landing = dropPoint.position;
        GameObject reward = Instantiate(rewardPrefab, new Vector3(landing.x, GetSpawnHeight(landing.y), landing.z), Quaternion.identity);

        if (reward.TryGetComponent<DescendingDrop>(out var descending)) descending.Begin(landing);
        else reward.transform.position = landing;
    }

    // 시작 높이: 카메라가 드롭 지점 근처를 비추고 있으면 현재 화면 위쪽 가장자리 + 여유, 아니면(카메라가 없거나 멀리 있음)
    // dropPoint 위 minSpawnHeight. 어느 경우든 dropPoint 위 minSpawnHeight보다 낮게 시작하지 않는다.
    private float GetSpawnHeight(float landingY)
    {
        float height = landingY + minSpawnHeight;
        Camera view = Camera.main;
        if (view != null && view.orthographic && Mathf.Abs(view.transform.position.y - landingY) <= view.orthographicSize * 2f)
            height = Mathf.Max(height, view.transform.position.y + view.orthographicSize + spawnMarginAboveScreen);
        return height;
    }

    // 씬 뷰에서 생성 위치를 노란 와이어 구로 표시
    private void OnDrawGizmosSelected()
    {
        if (dropPoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(dropPoint.position, 0.4f);
    }
}
