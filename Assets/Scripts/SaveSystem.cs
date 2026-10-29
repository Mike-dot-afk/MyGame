using UnityEngine;

public static class SaveSystem
{
    // 当前正在读写的存档槽位（由UI设置）
    public static string currentSlotID { get; set; }
    public const string TempSaveSlot = "TempSave";
    public const string CheckpointSaveSlot = "CheckpointSave";

    // 上层唯一入口：保存实体数据
    public static void SaveEntity<T>(string saveableID, string key, T data)
    {
        if (string.IsNullOrEmpty(currentSlotID))
        {
            Debug.LogError("未设置存档槽位！");
            return;
        }

        // 中层拼接最终key：slotID + saveableID + key
        string finalKey = $"{currentSlotID}_{saveableID}_{key}";

        // 中层统一调用底层
        SaveFileManager.Instance.SaveData(finalKey, data);
    }

    // 上层唯一入口：加载实体数据
    public static T LoadEntity<T>(string saveableID, string key, T defaultValue)
    {
        if (string.IsNullOrEmpty(currentSlotID))
            return defaultValue;

        string finalKey = $"{currentSlotID}_{saveableID}_{key}";

        // 中层统一调用底层
        return SaveFileManager.Instance.LoadData(finalKey, defaultValue);
    }

    // 供UI存档调用：设置槽位 → 触发全局存档
    public static void SaveGame(string slotID)
    {
        currentSlotID = slotID;

        // 遍历所有实体，让它们自己存（通过SaveSystem）
        foreach (var entity in Object.FindObjectsOfType<SaveableEntity>(includeInactive: true))
        {
            entity.SaveData();
        }
    }

    // 供UI读档调用
    public static void LoadGame(string slotID)
    {
        currentSlotID = slotID;

        foreach (var entity in Object.FindObjectsOfType<SaveableEntity>(includeInactive: true))
        {
            entity.LoadData();
        }
    }
}