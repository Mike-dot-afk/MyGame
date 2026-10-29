using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：当前是否处于手雷蓄力中
public class PlayerBT_IsThrowCharging : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        return tps.IsThrowCharging() ? TaskStatus.Success : TaskStatus.Failure;
    }
}
