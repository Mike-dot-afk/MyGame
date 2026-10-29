using UnityEngine;

/// <summary>
/// 存档点交互，挂载到存档台、床等，仅打开存档UI
/// </summary>
public class SaveMenuOpen : MonoBehaviour, IInteractable
{
    [Header("存档点配置")]
    public string savePointName = "存档点";

    public SaveUIManager saveUIManager;

    public string GetInteractPrompt()
    {
        return $"打开{savePointName}";
    }

    /// 交互逻辑：仅打开存档UI，无任何存档操作
    ///</summary>
    public void Interact()
    {
        // 直接打开存档UI面板
        if (saveUIManager != null && !UIManager.Instance.bagUI.IsBagOpen())
        {
            saveUIManager.ToggleUI();
        }
    }
}