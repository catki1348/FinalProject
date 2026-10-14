// 컨셉 통합 초안 — 본 게임 미적용. AI: OpenAI Codex (GPT-6), 2026-10-14
using System;
using UnityEngine;

// 플레이어의 체력을 관리하고 IDamageable을 구현해 적/투사체로부터 피해를 받을 수 있게 하는 스크립트
[RequireComponent(typeof(PlayerEquipment))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerDefense))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int baseHealth = 100;          // 초기 체력(장비 보너스 합산 전)
    [SerializeField] private float invincibleDuration = 1f; // 피격 후 무적 시간(연속 피격 방지)
    [SerializeField] private int disarmedMaxHealth = 100;   // 무장해제 상태에서 별도로 갖는 체력(장비/방어력과 무관한 고정값)

    public event Action<int, int> OnHealthChanged; // 인자: (현재 체력, 현재 상태의 최대 체력)
    public event Action OnDied;

    // 무장해제 상태가 아니면 일반 체력을, 무장해제 상태면 별도의 무장해제 체력을 반환(UI 등 외부에서는 이 값만 보면 됨)
    public int CurrentHealth => isDisarmed ? disarmedHealth : currentHealth;
    public int FinalMaxHealth { get; private set; }         // 장비 보너스를 합산한 일반 상태의 최대 체력
    public bool IsDisarmed => isDisarmed;                     // 일반 체력이 0이 되어 무장해제 상태로 전환되었는지 여부

    // 현재 상태(일반/무장해제)에 대응하는 최대 체력 - OnHealthChanged 발행 시 CurrentHealth와 짝을 맞추기 위한 헬퍼
    private int ActiveMaxHealth => isDisarmed ? disarmedMaxHealth : FinalMaxHealth;

    private int currentHealth;
    private int disarmedHealth;   // 무장해제 상태의 현재 체력(무장해제 진입 시 disarmedMaxHealth로 초기화)
    private bool isDisarmed;      // IsDisarmed의 백킹 필드
    private float lastHitTime = -999f;
    private PlayerSpriteAnimator spriteAnimator; // 없는 경우(스프라이트 미사용 캐릭터)도 있어 null 허용
    private PlayerEquipment equipment;
    private PlayerDefense defense;
    private PlayerStats stats; // 방어력(FinalDefense)으로 피해를 경감하기 위한 참조

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        stats = GetComponent<PlayerStats>();
        defense = GetComponent<PlayerDefense>();
        spriteAnimator = GetComponent<PlayerSpriteAnimator>();

        equipment.OnEquipmentChanged += HandleEquipmentChanged;

        RecalculateMaxHealth(applyDelta: false);
        currentHealth = FinalMaxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);
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

        // 이미 무장해제 상태(일반 체력 소진)라면 장비 변경으로 일반 체력 풀을 되살리지 않는다 -
        // 그 사이 실제로 표시/적용되는 체력은 CurrentHealth가 가리키는 disarmedHealth뿐이다.
        if (applyDelta && !isDisarmed)
        {
            int delta = FinalMaxHealth - previousMax;
            int minBound = currentHealth > 0 ? 1 : 0;
            currentHealth = Mathf.Clamp(currentHealth + delta, minBound, FinalMaxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);
        }
    }

    // 무적 시간이 끝난 경우에만 방어력으로 경감된 피해(최소 1)를 적용한다.
    // 일반 상태에서 체력이 0이 되면 죽는 대신 무장해제 상태로 전환되고, 무장해제 상태에서
    // 별도의 체력(disarmedMaxHealth)이 다시 0이 되어야 비로소 사망(게임오버) 처리한다.
    public void TakeDamage(int amount)
    {
        if (amount <= 0 || CurrentHealth <= 0) return;
        if (defense.TryBlock(amount)) return;
        if (Time.time - lastHitTime < invincibleDuration) return;
        lastHitTime = Time.time;

        int actualDamage = Mathf.Max(1, amount - stats.FinalDefense);
        actualDamage = defense.AbsorbDamage(actualDamage);
        if (actualDamage <= 0) return;

        if (isDisarmed)
        {
            disarmedHealth = Mathf.Max(0, disarmedHealth - actualDamage);
            OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);

            if (disarmedHealth <= 0) Die();
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - actualDamage);

        if (currentHealth <= 0)
        {
            EnterDisarmedState();
        }
        else
        {
            OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);
            spriteAnimator?.TriggerDamage();
        }
    }

    // 체력을 amount만큼 회복한다(현재 상태의 최대 체력을 넘지 않음). 일반 상태면 일반 체력을,
    // 무장해제 상태면 무장해제 체력을 회복한다. 회복 아이템(HealItem)이 호출한다.
    public void Heal(int amount)
    {
        if (amount <= 0) return;

        if (isDisarmed) disarmedHealth = Mathf.Min(disarmedMaxHealth, disarmedHealth + amount);
        else currentHealth = Mathf.Min(FinalMaxHealth, currentHealth + amount);

        OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);
    }

    // 일반 체력이 0이 되었을 때 호출 - 무장해제 상태로 전환하고 별도의 체력을 가득 채운 뒤,
    // 표시 스크립트는 IsDisarmed를 참조하므로 회복된 체력과 무장해제 외형이 같은 상태를 따른다.
    private void EnterDisarmedState()
    {
        isDisarmed = true;
        defense.ClearBuff();
        disarmedHealth = disarmedMaxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);
    }

    // 무장해제 상태의 체력까지 모두 소진되었을 때 호출 - 게임오버 연출을 띄운다(규칙 35: GameOverManager를 통해 처리)
    private void Die()
    {
        OnDied?.Invoke();
        GameOverManager.Instance.TriggerGameOver();
    }
}
