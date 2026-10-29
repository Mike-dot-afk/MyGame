using UnityEngine;
using System.Collections.Generic;

public class WeaponQuickBarSave : SaveableEntity
{
    public WeaponQuickBarUI quickBar;

    public override void SaveData()
    {
        if (quickBar == null) return;

        WeaponQuickBarSlot[] slots = quickBar.GetComponentsInChildren<WeaponQuickBarSlot>(true);
        List<string> itemNames = new List<string>();

        for (int i = 0; i < 5; i++)
        {
            var slot = FindSlotByIndex(slots, i);
            if (slot != null && slot.currentItem != null)
                itemNames.Add(slot.currentItem.name);
            else
                itemNames.Add("Empty");
        }

        string saveStr = string.Join("|", itemNames);
        SaveSystem.SaveEntity(saveableID, "WeaponQuickBar", saveStr);
    }

    public override void LoadData()
    {
        if (quickBar == null) return;

        string saveStr = SaveSystem.LoadEntity<string>(saveableID, "WeaponQuickBar", "");

        WeaponQuickBarSlot[] slots = quickBar.GetComponentsInChildren<WeaponQuickBarSlot>(true);
        foreach (var s in slots) s.ClearSlot();

        if (string.IsNullOrEmpty(saveStr)) return;

        string[] itemNames = saveStr.Split('|');
        int loadCount = Mathf.Min(itemNames.Length, 5);

        for (int i = 0; i < loadCount; i++)
        {
            string itemName = itemNames[i];
            if (itemName == "Empty") continue;

            var slot = FindSlotByIndex(slots, i);
            if (slot == null) continue;

            ItemData item = Resources.Load<ItemData>("Items/" + itemName);
            if (item != null) slot.EquipItem(item);
        }
    }

    private WeaponQuickBarSlot FindSlotByIndex(WeaponQuickBarSlot[] slots, int index)
    {
        foreach (var s in slots)
            if (s.slotIndex == index) return s;
        return null;
    }
}