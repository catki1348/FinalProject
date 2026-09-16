using System;
using UnityEngine;

// 플레이어의 체력을 관리하고 IDamageable을 구현해 적/투사체로부터 피해를 받을 수 있게 하는 스크립트
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float invincibleDuration = 1f; // 피격 후 무적 시간(연속 피격 방지)

    public event Action<int, int> OnHealthChanged; // 인자: (현재 체력, 최대 체력)
    public event Action OnDied;

    private int currentHealth;
    private float lastHitTime = -999f;
    private PlayerSpriteAnimator spriteAnimator; // 없는 경우(스프라이트 미사용 캐릭터)도 있어 null 허용

    private void Awake()
    {
        currentHealth = maxHealth;
        spriteAnimator = GetComponent<PlayerSpriteAnimator>();
    }

    // 무적 시간이 끝난 경우에만 피해를 적용하고, 체력이 0 이하가 되면 사망 처리
    public void TakeDamage(int amount)
    {
        if (Time.time - lastHitTime < invincibleDuration) return;
        lastHitTime = Time.time;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        spriteAnimator?.TriggerDisarmed();

        if (currentHealth <= 0) Die();
    }

    // 최소 프로토타입에서는 파괴 대신 현재 씬을 재시작해 다시 도전할 수 있게 함
    private void Die()
    {
        OnDied?.Invoke();
        GameManager.Instance.LoadScene(gameObject.scene.name);
    }
}
