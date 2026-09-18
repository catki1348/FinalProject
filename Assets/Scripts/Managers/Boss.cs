using UnityEngine;

// 각 스테이지 5영역(보스존)의 보스. 체력이 0이 되면 StageManager에 처치를 알려 다음 스테이지로 이동시킨다.
// MapEdit.md 규칙 5, 6: 5영역은 골인 지점 대신 보스존이며, 보스 처치 시 스테이지가 완료 처리된다.
public class Boss : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 30;

    private int currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth <= 0) StageManager.Instance.NotifyBossDefeated();
    }
}
