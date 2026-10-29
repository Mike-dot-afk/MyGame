using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 动作：开始换弹（播放换弹动画，状态由 GunWeapon 维护）
public class PlayerBT_StartReload : PlayerBT_ActionBase
{
    public override void OnStart()
    {
        if (tps != null) tps.StartReload();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
