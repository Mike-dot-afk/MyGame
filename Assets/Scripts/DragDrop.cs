using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static SlotUI draggedSlot;
    private CanvasGroup canvasGroup;
    private SlotUI slotUI;
    private GameObject dragIcon; // 拖拽替身
    private Image dragIconImage;

    void Awake()
    {
        slotUI = GetComponent<SlotUI>();
        canvasGroup = GetComponent<CanvasGroup>();
        //Debug.Log($"【拖拽调试】Awake: 找到格子 {gameObject.name}, SlotUI={slotUI != null}, CanvasGroup={canvasGroup != null}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (slotUI.currentItem == null || canvasGroup == null) return;

        draggedSlot = slotUI;
        // 让原格子半透明，但位置完全不动
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;

        // --- 关键：创建拖拽替身，和原格子无关 ---
        dragIcon = new GameObject("DragIcon");
        // 父节点设为 Bag，避免被原格子裁剪
        dragIcon.transform.SetParent(transform.parent);
        dragIcon.transform.SetAsLastSibling(); // 显示在最上层

        // 给替身加 Image，复制原物品图标
        CanvasGroup dragCanvas = dragIcon.AddComponent<CanvasGroup>();
        dragCanvas.blocksRaycasts = false; // 不拦截射线
        dragIconImage = dragIcon.AddComponent<Image>();
        dragIconImage.sprite = slotUI.icon.sprite; // 直接复制原格子的物品图标
        dragIconImage.SetNativeSize();
        dragIcon.transform.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggedSlot == null || dragIcon == null) return;
        // 只移动替身，原格子（object）及其子物体完全不动
        dragIcon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggedSlot == null) return;

        // 销毁替身
        if (dragIcon != null) Destroy(dragIcon);
        dragIcon = null;

        // 还原原格子状态（位置不变，只改透明度和射线拦截）
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // 第一步：判断是否拖拽到UI外部（核心：丢弃判断）
        bool isDroppedOutside = !EventSystem.current.IsPointerOverGameObject();

        // 第二步：获取目标格子
        SlotUI targetSlot = null;
        WeaponQuickBarSlot targetQuickSlot = null;
        GameObject targetObj = eventData.pointerCurrentRaycast.gameObject;
        if (targetObj != null)
        {
            targetSlot = targetObj.GetComponent<SlotUI>();
            targetQuickSlot = targetObj.GetComponent<WeaponQuickBarSlot>();
        }

        // 第三步：执行丢弃/交换逻辑
        if (isDroppedOutside)
        {
            // 拖拽到外部：检查物品是否可丢弃
            if (draggedSlot.currentItem != null && draggedSlot.currentItem.canDiscard)
            {
                draggedSlot.ClearSlot(); // 清空格子，完成丢弃
            }
            else
            {
                // 关键物品不可丢弃，弹出提示（可选，你可以自行加UI提示）
            }
        }
        else if (targetQuickSlot != null)
        {
            HandleQuickBarDrag(draggedSlot, targetQuickSlot);
        }
        else if (targetSlot != null && targetSlot != draggedSlot)
        {
            SwapItems(draggedSlot, targetSlot);
        }

        draggedSlot = null;
    }

    void SwapItems(SlotUI from, SlotUI to)
    {
        // 如果两个格子物品一样，并且可以堆叠 → 合并，而不是交换
        if (from.currentItem == to.currentItem && from.currentItem != null && from.currentItem.canStack)
        {
            int canAdd = int.MaxValue; // 无上限
            int add = Mathf.Min(canAdd, from.count);

            to.count += add;
            to.SetItem(to.currentItem, to.count);

            from.count -= add;
            if (from.count <= 0)
                from.ClearSlot();
            else
                from.SetItem(from.currentItem, from.count);

            return;
        }

        // 否则 → 正常交换（武器、不同物品都会走这里）
        ItemData tempItem = to.currentItem;
        int tempCount = to.count;

        to.SetItem(from.currentItem, from.count);
        from.SetItem(tempItem, tempCount);
    }

    // 新增方法：处理快捷栏装备逻辑
    private void HandleQuickBarDrag(SlotUI fromSlot, WeaponQuickBarSlot toSlot)
    {
        // 新增：物品类型校验逻辑
        ItemData draggedItem = fromSlot.currentItem;
        if (draggedItem == null) return;

        // 新增：根据物品类型判断是否可装备
        bool canEquip = false;
        switch (draggedItem.itemType)
        {
            case ItemType.Gun:
                canEquip = toSlot.CompareTag("GunSlot");
                break;
            case ItemType.Knife:
                canEquip = toSlot.CompareTag("KnifeSlot");
                break;
            default:
                canEquip = false;
                break;
        }

        // 新增：校验通过则执行装备
        if (canEquip)
        {
            // 新增：调用快捷栏装备方法
            WeaponQuickBarUI.Instance.EquipWeaponToQuickBar(fromSlot, toSlot);
        }
    }
}