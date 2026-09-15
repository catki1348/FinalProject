using UnityEngine;

// 장비 아이템 하나를 표현하는 데이터(스탯 없이 이름/슬롯 타입만 보유)
[CreateAssetMenu(fileName = "I_NewItem", menuName = "Equipment/Item Data")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string itemName;
    [SerializeField] private EquipmentSlotType slotType;

    public string ItemName => itemName;
    public EquipmentSlotType SlotType => slotType;
}
