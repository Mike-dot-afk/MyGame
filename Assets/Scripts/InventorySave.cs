using UnityEngine;
using System.Collections.Generic;

public class InventorySave : SaveableEntity
{
    private Inventory inventory;

    void Awake()
    {
        inventory = GetComponent<Inventory>();
    }

    public override void SaveData()
    {
        List<string> slotStrList = new List<string>();

        // 遍历所有格子，手动拼接每个格子的字符串
        foreach (SlotUI slot in inventory.slots)
        {
            string itemName = "";
            int count = 0;

            if (slot.currentItem != null)
            {
                itemName = slot.currentItem.name;
                count = slot.count;
            }

            // 单个格子格式：itemName|count
            slotStrList.Add($"{itemName}|{count}");
        }

        // 用分号拼接所有格子，形成最终存档字符串
        string saveStr = string.Join(";", slotStrList);

        SaveSystem.SaveEntity(saveableID, "Inventory", saveStr);
    }

    public override void LoadData()
    {
        string saveStr = SaveSystem.LoadEntity<string>(saveableID, "Inventory", "");

        // 空存档
        if (string.IsNullOrEmpty(saveStr))
        {
            return;
        }

        // 按分号拆分，得到每个格子的字符串
        string[] slotStrs = saveStr.Split(';');

        // 过滤空数据，防止 "" 变成 1 格
        int validCount = 0;
        for (int i = 0; i < slotStrs.Length; i++)
        {
            if (!string.IsNullOrEmpty(slotStrs[i]))
                validCount++;
        }

        // 只有有效数量 >0 才设置格子
        if (validCount > 0)
        {
            inventory.SetSlotCount(validCount);
        }

        int savedSlotCount = slotStrs.Length;
        inventory.SetSlotCount(savedSlotCount);
        int loopCount = Mathf.Min(slotStrs.Length, inventory.slots.Count);

        for (int i = 0; i < loopCount; i++)
        {
            string[] parts = slotStrs[i].Split('|');
            if (parts.Length < 2)
            {
                inventory.slots[i].ClearSlot();
                continue;
            }

            string itemName = parts[0];
            if (!int.TryParse(parts[1], out int count))
            {
                inventory.slots[i].ClearSlot();
                continue;
            }

            if (!string.IsNullOrEmpty(itemName))
            {
                ItemData item = Resources.Load<ItemData>("Items/" + itemName);
                inventory.slots[i].SetItem(item, count);
            }
            else
            {
                inventory.slots[i].ClearSlot();
            }
        }

        // 清空超出存档长度的格子
        for (int i = loopCount; i < inventory.slots.Count; i++)
        {
            inventory.slots[i].ClearSlot();
        }
    }
}