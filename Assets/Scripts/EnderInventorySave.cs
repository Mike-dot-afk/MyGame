using UnityEngine;
using System.Collections.Generic;

public class EnderInventorySave : SaveableEntity
{
    private EnderInventory enderInventory;

    void Awake()
    {
        enderInventory = GetComponent<EnderInventory>();
    }

    public override void SaveData()
    {
        List<string> slotStrList = new List<string>();

        foreach (SlotUI slot in enderInventory.slots)
        {
            string itemName = "";
            int count = 0;

            if (slot.currentItem != null)
            {
                itemName = slot.currentItem.name;
                count = slot.count;
            }

            slotStrList.Add($"{itemName}|{count}");
        }

        string saveStr = string.Join(";", slotStrList);
        SaveSystem.SaveEntity(saveableID, "EnderInventory", saveStr);
    }

    public override void LoadData()
    {
        string saveStr = SaveSystem.LoadEntity<string>(saveableID, "EnderInventory", "");

        if (string.IsNullOrEmpty(saveStr))
        {
            return;
        }

        string[] slotStrs = saveStr.Split(';');
        int loopCount = Mathf.Min(slotStrs.Length, enderInventory.slots.Count);

        for (int i = 0; i < loopCount; i++)
        {
            string[] parts = slotStrs[i].Split('|');
            if (parts.Length < 2)
            {
                enderInventory.slots[i].ClearSlot();
                continue;
            }

            string itemName = parts[0];
            if (!int.TryParse(parts[1], out int count))
            {
                enderInventory.slots[i].ClearSlot();
                continue;
            }

            if (!string.IsNullOrEmpty(itemName))
            {
                ItemData item = Resources.Load<ItemData>("Items/" + itemName);
                enderInventory.slots[i].SetItem(item, count);
            }
            else
            {
                enderInventory.slots[i].ClearSlot();
            }
        }

        for (int i = loopCount; i < enderInventory.slots.Count; i++)
        {
            enderInventory.slots[i].ClearSlot();
        }
    }
}