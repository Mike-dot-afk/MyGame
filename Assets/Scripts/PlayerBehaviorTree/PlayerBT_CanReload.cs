using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：按下 R 键且当前可换弹
public class PlayerBT_CanReload : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        if (!Input.GetKeyDown(KeyCode.R)) return TaskStatus.Failure;
        return tps.CanReloadNow() ? TaskStatus.Success : TaskStatus.Failure;
    }
}
