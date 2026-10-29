using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 动作：取消手雷蓄力（回到手持状态）
public class PlayerBT_CancelGrenade : PlayerBT_ActionBase
{
    public override void OnStart()
    {
        if (tps != null) tps.CancelGrenadeThrow();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
