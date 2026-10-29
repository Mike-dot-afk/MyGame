using UnityEngine;
/// 可拾取物品交互，挂载到道具物体上
public class ItemPickup : MonoBehaviour, IInteractable
{
    [Header("拾取物品配置")]
    public ItemData itemData;
    public int itemCount = 1;
    [Header("掉落归属")]
    public string belongBoxSaveID;
    [Header("pickupSaveID")]
    public string pickupSaveID; // 编辑器给每个场景物品分配唯一字符串/GUID，动态掉落物品不用填（有belongBoxSaveID会被跳过）
#if UNITY_EDITOR
    [ContextMenu("生成pickupSaveID(GUID)")]
    private void GeneratePickupSaveId()
    {
        pickupSaveID = System.Guid.NewGuid().ToString();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
    /// 交互提示：显示拾取物品名称
    public string GetInteractPrompt()
    {
        return $"拾取";
    }
    /// <summary>
    /// 交互逻辑：添加到背包，成功则禁用物体
    /// </summary>
    public void Interact()
    {
        if (Inventory.instance != null && Inventory.instance.AddItem(itemData, itemCount))
        {
            gameObject.SetActive(false);
            if (UIManager.Instance != null)
            {
                UIManager.Instance.RefreshAmmoUI();
            }

            // =========新增：拾取成功弹出拾取提示=========
            if (PickupNotifyManager.Instance != null && itemData != null)
            {
                PickupNotifyManager.Instance.ShowNotify(itemData.itemName, itemCount, itemData.icon);
            }
        }
    }
}
