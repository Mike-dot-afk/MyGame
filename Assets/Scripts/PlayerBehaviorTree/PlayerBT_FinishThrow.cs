using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 动作：实际抛出并解锁（树时序中位于 Wait 0.75s 之后）
public class PlayerBT_FinishThrow : PlayerBT_ActionBase
{
    public override void OnStart()
    {
        if (tps != null) tps.FinishThrow();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
