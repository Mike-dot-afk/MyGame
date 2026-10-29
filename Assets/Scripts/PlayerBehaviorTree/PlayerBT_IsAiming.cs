using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：当前是否处于瞄准状态（由脚本侧的瞄准输入决策维护）
public class PlayerBT_IsAiming : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        return tps.IsAiming() ? TaskStatus.Success : TaskStatus.Failure;
    }
}
