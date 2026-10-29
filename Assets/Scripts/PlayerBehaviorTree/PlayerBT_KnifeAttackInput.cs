using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：持刀且点击射击键（消费输入，点击一次触发一次近战）
public class PlayerBT_KnifeAttackInput : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        if (!tps.HasKnife()) return TaskStatus.Failure;
        if (!tps.IsShootInputPressed()) return TaskStatus.Failure;
        tps.ConsumeShootInput();
        return TaskStatus.Success;
    }
}
