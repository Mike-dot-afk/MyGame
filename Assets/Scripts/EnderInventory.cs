using System.Collections.Generic;
using UnityEngine;

public class EnderInventory : MonoBehaviour
{
    public static EnderInventory instance;  // 全局唯一单例

    public List<SlotUI> slots = new List<SlotUI>();

    void Awake()
    {
        instance = this;
    }

    // 添加物品（和你的 Inventory 逻辑完全一样）
    public bool AddItem(ItemData item, int count = 1)
    {
        foreach (var slot in slots)
        {
            if (slot.currentItem == item)
            {
                slot.SetItem(item, slot.count + count);
                return true;
            }
        }

        foreach (var slot in slots)
        {
            if (slot.currentItem == null)
            {
                slot.SetItem(item, count);
                return true;
            }
        }
        return false;
    }
}