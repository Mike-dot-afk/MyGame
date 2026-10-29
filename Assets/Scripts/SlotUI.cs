using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class SlotUI : MonoBehaviour, IPointerClickHandler
{
    public Image icon;          // 格子里的物品图标
    public TextMeshProUGUI countText;      // 显示物品数量的文本
    public ItemData currentItem;// 当前格子里的物品
    public int count = 0;       // 物品数量

    // 替代原来 Button 的点击回调
    public void OnPointerClick(PointerEventData eventData)
    {

        if (currentItem != null)
        {
            if (Inventory.instance == null)
            {
                return;
            }
            // 直接调用使用物品逻辑
            Inventory.instance.UseItem(this);
        }
    }

    // 设置格子里的物品和数量
    public void SetItem(ItemData item, int num = 1)
    {
        currentItem = item;
        count = num;

        if (item != null)
        {
            icon.sprite = item.icon;
            countText.text = count > 1 ? count.ToString() : "";
        }
        else
        {
            ClearSlot();
        }
    }

    // 清空格子
    public void ClearSlot()
    {
        currentItem = null;
        count = 0;
        if (icon != null)
            icon.sprite = null;
        if (countText != null)
            countText.text = "";
    }
}