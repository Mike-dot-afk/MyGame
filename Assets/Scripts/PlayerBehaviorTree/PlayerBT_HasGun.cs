using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：当前是否持枪（GunWeapon 及其子类）
public class PlayerBT_HasGun : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Failure;
        return tps.HasGun() ? TaskStatus.Success : TaskStatus.Failure;
    }
}
