using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：瞄准中按下射击且枪械可开火（不消费输入，由 FireWeapon 动作内部消费）
public class PlayerBT_CanFire : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        return tps.CanFireNow() ? TaskStatus.Success : TaskStatus.Failure;
    }
}
