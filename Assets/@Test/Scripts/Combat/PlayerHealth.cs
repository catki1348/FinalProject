using System;
using UnityEngine;

// 플레이어의 체력을 관리하고 IDamageable을 구현해 적/투사체로부터 피해를 받을 수 있게 하는 스크립트
[RequireComponent(typeof(PlayerEquipment))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int baseHealth = 100;          // 초기 체력(장비 보너스 합산 전)
    [SerializeField] private float invincibleDuration = 1f; // 피격 후 무적 시간(연속 피격 방지)

    public event Action<int, int> OnHealthChanged; // 인자: (현재 체력, 최종 최대 체력)
    public event Action OnDied;

    public int CurrentHealth => currentHealth;
    public int FinalMaxHealth { get; private set; }

    private int currentHealth;
    private float lastHitTime = -999f;
    private PlayerSpriteAnimator spriteAnimator; // 없는 경우(스프라이트 미사용 캐릭터)도 있어 null 허용
    private PlayerEquipment equipment;
    private PlayerStats stats; // 방어력(FinalDefense)으로 피해를 경감하기 위한 참조

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        stats = GetComponent<PlayerStats>();
        spriteAnimator = GetComponent<PlayerSpriteAnimator>();

        equipment.OnEquipmentChanged += HandleEquipmentChanged;

        RecalculateMaxHealth(applyDelta: false);
        currentHealth = FinalMaxHealth;
        OnHealthChanged?.Invoke(currentHealth, FinalMaxHealth);
    }

    private void OnDestroy()
    {
        if (equipment != null)
        {
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        }
    }

    private void HandleEquipmentChanged(EquipmentSlotType slot, ItemData item)
    {
        RecalculateMaxHealth(applyDelta: true);
    }

    // 장착 중인 7부위의 체력 보너스를 합산해 최종 최대체력(기본값 + 장비 합)을 다시 계산
    // applyDelta가 true면 최대체력 변화량만큼 현재체력도 함께 보정(장비 교체 시 즉시 풀피 회복/즉사 방지)
    private void RecalculateMaxHealth(bool applyDelta)
    {
        int previousMax = FinalMaxHealth;
        int healthBonus = 0;

        foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
        {
            var item = equipment.GetEquipped(slot);
            if (item != null) healthBonus += item.HealthBonus;
        }

        FinalMaxHealth = baseHealth + healthBonus;

        if (applyDelta)
        {
            int delta = FinalMaxHealth - previousMax;
            int minBound = currentHealth > 0 ? 1 : 0;
            currentHealth = Mathf.Clamp(currentHealth + delta, minBound, FinalMaxHealth);
            OnHealthChanged?.Invoke(currentHealth, FinalMaxHealth);
        }
    }

    // 무적 시간이 끝난 경우에만 방어력으로 경감된 피해(최소 1)를 적용하고, 체력이 0 이하가 되면 사망 처리
    public void TakeDamage(int amount)
    {
        if (Time.time - lastHitTime < invincibleDuration) return;
        lastHitTime = Time.time;

        int actualDamage = Mathf.Max(1, amount - stats.FinalDefense);
        currentHealth = Mathf.Max(0, currentHealth - actualDamage);
        OnHealthChanged?.Invoke(currentHealth, FinalMaxHealth);
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
