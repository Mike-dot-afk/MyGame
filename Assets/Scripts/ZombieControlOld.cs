using UnityEngine;
using UnityEngine.AI;
public class ZombieControlOld : EnemyAIBase
{
    // ===== 追击状态机 =====
    public enum AIState { Patrol, Chase }
    [Header("追击范围")]
    public float chaseRange = 8f;
    [Header("普通硬直/眩晕")]
    public float defaultStunTime = 1.2f;
    public bool canBeStun = true;
    [Header("抗性系统")]
    public float currentStunResist = 0f;
    public float maxStunResist = 1.0f;
    public float resistDecaySpeed = 0.3f;
    public float minStunTime = 0.25f;
    [Header("处决硬直")]
    public bool isExecutionStun = false;
    public float executionStunTime = 2.5f;
    public float currentExecutionTimer = 0f;
    public bool hasTriggeredExecution = false;
    // 子类独有硬直状态（基类不实现硬直）
    public bool isStunned = false;
    public float currentStunDuration = 0f;
    private AIState currentState = AIState.Patrol;
    protected override void Awake()
    {
        base.Awake();
    }
    /// <summary>核心AI，严格复刻原版Update逻辑顺序</summary>
    protected override void UpdateAIBehavior(float distanceToPlayer)
    {
        // ========== 1.处决硬直 ==========
        if (isExecutionStun)
        {
            currentExecutionTimer -= Time.deltaTime;
            agent.isStopped = true;
            if (currentExecutionTimer <= 0)
            {
                ExitExecutionStun();
            }
            return;
        }
        // ========== 2.普通受击硬直 ==========
        if (isStunned)
        {
            currentStunDuration -= Time.deltaTime;
            if (currentStunDuration <= 0)
            {
                ExitStun();
            }
            else
            {
                agent.isStopped = true;
                return;
            }
        }
        // ========== 3.抗性衰减 ==========
        if (currentStunResist > 0)
        {
            currentStunResist = Mathf.Max(0, currentStunResist - resistDecaySpeed * Time.deltaTime);
        }
        // ========== 4.攻击冷却 ==========
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }
        // ========== 5.距离分支逻辑 ==========
        if (distanceToPlayer <= attackRange)
        {
            HandleMeleeAttackInRange();
        }
        else
        {
            switch (currentState)
            {
                case AIState.Patrol:
                    UpdatePatrol(distanceToPlayer);
                    break;
                case AIState.Chase:
                    UpdateChase(distanceToPlayer);
                    break;
            }
        }
    }
    void UpdatePatrol(float distanceToPlayer)
    {
        // 新增：被其他敌人警报唤醒，直接进入追击，不等待自身检测玩家
        if (hasAlertPlayer)
        {
            currentState = AIState.Chase;
            hasPatrolTarget = false;
            UpdateChase(distanceToPlayer);
            return;
        }

        // 原有逻辑：自身检测到玩家进入追击范围
        if (distanceToPlayer <= chaseRange)
        {
            currentState = AIState.Chase;
            hasPatrolTarget = false;
            TriggerAlertToNearbyEnemies();
            UpdateChase(distanceToPlayer);
            return;
        }
        if (distanceToPlayer > patrolActivateRange)
        {
            if (agent != null)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
            return;
        }
        animator.ResetTrigger("IsAttacking");
        hasAlertPlayer = false;
        if (patrolWaitTimer > 0)
        {
            patrolWaitTimer -= Time.deltaTime;
            agent.isStopped = true;
            return;
        }
        if (!hasPatrolTarget || Vector3.Distance(transform.position, patrolTarget) < 1f)
        {
            PickNewPatrolTarget();
            return;
        }
        agent.isStopped = false;
        agent.SetDestination(patrolTarget);
    }
    void UpdateChase(float distanceToPlayer)
    {
        if (distanceToPlayer > giveUpRange)
        {
            currentState = AIState.Patrol;
            hasPatrolTarget = false;
            ClearAlertState();
            agent.isStopped = true;
            animator.ResetTrigger("IsAttacking");
            return;
        }
        agent.isStopped = false;
        agent.SetDestination(playerTransform.position);
        animator.ResetTrigger("IsAttacking");
    }
    /// <summary>读档后重置为待机，清除遗留的追击状态</summary>
    public override void ResetToPatrolState()
    {
        base.ResetToPatrolState();
        currentState = AIState.Patrol;
        hasPatrolTarget = false;
    }
    #region 普通硬直/眩晕接口（对外API原样保留）
    public void SetStun(float stunTime = 0)
    {
        if (isDead) return;
        if (!canBeStun) return;
        if (isStunned) return;
        isStunned = true;
        float rawTime = stunTime <= 0 ? defaultStunTime : stunTime;
        float finalStun = rawTime - currentStunResist;
        finalStun = Mathf.Max(minStunTime, finalStun);
        currentStunDuration = finalStun;
        currentStunResist = Mathf.Min(currentStunResist + 0.3f, maxStunResist);
        agent.isStopped = true;
        animator.ResetTrigger("IsAttacking");
        animator.SetTrigger("GetStun");
    }
    public void ExitStun()
    {
        if (isDead) return;
        isStunned = false;
        currentStunDuration = 0;
        animator.SetTrigger("StunEnd");
    }
    #endregion
    #region 处决硬直
    public void EnterExecutionStun()
    {
        if (isDead) return;
        if (hasTriggeredExecution) return;
        hasTriggeredExecution = true;
        isExecutionStun = true;
        currentExecutionTimer = executionStunTime;
        agent.isStopped = true;
        animator.ResetTrigger("IsAttacking");
        animator.SetTrigger("GetStun");
    }
    public void ExitExecutionStun()
    {
        if (isDead) return;
        isExecutionStun = false;
        currentExecutionTimer = 0;
        animator.SetTrigger("StunEnd");
    }
    #endregion
    /// <summary>死亡回调：清理僵尸全部独有的状态</summary>
    protected override void OnDie()
    {
        base.OnDie();
        isExecutionStun = false;
        currentExecutionTimer = 0;
        isStunned = false;
        currentStunDuration = 0;
        if (animator != null)
        {
            animator.ResetTrigger("GetStun");
            animator.ResetTrigger("StunEnd");
        }
    }
    /// <summary>重写行走低吼：增加硬直过滤，原版逻辑</summary>
    protected override void UpdateWalkGrowl()
    {
        // 原版条件：!isDead && !isStunned && !isExecutionStun
        if (isDead || isStunned || isExecutionStun || agent == null || audioSource == null)
            return;
        if (agent.velocity.magnitude > 0.2f)
        {
            walkGrowlTimer -= Time.deltaTime;
            if (walkGrowlTimer <= 0)
            {
                PlayRandomClip(walkClips);
                walkGrowlTimer = Random.Range(walkGrowlMinInterval, walkGrowlMaxInterval);
            }
        }
    }
}
