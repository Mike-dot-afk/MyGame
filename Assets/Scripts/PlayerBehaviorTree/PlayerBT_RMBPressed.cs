using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：手持手雷时刚按下右键（蓄力入口，要求尚未蓄力）
public class PlayerBT_RMBPressed : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        if (!tps.IsHoldingGrenade()) return TaskStatus.Failure;
        if (tps.IsThrowCharging()) return TaskStatus.Failure;
        return Input.GetMouseButtonDown(1) ? TaskStatus.Success : TaskStatus.Failure;
    }
}
