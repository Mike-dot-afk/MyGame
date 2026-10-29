using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

// 行为树条件任务基类：自动获取副本上的 ThirdPersonShooterController_Tree
public abstract class PlayerBT_ConditionalBase : Conditional
{
    protected ThirdPersonShooterController_Tree tps;

    public override void OnAwake()
    {
        tps = GetComponent<ThirdPersonShooterController_Tree>();
    }
}
