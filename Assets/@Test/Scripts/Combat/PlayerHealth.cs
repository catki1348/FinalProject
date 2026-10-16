using System;
using UnityEngine;

// 플레이어의 체력을 관리하고 IDamageable을 구현해 적/투사체로부터 피해를 받을 수 있게 하는 스크립트
[RequireComponent(typeof(PlayerEquipment))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerHealth : MonoBehaviour, IDamageable, IContextDamageable
{
    [SerializeField] private int baseHealth = 100;          // 초기 체력(장비 보너스 합산 전)
    [SerializeField] private float invincibleDuration = 1f; // 피격 후 무적 시간(연속 피격 방지)
    [SerializeField] private bool enableDisarmedState = false; // OFF: 체력 소진 시 무장해제 없이 사망 처리
    [SerializeField] private int disarmedMaxHealth = 100;   // 무장해제 상태에서 별도로 갖는 체력(장비/방어력과 무관한 고정값)

    public event Action<int, int> OnHealthChanged; // 인자: (현재 체력, 현재 상태의 최대 체력)
    public event Action OnDied;

    // 무장해제 상태가 아니면 일반 체력을, 무장해제 상태면 별도의 무장해제 체력을 반환(UI 등 외부에서는 이 값만 보면 됨)
    public int CurrentHealth => isDisarmed ? disarmedHealth : currentHealth;
    public int FinalMaxHealth { get; private set; }         // 장비 보너스를 합산한 일반 상태의 최대 체력
    public bool IsDisarmed => isDisarmed;                     // 일반 체력이 0이 되어 무장해제 상태로 전환되었는지 여부
    // 현재 상태(일반/무장해제)의 체력 비율(0~1) - 드랍 조건(저체력) 판정용
    public float HealthRatio => ActiveMaxHealth > 0 ? (float)CurrentHealth / ActiveMaxHealth : 0f;

    // 현재 상태(일반/무장해제)에 대응하는 최대 체력 - OnHealthChanged 발행 시 CurrentHealth와 짝을 맞추기 위한 헬퍼
    private int ActiveMaxHealth => isDisarmed ? disarmedMaxHealth : FinalMaxHealth;

    private int currentHealth;
    private int disarmedHealth;   // 무장해제 상태의 현재 체력(무장해제 진입 시 disarmedMaxHealth로 초기화)
    private bool isDisarmed;      // IsDisarmed의 백킹 필드
    private float lastHitTime = -999f;
    private PlayerSpriteAnimator spriteAnimator; // 없는 경우(스프라이트 미사용 캐릭터)도 있어 null 허용
    private PlayerEquipment equipment;
    private PlayerStats stats; // 방어력(FinalDefense)으로 피해를 경감하기 위한 참조
    private PlayerDefense defense; // 우클릭 방어(차단/완벽 방어 임시 체력). 없는 캐릭터도 있어 null 허용

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        stats = GetComponent<PlayerStats>();
        spriteAnimator = GetComponent<PlayerSpriteAnimator>();
        defense = GetComponent<PlayerDefense>();

        equipment.OnEquipmentChanged += HandleEquipmentChanged;

        RecalculateMaxHealth(applyDelta: false);
        currentHealth = FinalMaxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);

        DropConditionManager.Register(this); // 몬스터 드랍 조건(저체력/무장해제) 판정용으로 Manager에 등록
    }

    private void OnDestroy()
    {
        DropConditionManager.Unregister(this);

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
    // 일반 체력이 0이 되면 사망한다. enableDisarmedState를 켠 경우에만 무장해제 체력으로 전환한다.
    // 방어 중이면(우클릭 유지, 무장해제 아님) 피해 전부를 차단하고, 입력 직후 3프레임이면 완벽 방어 버프를 받는다.
    // 차단된 공격은 무적 시간을 소모하지 않는다. 차단하지 못한 피해는 방어력 경감 후 임시 체력부터 차감한다.
    // 공격자 정보가 있는 피해 경로 - 방어 성공 시 공격자 넉백 이벤트까지 전달한다.
    public void TakeDamage(DamageContext context)
    {
        if (defense != null && context.BaseDamage > 0 &&
            defense.TryBlock(context.BaseDamage, context.AttackerBody, context.AttackId)) return;
        TakeDamage(context.BaseDamage, false);
    }

    public void TakeDamage(int amount) { TakeDamage(amount, true); }

    private void TakeDamage(int amount, bool tryBlock)
    {
        if (tryBlock && defense != null && amount > 0 && defense.TryBlock(amount)) return;
        if (Time.time - lastHitTime < invincibleDuration) return;
        lastHitTime = Time.time;

        int actualDamage = Mathf.Max(1, amount - stats.FinalDefense);
        if (defense != null)
        {
            actualDamage = defense.AbsorbDamage(actualDamage);
            if (actualDamage <= 0) return; // 임시 체력이 전부 흡수하면 실제 체력/피격 연출 없음
        }

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

    // Codex (GPT-6), 2026-10-15: environmental attrition has its own clock. It neither
    // consumes nor grants combat invulnerability, block buffers, or damage animations.
    public void TakeEnvironmentalDamage(int amount)
    {
        if (amount <= 0 || (isDisarmed && disarmedHealth <= 0)) return;
        if (isDisarmed)
        {
            disarmedHealth = Mathf.Max(0, disarmedHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);
            if (disarmedHealth == 0) Die();
        }
        else
        {
            currentHealth = Mathf.Max(0, currentHealth - amount);
            if (currentHealth == 0) EnterDisarmedState();
            else OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);
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

    // 무장해제 상태를 해제하고 일반 상태의 체력을 가득 채운다(RearmItem이 호출). 무장 상태에서는 아무 일도 하지 않고 false를 반환한다.
    // 갑옷 장비/슬롯은 건드리지 않는다 - 상태(isDisarmed)와 체력만 복원하며, 외형은 IsDisarmed를 보는 기존 표시 스크립트가 따른다.
    public bool Rearm()
    {
        if (!isDisarmed) return false;

        isDisarmed = false;
        currentHealth = FinalMaxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);
        return true;
    }

    // 일반 체력이 0이 되었을 때 호출 - 무장해제 상태로 전환하고 별도의 체력을 가득 채운 뒤,
    // 표시 스크립트는 IsDisarmed를 참조하므로 회복된 체력과 무장해제 외형이 같은 상태를 따른다.
    private void EnterDisarmedState()
    {
        if (!enableDisarmedState)
        {
            OnHealthChanged?.Invoke(CurrentHealth, ActiveMaxHealth);
            Die();
            return;
        }

        isDisarmed = true;
        defense?.ClearBuff(); // 무장해제되면 방어 버프(공격력 증가/임시 체력)를 제거한다
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
