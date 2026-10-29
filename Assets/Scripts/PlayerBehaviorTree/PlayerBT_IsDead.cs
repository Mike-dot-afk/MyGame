using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 条件：玩家是否已死亡（生命分支入口）
public class PlayerBT_IsDead : PlayerBT_ConditionalBase
{
    public override TaskStatus OnUpdate()
    {
        if (PlayerHealth.Instance == null) return TaskStatus.Failure;
        return PlayerHealth.Instance.alreadyDead ? TaskStatus.Success : TaskStatus.Failure;
    }
}
