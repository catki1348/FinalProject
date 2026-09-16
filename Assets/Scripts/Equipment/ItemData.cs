using UnityEngine;

// 장비 아이템 하나를 표현하는 데이터(이름/슬롯 타입 + 스탯 보너스 보유)
[CreateAssetMenu(fileName = "I_NewItem", menuName = "Equipment/Item Data")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string itemName;
    [SerializeField] private EquipmentSlotType slotType;

    [SerializeField] private int healthBonus;  // 장착 시 최종 체력에 더해지는 값
    [SerializeField] private int attackBonus;  // 장착 시 최종 공격력에 더해지는 값
    [SerializeField] private int defenseBonus; // 장착 시 최종 방어력에 더해지는 값

    public string ItemName => itemName;
    public EquipmentSlotType SlotType => slotType;

    public int HealthBonus => healthBonus;
    public int AttackBonus => attackBonus;
    public int DefenseBonus => defenseBonus;
}
