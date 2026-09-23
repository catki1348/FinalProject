using UnityEngine;

// 각 스테이지 5영역(보스존)의 보스. 체력이 0이 되면 처치 이벤트만 발행한다.
// MapEdit.md 규칙 8: 보스를 처치해야 종료점(BossZoneEndPoint)이 활성화되는 구조이므로,
// 여기서 스테이지를 곧바로 넘기지 않는다(그 역할은 BossZoneEndPoint + StageManager가 담당).
// 규칙 40: GameObject.Find로 종료점을 직접 찾지 않고, 정적 이벤트로만 알린다(이 씬에는 보스가 1마리뿐이므로
// 인스턴스 이벤트 대신 정적 이벤트로도 구독자를 안전하게 특정할 수 있다).
public class Boss : MonoBehaviour, IDamageable
{
    public static event System.Action OnBossDefeated;

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
        if (currentHealth <= 0) OnBossDefeated?.Invoke();
    }
}
