using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 动作：开火（内部校验并消费 shoot 输入，含后坐力）
public class PlayerBT_FireWeapon : PlayerBT_ActionBase
{
    public override void OnStart()
    {
        if (tps != null) tps.FireWeapon();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
