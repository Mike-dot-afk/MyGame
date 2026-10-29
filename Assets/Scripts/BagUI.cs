using UnityEngine;

public class BagUI : MonoBehaviour
{
    [SerializeField] private GameObject bagPanel;
    public Inventory inventory;
    void Start()
    {
        // 默认隐藏背包
        bagPanel.SetActive(false);
    }

    // 切换背包显示/隐藏
    public bool ToggleBag()
    {
        bool isActive = !bagPanel.activeSelf;
        bagPanel.SetActive(isActive);
        if (isActive) RefreshBagUI();
        return isActive;
    }

    // 强制设置背包显隐
    public void SetBagActive(bool active)
    {
        bagPanel.SetActive(active);
    }

    // 判断背包是否打开
    public bool IsBagOpen()
    {
        return bagPanel.activeSelf;
    }

    // 刷新背包 UI（比如打开时重新渲染图标）
    public void RefreshBagUI()
    {
        foreach (var slot in inventory.slots)
        {
            if (slot.currentItem != null)
            {
                slot.SetItem(slot.currentItem, slot.count);
            }
            else
            {
                slot.ClearSlot();
            }
        }
    }
}