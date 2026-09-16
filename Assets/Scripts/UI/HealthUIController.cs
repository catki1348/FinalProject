using UnityEngine;
using TMPro;

// PlayerHealth의 체력 변화를 좌측 상단 HUD(세로 게이지 + 숫자)에 반영
public class HealthUIController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;      // Inspector 드래그로 연결 (규칙 40 - Find 금지)
    [SerializeField] private MonoBehaviour healthGaugeView;  // IHealthGaugeView를 구현한 컴포넌트를 드래그로 연결
    [SerializeField] private TMP_Text healthValueText;       // 현재 체력 숫자 표기

    private IHealthGaugeView gaugeView;

    private void Awake()
    {
        gaugeView = healthGaugeView as IHealthGaugeView;
    }

    private void OnEnable()
    {
        if (playerHealth == null) return;
        playerHealth.OnHealthChanged += HandleHealthChanged;
        HandleHealthChanged(playerHealth.CurrentHealth, playerHealth.FinalMaxHealth);
    }

    private void OnDisable()
    {
        if (playerHealth == null) return;
        playerHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int current, int max)
    {
        gaugeView?.SetRatio(max > 0 ? (float)current / max : 0f);
        if (healthValueText != null)
        {
            healthValueText.text = current.ToString();
        }
    }
}
