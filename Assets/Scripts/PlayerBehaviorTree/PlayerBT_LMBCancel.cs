using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：蓄力中按下左键（取消投掷）
public class PlayerBT_LMBCancel : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        if (!tps.IsThrowCharging()) return TaskStatus.Failure;
        return Input.GetMouseButtonDown(0) ? TaskStatus.Success : TaskStatus.Failure;
    }
}
