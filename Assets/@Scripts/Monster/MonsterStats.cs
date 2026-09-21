using System;
using UnityEngine;

// 몬스터의 체력/공격력/방어력 기본 스탯을 보유하고 피해를 처리하는 스크립트
// 패턴(행동) 스크립트와는 역할을 분리한다(Monster.md 규칙 1, 3 - SRP)
public class MonsterStats : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 20; // 최대 체력
    [SerializeField] private int attack = 5;     // 공격력
    [SerializeField] private int defense = 2;    // 방어력

    private int currentHealth;

    public int Attack => attack;
    public int Defense => defense;
    public bool IsDead { get; private set; }

    // (현재 체력, 최대 체력) - 몬스터 체력 게이지 등 갱신용
    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    // 방어력으로 경감된 피해(최소 1)를 적용하고, 체력이 0 이하가 되면 사망 처리(PlayerHealth.TakeDamage와 동일 정책)
    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        int actualDamage = Mathf.Max(1, amount - defense);
        currentHealth = Mathf.Max(0, currentHealth - actualDamage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0) Die();
    }

    // 사망 상태로 전환하고 구독자(MonsterController 등)에게 알림 - 이후 TakeDamage는 무시됨
    private void Die()
    {
        IsDead = true;
        OnDeath?.Invoke();
    }
}
