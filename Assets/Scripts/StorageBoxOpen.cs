using UnityEngine;

/// <summary>
/// 储物箱交互，挂载到箱子/柜子上
/// 靠近按 F：开关储物箱界面
/// </summary>
public class StorageBoxOpen : MonoBehaviour, IInteractable
{
    [Header("储物箱配置")]
    public string boxName = "储物箱";

    public UIManager uiManager;

    public string GetInteractPrompt()
    {
        return $"打开{boxName}";
    }

    /// <summary>
    /// 交互逻辑：开关储物箱
    /// </summary>
    public void Interact()
    {
        if (uiManager != null)
        {
            uiManager.ToggleStorageBox();
        }
    }
}