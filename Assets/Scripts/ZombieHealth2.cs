using UnityEngine;
using UnityEngine.AI;

public class ZombieHealth2 : EnemyHealthBase, IDamageable, IEnemyHealth
{
    [Header("斧头对象")]
    public Transform axe;

    protected override void Awake()
    {
        base.Awake();
        // base.Awake 已处理maxHealth/currentHealth、animator、enemyAI
    }

    /// <summary>死亡后期回调：隐藏斧头，基类已经完成关闭碰撞、调用AI.Die、agent处理、延迟隐藏</summary>
    protected override void OnPostDeath()
    {
        base.OnPostDeath();
        if (axe != null)
        {
            axe.gameObject.SetActive(false);
        }

        // 禁用ZombieControl2（兼容逻辑，enemyAI已经调用Die，但保留原行为）
        ZombieControl2 control = GetComponent<ZombieControl2>();
        if (control != null)
        {
            control.enabled = false;
        }
    }

    /// <summary>复活回调：恢复斧头显示，基类已恢复血量、碰撞、agent、调用enemyAI.ReviveAI()</summary>
    protected override void OnAfterRevive()
    {
        base.OnAfterRevive();
        if (axe != null)
        {
            axe.gameObject.SetActive(true);
        }

        // 重新启用AI脚本
        ZombieControl2 control = GetComponent<ZombieControl2>();
        if (control != null)
        {
            control.enabled = true;
        }
    }

    // 接口实现，直接转发基类
    public new void TakeDamage(float damage, Vector3 hitPoint) => base.TakeDamage(damage, hitPoint);
    public new bool isDead() => base.isDead();
}
