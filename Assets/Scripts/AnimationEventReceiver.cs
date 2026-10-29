using UnityEngine;

public class AnimationEventReceiver : MonoBehaviour
{
    // 引用你的第三人称控制器
    public ThirdPersonShooterController tpsController;

    // 动画事件调用：挥刀结束
    public void OnSlashAnimEnd()
    {
        var knife = tpsController.currentKnife;
        if (knife != null)
        {
            knife.SlashEnd();
        }
    }

    // 动画事件调用：挥刀击中敌人
    public void OnSlashHitEnemy()
    {
        var knife = tpsController.currentKnife;
        if (knife != null)
        {
            knife.OnSlashHit();
        }
    }
}