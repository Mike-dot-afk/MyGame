using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ZombieHealth : EnemyHealthBase, IDamageable, IEnemyHealth
{
    [Header("硬直配置")]
    public float hitStunTime = 0.7f;

    private ZombieControlOld _zombieAi;

    protected override void Awake()
    {
        base.Awake();
        // base.Awake已经处理animator、enemyAI
        _zombieAi = GetComponent<ZombieControlOld>();
    }

    /// <summary>受击后，血量>0才会进入这里，基类已经完成扣血</summary>
    protected override void OnAfterTakeDamage(Vector3 hitPoint)
    {
        base.OnAfterTakeDamage(hitPoint);

        // 血量>0才判断半血处决
        if (_zombieAi != null && !_zombieAi.hasTriggeredExecution)
        {
            float halfHp = maxHealth * 0.5f;
            if (currentHealth <= halfHp)
            {
                _zombieAi.EnterExecutionStun();
            }
        }

        // 普通受击硬直
        if (_zombieAi != null && !_zombieAi.isExecutionStun)
        {
            _zombieAi.SetStun(hitStunTime);
        }
    }

    /// <summary>死亡末尾回调，基类已经：关闭碰撞、调用enemyAI.Die、关闭Agent、延迟SetActive</summary>
    protected override void OnPostDeath()
    {
        base.OnPostDeath();

        // 兼容新版AI
        ZombieControl control = GetComponent<ZombieControl>();
        if (control != null)
        {
            control.enabled = false;
        }

        // 生成战利品
        ZombieLootSave lootSave = GetComponent<ZombieLootSave>();
        if (lootSave != null)
        {
            lootSave.SpawnZombieLoot();
        }
    }

    /// <summary>复活末尾回调，基类已经恢复血量、碰撞、Agent、调用enemyAI.ReviveAI</summary>
    protected override void OnAfterRevive()
    {
        base.OnAfterRevive();

        // 兼容新版AI重置
        ZombieControl control = GetComponent<ZombieControl>();
        if (control != null)
        {
            control.enabled = true;
            control.hasTriggeredExecution = false;
            control.isExecutionStun = false;
        }

        // 旧版AI状态重置（注意：ReviveAI只重置isDead，处决/硬直标记子类自己清）
        if (_zombieAi != null)
        {
            _zombieAi.isStunned = false;
            _zombieAi.isExecutionStun = false;
            _zombieAi.hasTriggeredExecution = false;
            _zombieAi.currentStunDuration = 0;
            _zombieAi.currentExecutionTimer = 0;
            _zombieAi.enabled = true;
        }

        if (animator != null)
        {
            animator.Rebind();
            animator.ResetTrigger("GetStun");
            animator.ResetTrigger("StunEnd");
        }

        ZombieLootSave lootSave = GetComponent<ZombieLootSave>();
        if (lootSave != null)
        {
            lootSave.DestroyOwnLoot();
        }
    }

    // 接口实现直接使用基类
    public new void TakeDamage(float damage, Vector3 hitPoint) => base.TakeDamage(damage, hitPoint);
    public new bool isDead() => base.isDead();
}
