using UnityEngine;

// 각 스테이지 5영역(보스존)의 보스. 체력이 0이 되면 처치 이벤트만 발행한다.
// MapEdit.md 규칙 8: 보스를 처치해야 종료점(BossZoneEndPoint)이 활성화되는 구조이므로,
// 여기서 스테이지를 곧바로 넘기지 않는다(그 역할은 BossZoneEndPoint + StageManager가 담당).
// 규칙 36: GameObject.Find로 종료점을 직접 찾지 않고, 정적 이벤트로만 알린다(이 씬에는 보스가 1마리뿐이므로
// 인스턴스 이벤트 대신 정적 이벤트로도 구독자를 안전하게 특정할 수 있다).
public class Boss : MonoBehaviour, IDamageable, IContextDamageable
{
    public static event System.Action OnBossDefeated;

    [SerializeField] private int maxHealth = 30;

    private int currentHealth;
    private BossHeadProximityDamage headProximity;

    // 보스 행동 스크립트(BossController)가 구독하는 인스턴스 이벤트 - 피격 사실만 알리고 행동 결정은 구독자가 담당(규칙 35, 36)
    public event System.Action<int> OnDamaged; // 인자: 이번에 실제로 입은 피해량
    public event System.Action OnDied;         // 체력이 0이 되는 순간 한 번 발행

    public int CurrentHealth => currentHealth; // 현재 체력(외부 조회용)
    public int MaxHealth => maxHealth;         // 최대 체력(외부 조회용)
    public bool IsDead => currentHealth <= 0;  // 사망 여부(외부 조회용)

    private void Awake()
    {
        currentHealth = maxHealth;
        TryGetComponent(out headProximity);
    }

    // OpenAI Codex (GPT-6), 2026-10-14: 공격자 문맥이 있는 피해만 머리 거리 배율을 한 번 적용한다.
    public void TakeDamage(DamageContext context)
    {
        if (IsDead) return;
        TakeDamage(headProximity != null ? headProximity.Calculate(context) : context.BaseDamage);
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0 || amount <= 0) return;

        int before = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnDamaged?.Invoke(before - currentHealth);
        if (currentHealth <= 0)
        {
            OnDied?.Invoke();
            OnBossDefeated?.Invoke();
        }
    }
}
