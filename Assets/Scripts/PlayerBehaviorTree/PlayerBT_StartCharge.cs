using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 动作：开始手雷蓄力（动画层/轨迹线/锁旋转/禁冲刺跳跃）
public class PlayerBT_StartCharge : PlayerBT_ActionBase
{
    public override void OnStart()
    {
        if (tps != null) tps.StartCharge();
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
