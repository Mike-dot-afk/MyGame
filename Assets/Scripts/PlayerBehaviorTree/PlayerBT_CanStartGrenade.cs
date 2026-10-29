using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：按下数字键6 且 背包有手雷 且 当前未持有未蓄力（手雷进入分支入口）
public class PlayerBT_CanStartGrenade : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        if (tps.IsHoldingGrenade()) return TaskStatus.Failure;
        if (tps.IsThrowCharging()) return TaskStatus.Failure;
        if (!Input.GetKeyDown(KeyCode.Alpha6)) return TaskStatus.Failure;
        return HasGrenadeInInventory() ? TaskStatus.Success : TaskStatus.Failure;
    }

    private bool HasGrenadeInInventory()
    {
        if (Inventory.instance == null || Inventory.instance.slots == null) return false;
        foreach (var slot in Inventory.instance.slots)
        {
            if (slot.currentItem != null && slot.currentItem.itemName == "手雷") return true;
        }
        return false;
    }
}
