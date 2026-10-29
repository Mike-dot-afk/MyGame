using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class QuickBarDragDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // 背包是 static SlotUI，这里是 static WeaponQuickBarSlot
    public static WeaponQuickBarSlot draggedSlot;

    private WeaponQuickBarSlot _slot;
    private CanvasGroup _canvasGroup;
    private GameObject _dragIcon;
    private Image _dragIconImage;

    void Awake()
    {
        _slot = GetComponent<WeaponQuickBarSlot>();
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_slot == null || _slot.currentItem == null) return;

        draggedSlot = _slot;

        // 原格子半透明
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0.6f;
            _canvasGroup.blocksRaycasts = false;
        }

        // 创建拖拽替身（完全和背包一样）
        _dragIcon = new GameObject("DragIcon");
        _dragIcon.transform.SetParent(transform.root);
        _dragIcon.transform.SetAsLastSibling();

        CanvasGroup cg = _dragIcon.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        _dragIconImage = _dragIcon.AddComponent<Image>();
        _dragIconImage.sprite = _slot.icon.sprite;
        _dragIconImage.preserveAspect = true;

        _dragIcon.transform.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragIcon != null)
            _dragIcon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 销毁替身
        if (_dragIcon != null)
            Destroy(_dragIcon);
        _dragIcon = null;

        // 还原原格子
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
        }

        // 拖到外部 → 丢弃
        if (!EventSystem.current.IsPointerOverGameObject())
        {
            if (draggedSlot != null && draggedSlot.currentItem != null && draggedSlot.currentItem.canDiscard)
            {
                draggedSlot.ClearSlot();
            }
            draggedSlot = null;
            return;
        }

        // 找到目标格子
        GameObject targetObj = eventData.pointerCurrentRaycast.gameObject;
        if (targetObj == null)
        {
            draggedSlot = null;
            return;
        }

        // 目标是快捷栏格子
        WeaponQuickBarSlot targetSlot = targetObj.GetComponent<WeaponQuickBarSlot>();
        if (targetSlot == null || targetSlot == draggedSlot)
        {
            draggedSlot = null;
            return;
        }

        if (draggedSlot.slotIndex == 4 || targetSlot.slotIndex == 4)
        {
            draggedSlot = null;
            return;
        }
        // 交换（完全模仿背包 SwapItems）
        SwapSlots(draggedSlot, targetSlot);
        draggedSlot = null;
    }

    // 完全模仿背包 SwapItems
    void SwapSlots(WeaponQuickBarSlot from, WeaponQuickBarSlot to)
    {
        if (from == null || to == null) return;

        if (from.slotIndex > 3 || to.slotIndex > 3)
        {
            return;
        }
        ItemData temp = from.currentItem;
        from.SetItem(to.currentItem);
        to.SetItem(temp);
    }
}