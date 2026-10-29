using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Tooltip("场景静态敌人存档唯一ID，不要手动修改，编辑器工具生成")]
    public string enemySaveID;

#if UNITY_EDITOR
    [ContextMenu("生成enemySaveID(GUID)")]
    private void GenerateEnemySaveId()
    {
        enemySaveID = System.Guid.NewGuid().ToString();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}