using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：蓄力中松开右键（触发投掷时序）
public class PlayerBT_RMBReleased : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        if (!tps.IsThrowCharging()) return TaskStatus.Failure;
        return Input.GetMouseButtonUp(1) ? TaskStatus.Success : TaskStatus.Failure;
    }
}
