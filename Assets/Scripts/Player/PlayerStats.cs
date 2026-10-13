using System;
using UnityEngine;

// 플레이어의 공격력/방어력 스탯을 보유하고, 장비에 따른 최종 수치를 계산하는 스크립트
// (체력은 PlayerHealth가 별도로 관리 - 규칙 35 SRP)
[RequireComponent(typeof(PlayerEquipment))]
public class PlayerStats : MonoBehaviour
{
    [SerializeField] private int baseAttack = 10;  // 초기 공격력
    [SerializeField] private int baseDefense = 5;  // 초기 방어력

    private PlayerEquipment equipment; // 장착 상태 조회용(같은 오브젝트의 컴포넌트, GetComponent로 캐싱 - 규칙 37)

    public int FinalAttack { get; private set; }
    public int FinalDefense { get; private set; }

    // (finalAttack, finalDefense) - 스테이터스 창 갱신용
    public event Action<int, int> OnStatsChanged;

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
        int attackBonus = 0;
        int defenseBonus = 0;

        foreach (EquipmentSlotType slot in Enum.GetValues(typeof(EquipmentSlotType)))
        {
            ItemData item = equipment.GetEquipped(slot);
            if (item == null) continue;

            attackBonus += item.AttackBonus;
            defenseBonus += item.DefenseBonus;
        }

        FinalAttack = baseAttack + attackBonus;
        FinalDefense = baseDefense + defenseBonus;
        OnStatsChanged?.Invoke(FinalAttack, FinalDefense);
    }
}
