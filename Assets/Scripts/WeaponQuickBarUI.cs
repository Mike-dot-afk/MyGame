using UnityEngine;
using UnityEngine.UI;

public class WeaponQuickBarUI : MonoBehaviour
{
    [SerializeField] private GameObject weaponQuickPanel;
    [SerializeField] private Image[] gunSlots;    // 1~4号枪格（对应索引0~3）
    [SerializeField] private Image knifeSlot;     // 5号刀格（对应索引4）

    public static WeaponQuickBarUI Instance;
    public int currentSelectedSlotIndex = -1;

    void Start()
    {
        weaponQuickPanel.SetActive(false);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // 新增：初始化快捷栏格子（自动获取所有 WeaponQuickBarSlot 组件）
            InitQuickBarSlots();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitQuickBarSlots()
    {
        // 修复：不能对数组调用 Clear()，必须用 Array.Clear
        if (gunSlots != null)
        {
            System.Array.Clear(gunSlots, 0, gunSlots.Length);
        }

        WeaponQuickBarSlot[] allSlots = GetComponentsInChildren<WeaponQuickBarSlot>();
        foreach (var slot in allSlots)
        {
            if (slot.slotIndex >= 0 && slot.slotIndex <= 3)
            {
                gunSlots[slot.slotIndex] = slot.GetComponent<Image>();
            }
            else if (slot.slotIndex == 4)
            {
                knifeSlot = slot.GetComponent<Image>();
            }
        }
    }

    // 装备限制：判断武器能否放到指定格子
    public bool CanEquipToSlot(bool isGun, int slotIndex)
    {
        if (isGun)
        {
            // 枪只能放0~3（对应1~4号格）
            return slotIndex >= 0 && slotIndex <= 3;
        }
        else
        {
            // 刀只能放4（对应5号格）
            return slotIndex == 4;
        }
    }

    public void UpdateSlotIcon(int slotIndex, Sprite icon)
    {
        // 找到所有快捷栏格子
        WeaponQuickBarSlot[] allSlots = GetComponentsInChildren<WeaponQuickBarSlot>();

        foreach (var slot in allSlots)
        {
            // 找到 slotIndex 匹配的那个格子
            if (slot.slotIndex == slotIndex)
            {
                if (slot.icon != null)
                {
                    slot.icon.sprite = icon;
                    slot.icon.enabled = (icon != null);
                }
                return;
            }
        }
    }

    // 核心：装备武器到快捷栏（无数量、无堆叠）
    public void EquipWeaponToQuickBar(SlotUI fromSlot, WeaponQuickBarSlot targetSlot)
    {
        if (fromSlot == null || targetSlot == null || fromSlot.currentItem == null) return;

        ItemData draggedItem = fromSlot.currentItem;

        // 1. 校验物品类型与槽位匹配（枪械/刀具，无数量）
        bool isCanEquip = CheckItemTypeMatchSlot(draggedItem.itemType, targetSlot);
        if (!isCanEquip)
        {
            //Debug.Log($"【快捷栏】{draggedItem.itemName} 无法放入该槽位！");
            return;
        }

        // 2. 记录目标槽位原有物品（无数量）
        ItemData originalItem = targetSlot.currentItem;

        // 3. 装备新物品到目标槽位（无数量）
        targetSlot.EquipItem(draggedItem);

        // 4. 清空原背包格子（无数量）
        fromSlot.ClearSlot();

        // 5. 旧物品放回背包（无数量）
        if (originalItem != null && Inventory.instance != null)
        {
            bool addSuccess = Inventory.instance.AddItem(originalItem); // 无数量参数
            if (!addSuccess)
            {
                //Debug.Log("【快捷栏】背包已满，无法放回替换的物品！");
            }
        }
    }

    // 辅助：校验物品类型与槽位是否匹配（无数量）
    private bool CheckItemTypeMatchSlot(ItemType itemType, WeaponQuickBarSlot targetSlot)
    {
        switch (itemType)
        {
            case ItemType.Gun:
                // 枪械槽：0~3
                return targetSlot.slotIndex >= 0 && targetSlot.slotIndex <= 3;
            case ItemType.Knife:
                // 刀具槽：4
                return targetSlot.slotIndex == 4;
            default:
                // 消耗品/材料/关键道具 无法放入快捷栏
                return false;
        }
    }

    // 重载：根据物品类型判断是否可装备
    public bool CanEquipToSlot(ItemData item)
    {
        switch (item.itemType)
        {
            case ItemType.Gun:
                return true; // 所有枪械槽都可装备
            case ItemType.Knife:
                return true; // 仅刀具槽可装备
            default:
                return false; // 其他物品不可装备
        }
    }

    // 切换到指定槽位，并装备武器
    public void SwitchToSlot(int slotIndex)
    {
        currentSelectedSlotIndex = slotIndex;

        // 0 = 空手
        if (slotIndex == 0)
        {
            UnequipAllWeapons();
            return;
        }

        // 6 = 手雷
        if (slotIndex == 6)
        {
            UnequipAllWeapons();
            ThirdPersonShooterController.Instance.TryStartHoldGrenade();
            return;
        }

        // 1~5 → 对应内部 0~4
        int realIndex = slotIndex - 1;

        WeaponQuickBarSlot[] allSlots = GetComponentsInChildren<WeaponQuickBarSlot>();
        foreach (var slot in allSlots)
        {
            if (slot.slotIndex == realIndex)
            {
                EquipSlotItem(slot);
                return;
            }
        }

        UnequipAllWeapons();
    }

    // 装备某个槽位里的武器
    private void EquipSlotItem(WeaponQuickBarSlot slot)
    {
        if (slot == null || slot.currentItem == null)
        {
            UnequipAllWeapons();
            return;
        }

        UnequipAllWeapons();
        WeaponManager.Instance.EquipWeaponByItem(slot.currentItem);
    }

    // 卸下所有武器 → 空手
    public void UnequipAllWeapons()
    {
        WeaponManager.Instance.UnequipCurrentWeapon();
        ThirdPersonShooterController.Instance.ClearCurrentGrenade();
        currentSelectedSlotIndex = -1;
    }
}