using UnityEngine;
using UnityEngine.AI;

public class MonsterHealth : EnemyHealthBase, IDamageable, IEnemyHealth
{
    [Header("硬直设置")]
    public float staggerDamageThreshold = 30f;
    private float accumulatedStaggerDamage = 0f;

    [Header("阶段转换设置")]
    public float phase2HealthPercent = 0.3f;
    private bool isPhase2Triggered = false;

    private bool _isHealthLocked = false;

    public void LockHealth(bool lockState)
    {
        _isHealthLocked = lockState;
    }

    public bool IsInPhase2()
    {
        return isPhase2Triggered;
    }

    protected override void Awake()
    {
        base.Awake();
        if (animator != null)
        {
            animator.SetFloat("StaggerAccumulated", accumulatedStaggerDamage);
            animator.SetBool("IsPhase2", false);
        }
    }

    public override void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (isDead()) return;
        if (_isHealthLocked) return;

        base.TakeDamage(damage, hitPoint);
        if (isDead()) return;

        accumulatedStaggerDamage += damage;
        if (animator != null)
        {
            animator.SetFloat("StaggerAccumulated", accumulatedStaggerDamage);
        }

        // 二阶段触发仅一次
        if (!isPhase2Triggered && currentHealth / maxHealth <= phase2HealthPercent)
        {
            isPhase2Triggered = true;
            if (animator != null)
            {
                animator.SetBool("IsPhase2", true);
            }
        }

        // 硬直爆发
        if (accumulatedStaggerDamage >= staggerDamageThreshold)
        {
            MonsterAnimationController ctrl = GetComponent<MonsterAnimationController>();
            if (ctrl != null)
            {
                ctrl.TriggerStagger();
            }
            accumulatedStaggerDamage = 0f;
            if (animator != null)
            {
                animator.SetFloat("StaggerAccumulated", accumulatedStaggerDamage);
            }
        }
    }

    protected override void OnPostDeath()
    {
        base.OnPostDeath();
        if (animator != null)
        {
            animator.SetBool("IsPhase2", false);
            animator.ResetTrigger("IsHit");
            animator.SetTrigger("IsDead");
        }

        MonsterAnimationController control = GetComponent<MonsterAnimationController>();
        if (control != null) control.enabled = false;

        MonsterAI ai = GetComponent<MonsterAI>();
        if (ai != null) ai.enabled = false;
    }

    protected override void OnAfterRevive()
    {
        base.OnAfterRevive();

        if (TryGetComponent<MonsterAnimationController>(out var control))
        {
            control.enabled = true;
        }
        if (TryGetComponent<MonsterAI>(out var ai))
        {
            ai.enabled = true;
        }

        if (animator != null)
        {
            animator.ResetTrigger("IsDead");
            animator.ResetTrigger("IsHit");
            bool shouldBeInPhase2 = (currentHealth / maxHealth <= phase2HealthPercent);
            isPhase2Triggered = shouldBeInPhase2;
            animator.SetBool("IsPhase2", shouldBeInPhase2);
        }

        accumulatedStaggerDamage = 0;
        if (animator != null)
        {
            animator.SetFloat("StaggerAccumulated", accumulatedStaggerDamage);
        }
    }

    public new bool isDead() => base.isDead();
}
