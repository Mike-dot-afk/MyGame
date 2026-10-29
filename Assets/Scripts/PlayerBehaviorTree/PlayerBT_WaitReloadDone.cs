using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 动作：等待换弹完成（Running 直到 isReloading 变为 false，由动画事件触发补弹）
public class PlayerBT_WaitReloadDone : PlayerBT_ActionBase
{
    public override TaskStatus OnUpdate()
    {
        if (tps == null) return TaskStatus.Success;
        return tps.IsReloading() ? TaskStatus.Running : TaskStatus.Success;
    }
}
