using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 动作：近战攻击（由 Knife 内部管理冷却与连击）
public class PlayerBT_KnifeAttack : PlayerBT_ActionBase
{
    public override void OnStart()
    {
        if (tps != null && tps.currentKnife != null)
        {
            tps.currentKnife.Attack();
        }
    }

    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}
