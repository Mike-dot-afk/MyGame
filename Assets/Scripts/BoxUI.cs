using UnityEngine;

public class BoxUI : MonoBehaviour
{
    [SerializeField] private GameObject boxPanel;

    void Start()
    {
        boxPanel.SetActive(false);
    }

    public void SetBoxActive(bool active)
    {
        boxPanel.SetActive(active);
        if (active) RefreshBoxUI();
    }

    public bool IsBoxOpen() => boxPanel.activeSelf;

    // 刷新时从全局末影箱库存取
    public void RefreshBoxUI()
    {
        foreach (var slot in EnderInventory.instance.slots)
        {
            if (slot.currentItem != null)
                slot.SetItem(slot.currentItem, slot.count);
            else
                slot.ClearSlot();
        }
    }
}