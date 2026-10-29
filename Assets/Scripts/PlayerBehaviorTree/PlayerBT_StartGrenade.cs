using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 动作：按6进入手持手雷（收武器 + 生成手雷，与原来的按键行为一致）
public class PlayerBT_StartGrenade : PlayerBT_ActionBase
{
    public override void OnStart()
    {
        if (tps != null) tps.TryStartHoldGrenade();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
