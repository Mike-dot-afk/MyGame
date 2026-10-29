using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 动作：投掷后清理动画层（树时序中位于 Wait 1.25s 之后）
public class PlayerBT_FinishThrowCleanup : PlayerBT_ActionBase
{
    public override void OnStart()
    {
        if (tps != null) tps.FinishThrowCleanup();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
