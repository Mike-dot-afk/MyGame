using UnityEngine;

public class CollectibleSave : SaveableEntity
{
    [Header("收集品专属数据")]
    public bool isCollected = false;

    public override void SaveData()
    {
        if (SaveFileManager.Instance == null) return;

        SaveFileManager.Instance.SaveData($"{saveableID}_Position", transform.position);
        SaveFileManager.Instance.SaveData($"{saveableID}_Active", gameObject.activeSelf);
        SaveFileManager.Instance.SaveData($"{saveableID}_Collected", isCollected);
    }

    public override void LoadData()
    {
        if (SaveFileManager.Instance == null) return;

        Vector3 pos = SaveFileManager.Instance.LoadData($"{saveableID}_Position", transform.position);
        bool isActive = SaveFileManager.Instance.LoadData($"{saveableID}_Active", true);
        transform.position = pos;
        gameObject.SetActive(isActive);

        isCollected = SaveFileManager.Instance.LoadData($"{saveableID}_Collected", false);
        // 自定义逻辑：收集后隐藏物体
        gameObject.SetActive(!isCollected);
    }
}