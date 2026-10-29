using UnityEngine;

/// <summary>
/// 存档读档接口，实现此接口的对象可被存档/读档
/// </summary>
public interface ISaveable
{
    /// <summary>
    /// 保存数据
    /// </summary>
    void SaveData();

    /// <summary>
    /// 加载数据
    /// </summary>
    void LoadData();
}