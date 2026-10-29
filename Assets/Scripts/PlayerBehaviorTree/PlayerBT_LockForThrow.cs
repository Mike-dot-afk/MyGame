using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 动作：蓄力结束、锁定玩家操作（松开右键瞬间，时序后续由树控制）
public class PlayerBT_LockForThrow : PlayerBT_ActionBase
{
    public override void OnStart()
    {
        if (tps != null) tps.LockForThrow();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
