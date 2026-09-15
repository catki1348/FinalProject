
using System;
using System.Collections.Generic;
using UnityEngine;

// 7개 부위(EquipmentSlotType)의 장착 상태를 관리하는 스크립트
public class PlayerEquipment : MonoBehaviour
{
    [SerializeField] private ItemData defaultMainWeapon;   // 초기 설정: 검
    [SerializeField] private ItemData defaultSubEquipment; // 초기 설정: 방패

    private readonly Dictionary<EquipmentSlotType, ItemData> equippedItems = new Dictionary<EquipmentSlotType, ItemData>();

    // 장착 상태가 바뀔 때마다 알림(구독 측: 캐릭터 스프라이트 교체, UI 등). item이 null이면 해제됨을 의미
    public event Action<EquipmentSlotType, ItemData> OnEquipmentChanged;

    private void Awake()
    {
        if (defaultMainWeapon != null) Equip(defaultMainWeapon);
        if (defaultSubEquipment != null) Equip(defaultSubEquipment);
    }

    public void Equip(ItemData item)
    {
        if (item == null) return;

        equippedItems[item.SlotType] = item;
        OnEquipmentChanged?.Invoke(item.SlotType, item);
    }

    public void Unequip(EquipmentSlotType slot)
    {
        if (!equippedItems.Remove(slot)) return;

        OnEquipmentChanged?.Invoke(slot, null);
    }

    public ItemData GetEquipped(EquipmentSlotType slot)
    {
        return equippedItems.TryGetValue(slot, out var item) ? item : null;
    }
}
