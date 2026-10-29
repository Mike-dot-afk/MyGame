using System;
using UnityEngine;

public abstract class SaveableEntity : MonoBehaviour, ISaveable
{
    [Header("存档配置")]
    [Tooltip("唯一标识ID，预制体/实例唯一，编辑器生成，不要手动修改")]
    public string saveableID;

#if UNITY_EDITOR
    [ContextMenu("生成SaveableID(GUID)")]
    private void GenerateSaveableId()
    {
        saveableID = Guid.NewGuid().ToString();
        // 标记场景脏，确保修改保存到场景文件
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    // 删掉原来Start里面自动生成ID的逻辑！！
    protected virtual void Start()
    {
        // 不要在这里赋值saveableID！
    }

    public abstract void SaveData();
    public abstract void LoadData();
}