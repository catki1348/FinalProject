using UnityEngine;
using TMPro;

// Enter 키로 스테이터스 창을 토글하고, 체력(최종 최대체력)/공격력/방어력을 순서대로 표기
public class StatusWindowController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth; // 체력(최종 최대체력) 조회용 - Inspector 드래그 (규칙 36 - Find 금지)
    [SerializeField] private PlayerStats playerStats;   // 공격력/방어력 조회용 - Inspector 드래그
    [SerializeField] private GameObject statusWindow;   // 토글 대상 UI 루트(기본 비활성 상태로 둘 것)
    [SerializeField] private TMP_Text healthValueText;
    [SerializeField] private TMP_Text attackValueText;
    [SerializeField] private TMP_Text defenseValueText;

    private void OnEnable()
    {
        // 구독만으로는 Awake 단계에서 이미 발행된 초기값을 놓칠 수 있어(다른 오브젝트의 Awake가 먼저 실행됨),
        // 구독 직후 현재값을 한 번 직접 읽어와 반영한다(HealthUIController와 동일한 패턴).
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.FinalMaxHealth);
        }
        if (playerStats != null)
        {
            playerStats.OnStatsChanged += HandleStatsChanged;
            HandleStatsChanged(playerStats.FinalAttack, playerStats.FinalDefense);
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
        if (playerStats != null) playerStats.OnStatsChanged -= HandleStatsChanged;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ToggleWindow();
        }
    }

    private void ToggleWindow()
    {
        bool willOpen = !statusWindow.activeSelf;
        statusWindow.SetActive(willOpen);
        if (!willOpen) return;

        if (playerHealth != null) HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.FinalMaxHealth);
        if (playerStats != null) HandleStatsChanged(playerStats.FinalAttack, playerStats.FinalDefense);
    }

    // 체력(스탯 창에서는 최종 최대체력 표기) - 순서: 체력 → 공격력 → 방어력
    private void HandleHealthChanged(int current, int max)
    {
        if (healthValueText != null) healthValueText.text = max.ToString();
    }

    private void HandleStatsChanged(int attack, int defense)
    {
        if (attackValueText != null) attackValueText.text = attack.ToString();
        if (defenseValueText != null) defenseValueText.text = defense.ToString();
    }
}
