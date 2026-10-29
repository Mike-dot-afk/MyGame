using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：当前是否手持手雷（手雷进行中分支入口）
public class PlayerBT_IsHoldingGrenade : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        return tps.IsHoldingGrenade() ? TaskStatus.Success : TaskStatus.Failure;
    }
}
