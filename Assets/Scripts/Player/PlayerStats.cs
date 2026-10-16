using System;
using UnityEngine;

// 플레이어의 공격력/방어력 스탯을 보유하고, 장비에 따른 최종 수치를 계산하는 스크립트
// (체력은 PlayerHealth가 별도로 관리 - 규칙 35 SRP)
[RequireComponent(typeof(PlayerEquipment))]
public class PlayerStats : MonoBehaviour
{
    [SerializeField] private int baseAttack = 10;  // 초기 공격력
    [SerializeField] private int baseDefense = 5;  // 초기 방어력

    private float defenseAttackBonus; // 완벽 방어 버프의 공격력 증가율(0이면 영향 없음). PlayerDefense가 설정
    private long furyAttackBonus; // Codex, 2026-10-15: additive area bonus after existing multipliers.
    private PlayerEquipment equipment; // 장착 상태 조회용(같은 오브젝트의 컴포넌트, GetComponent로 캐싱 - 규칙 37)

    public int FinalAttack { get; private set; }
    public int FinalDefense { get; private set; }

    // (finalAttack, finalDefense) - 스테이터스 창 갱신용
    public event Action<int, int> OnStatsChanged;

    // Stack counters retain long precision; damage saturates at the existing int API limit.
    public void SetFuryAttackBonus(long bonus)
    {
        furyAttackBonus = Math.Max(0L, bonus);
        RecalculateStats();
    }

    // 방어 버프 공격력 보너스(예: 0.2 = +20%)를 교체하고 장비 포함 최종 공격력을 다시 계산한다.
    public void SetDefenseAttackBonus(float bonus)
    {
        defenseAttackBonus = Mathf.Max(0f, bonus);
        RecalculateStats();
    }

    private void Awake()
    {
        equipment = GetComponent<PlayerEquipment>();
        equipment.OnEquipmentChanged += HandleEquipmentChanged;
        RecalculateStats();
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
        RecalculateStats();
    }

    // 7개 부위의 장착 아이템 보너스를 합산해 최종 수치(기본값 + 장비 합)를 다시 계산
    private void RecalculateStats()
    {
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        int attackBonus = 0;
        int defenseBonus = 0;

        foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
        {
            ItemData item = equipment.GetEquipped(slot);
            if (item == null) continue;

            attackBonus += item.AttackBonus;
            defenseBonus += item.DefenseBonus;
        }

        double attack = Math.Round(((double)baseAttack + attackBonus) * (1d + defenseAttackBonus)) + furyAttackBonus;
        FinalAttack = (int)Math.Min(int.MaxValue, Math.Max(0d, attack));
        FinalDefense = baseDefense + defenseBonus;
        OnStatsChanged?.Invoke(FinalAttack, FinalDefense);
    }
}
