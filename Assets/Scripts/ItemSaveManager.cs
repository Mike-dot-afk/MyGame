using UnityEngine;
using System.Collections.Generic;

public class ItemSaveManager : SaveableEntity
{
    public static ItemSaveManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    // 找到所有场景物品（包括被禁用的）,跳过箱子动态掉落的物品
    private List<ItemPickup> GetAllItems()
    {
        List<ItemPickup> list = new List<ItemPickup>();
        foreach (var item in UnityEngine.Object.FindObjectsOfType<ItemPickup>(includeInactive: true))
        {
            if (!string.IsNullOrEmpty(item.belongBoxSaveID))
                continue;
            list.Add(item);
        }
        return list;
    }

    public override void SaveData()
    {
        var items = GetAllItems();
        foreach (var item in items)
        {
            // 防御：静态场景拾取物pickupSaveID不能为空
            if (string.IsNullOrWhiteSpace(item.pickupSaveID))
            {
                Debug.LogWarning($"场景拾取物 {item.gameObject.name} pickupSaveID为空，跳过存档", item);
                continue;
            }
            // 核心：不用i下标，使用物品自身稳定pickupSaveID作为key
            string key = $"{saveableID}_Item_{item.pickupSaveID}";
            SaveSystem.SaveEntity(key, "Active", item.gameObject.activeSelf);
        }
    }

    public override void LoadData()
    {
        var items = GetAllItems();
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.pickupSaveID))
            {
                Debug.LogWarning($"场景拾取物 {item.gameObject.name} pickupSaveID为空，跳过读档", item);
                continue;
            }
            string key = $"{saveableID}_Item_{item.pickupSaveID}";
            // 第三个参数true：存档没有该key，默认物体是激活状态
            bool wasActive = SaveSystem.LoadEntity(key, "Active", true);
            item.gameObject.SetActive(wasActive);
        }
    }
}