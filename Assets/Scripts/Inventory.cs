using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SunTemple;

public class Inventory : MonoBehaviour
{
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private PlayerHealth playerHealth;
    public GameObject Bag;
    public static Inventory instance; // 单例，方便其他脚本调用

    //====开锁标记，放在Inventory（有单例）====
    public static Door currentOperatingDoor;

    public List<SlotUI> slots = new List<SlotUI>(); // 存储所有背包格子
    private BagUI bagUI;
    void Awake()
    {
        instance = this;
        bagUI = Bag.GetComponent<BagUI>();
    }
    // 把你场景里的 8 个格子对象拖进这个列表
    public void InitSlots(List<SlotUI> slotList)
    {
        slots = slotList;
    }
    // 添加物品到背包
    public bool AddItem(ItemData item, int count = 1)
    {
        if (item.itemType == ItemType.Bag)
        {
            ExpandCapacity(2);
            return true;
        }
        // 1. 先找已有相同物品的格子，尝试堆叠
        foreach (var slot in slots)
        {
            if (slot.currentItem == item && item.canStack)
            {
                slot.SetItem(item, slot.count + count);
                return true;
            }
        }
        // 2. 没有可堆叠的，找第一个空格子
        foreach (var slot in slots)
        {
            if (slot.currentItem == null)
            {
                slot.SetItem(item, count);
                return true;
            }
        }
        // 3. 背包满了
        return false;
    }
    // 使用物品
    public void UseItem(SlotUI slot)
    {
        if (slot.currentItem == null) return;

        // =========【开锁模式优先】 =========
        Door targetDoor = currentOperatingDoor;
        if (targetDoor != null)
        {
            if (slot.currentItem.itemType == ItemType.KeyItem)
            {
                bool unlockSuccess = targetDoor.TryUnlockWithKey(slot.currentItem);
                if (unlockSuccess)
                {
                    //开锁成功消耗钥匙
                    slot.count--;
                    if (slot.count <= 0)
                    {
                        slot.ClearSlot();
                    }
                    else
                    {
                        slot.SetItem(slot.currentItem, slot.count);
                    }
                    currentOperatingDoor = null;
                    //调用UIManager关闭背包（统一做暂停恢复、鼠标锁定）
                    if (UIManager.Instance != null)
                    {
                        UIManager.Instance.ToggleBagAndQuickBar();
                    }
                    return;
                }
                else
                {
                    Debug.Log("这个钥匙不能打开这扇门");
                    //开锁失败，背包继续保持打开，允许继续选择其他格子
                    return;
                }
            }
            else
            {
                Debug.Log("这个物品不是钥匙");
                return;
            }
        }

        // ====== 下面全部是原来的正常物品使用逻辑，完全未改动 ======
        switch (slot.currentItem.itemType)
        {
            case ItemType.Consume:
                // 添加回血逻辑
                if (slot.currentItem.itemName == "绿草")
                {
                    if (playerHealth != null)
                    {
                        // 回血30，但不能超过最大血量
                        playerHealth.currentHp = Mathf.Min(playerHealth.currentHp + playerHealth.greenFlowerHealingAmount, playerHealth.maxHp);
                        // 更新UI显示
                        if (playerHealth.uiManager != null)
                        {
                            playerHealth.uiManager.OnPlayerHPChanged(
                                Mathf.RoundToInt(playerHealth.currentHp),
                                Mathf.RoundToInt(playerHealth.maxHp)
                            );
                        }
                    }
                }
                // 消耗品：使用后数量-1
                slot.count--;
                if (slot.count <= 0)
                {
                    slot.ClearSlot();
                }
                else
                {
                    slot.SetItem(slot.currentItem, slot.count);
                }
                break;
            case ItemType.Gun:
                break;
            case ItemType.Knife:
                break;
            case ItemType.Bag:
                break;
            case ItemType.KeyItem:
                //普通模式钥匙不能直接使用，只能在开锁模式消耗
                break;
            case ItemType.Material:
                if (slot.currentItem.itemName == "手雷")
                {
                    slot.count--;
                    if (slot.count <= 0)
                    {
                        slot.ClearSlot();
                    }
                    else
                    {
                        slot.SetItem(slot.currentItem, slot.count);
                    }
                }
                break;
            default:
                break;
        }
    }
    // 丢弃物品
    public void DropItem(SlotUI slot)
    {
        if (slot.currentItem == null) return;
        slot.ClearSlot();
    }
    // 背包扩容核心函数（适配你的层级与预制体）
    public void ExpandCapacity(int addCount = 2)
    {
        // 1. 定义扩容参数（可自行修改）
        int expandNum = addCount; // 每次扩容2格，你可改为1/4等
        // 2. 动态生成新格子
        for (int i = 0; i < expandNum; i++)
        {
            // 实例化新格子（使用背包格子预制体）
            GameObject newSlotObj = Instantiate(slotPrefab, Bag.transform);
            // 获取并初始化 SlotUI 组件
            SlotUI newSlot = newSlotObj.GetComponent<SlotUI>();
            newSlot.ClearSlot(); // 初始化为空格子
            slots.Add(newSlot);  // 添加到背包格子列表
        }
        // 3. 强制刷新背包UI布局（关键！解决格子错位/重叠）
        bagUI.RefreshBagUI();
    }
    public void ShrinkCapacity(int removeCount = 2)
    {
        // 最多只能删到 0 格
        int removeNum = Mathf.Min(removeCount, slots.Count);
        for (int i = 0; i < removeNum; i++)
        {
            // 从最后面开始删
            int index = slots.Count - 1;
            SlotUI slot = slots[index];
            // 先清物品
            slot.ClearSlot();
            // 删掉格子物体
            Destroy(slot.gameObject);
            // 从列表移除
            slots.RemoveAt(index);
        }
        bagUI.RefreshBagUI();
    }
    // 自动扩容 or 缩容，统一调用这个就够了
    public void SetSlotCount(int targetCount)
    {
        // 目标不能小于0
        targetCount = Mathf.Max(targetCount, 0);
        int current = slots.Count;
        if (targetCount > current)
        {
            // 需要扩容
            ExpandCapacity(targetCount - current);
        }
        else if (targetCount < current)
        {
            // 需要缩容
            ShrinkCapacity(current - targetCount);
        }
        // 等于就什么都不做
    }
}
