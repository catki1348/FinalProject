using UnityEngine;

// 드랍 판정에 필요한 "플레이어의 현재 상태"(저체력/무장해제)를 알려주는 Manager.
// 플레이어(PlayerHealth)가 스스로 Register/Unregister 하고, 몬스터 드로퍼는 이 Manager에게만 묻는다.
// -> 플레이어에 싱글톤/정적 접근자를 두지 않고(규칙 35), GameObject.Find도 쓰지 않는다(규칙 36). 싱글톤은 Manager에만 허용된다.
// 작업 AI: Claude Sonnet 5.5, 2026-10-14 (@Edit/ItemDropDesign.md §4)
public class DropConditionManager : MonoBehaviour
{
    public static DropConditionManager Instance { get; private set; }

    private PlayerHealth player; // 등록된 플레이어(없을 수 있음)

    // 플레이어 등록 - Manager 오브젝트가 없으면 이때 한 번 만든다(씬에 미리 두지 않아도 동작)
    public static void Register(PlayerHealth health)
    {
        if (Instance == null)
        {
            var go = new GameObject(nameof(DropConditionManager));
            Instance = go.AddComponent<DropConditionManager>();
        }
        Instance.player = health;
    }

    // 플레이어 해제 - 종료/씬 전환 중 Manager를 새로 만들지 않도록 Instance가 있을 때만 처리한다
    public static void Unregister(PlayerHealth health)
    {
        if (Instance != null && Instance.player == health) Instance.player = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 플레이어가 등록되어 있는가
    public bool HasPlayer => player != null;

    // 현재 상태의 체력 비율이 threshold 이하인가(일반/무장해제 모두 현재 체력 풀 기준). 플레이어가 없으면 false
    public bool IsLowHealth(float threshold)
    {
        return player != null && player.HealthRatio <= threshold;
    }

    // 플레이어가 무장해제 상태인가. 플레이어가 없으면 false
    public bool IsDisarmed => player != null && player.IsDisarmed;
}
