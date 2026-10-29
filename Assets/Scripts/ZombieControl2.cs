using UnityEngine;
using UnityEngine.AI;
public class ZombieControl2 : EnemyAIBase
{
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
    // 硬直状态字段
    public bool isStunned = false;
    public float currentStunDuration = 0f;
    [Header("远程投掷参数")]
    public float throwRangeStart = 2f;
    public float throwRange = 4.5f;
    public float throwCooldown = 3f;
    private float throwCooldownTimer;
    [Header("手斧的位置切换")]
    public Transform axe;
    public Transform holdPoint;
    public Transform aimPoint;
    [Header("斧头相关")]
    public float recoverAxeTime = 2f;
    public Transform throwSpawnPoint;
    public GameObject flyingAxePrefab;
    private float axePoseWeight = 0;
    private float targetWeight = 0;
    private AIState currentState = AIState.Patrol;
    protected override void Awake()
    {
        base.Awake();
        axe.gameObject.SetActive(true);
        throwCooldownTimer = 3f;
    }
    protected override void UpdateAIBehavior(float distanceToPlayer)
    {
        // ========== 1.普通受击硬直 ==========
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
        // ========== 2.抗性衰减 ==========
        if (currentStunResist > 0)
        {
            currentStunResist = Mathf.Max(0, currentStunResist - resistDecaySpeed * Time.deltaTime);
        }
        // ==========3.攻击冷却 ==========
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }
        // 斧头位置平滑插值
        axePoseWeight = Mathf.Lerp(axePoseWeight, targetWeight, Time.deltaTime * 13f);
        axe.transform.SetPositionAndRotation(
            Vector3.Lerp(holdPoint.position, aimPoint.position, axePoseWeight),
            Quaternion.Lerp(holdPoint.rotation, aimPoint.rotation, axePoseWeight)
        );
        // 投掷冷却计时
        if (throwCooldownTimer > 0)
            throwCooldownTimer -= Time.deltaTime;
        // 1.近战范围
        if (distanceToPlayer <= attackRange)
        {
            animator.ResetTrigger("IsThrowing");
            HandleMeleeAttackInRange();
            SetAxePoint();
        }
        // 2.投掷范围
        else if (distanceToPlayer > throwRangeStart && distanceToPlayer <= throwRange)
        {
            animator.ResetTrigger("IsAttacking");
            agent.SetDestination(playerTransform.position);
            if (throwCooldownTimer <= 0)
            {
                animator.SetTrigger("IsThrowing");
                throwCooldownTimer = throwCooldown;
            }
            FacePlayer();
        }
        // 3.巡逻/追击
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
        animator.ResetTrigger("IsThrowing");
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
            animator.ResetTrigger("IsThrowing");
            return;
        }
        agent.isStopped = false;
        agent.SetDestination(playerTransform.position);
        animator.ResetTrigger("IsAttacking");
        animator.ResetTrigger("IsThrowing");
    }
    /// <summary>读档后重置为待机，清除遗留的追击状态</summary>
    public override void ResetToPatrolState()
    {
        base.ResetToPatrolState();
        currentState = AIState.Patrol;
        hasPatrolTarget = false;
    }
    #region 硬直对外API
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
    #region 斧头动画事件接口（原样保留，Animator无需改动）
    void SetAxePoint()
    {
        targetWeight = 1f;
    }
    void ResetAxePoint()
    {
        targetWeight = 0f;
    }
    public void OnAttackEnd()
    {
        ResetAxePoint();
    }
    public void OnThrowStart()
    {
        agent.isStopped = true;
        animator.SetFloat("MoveSpeed", 0f);
    }
    public void OnThrowEnd()
    {
        agent.isStopped = false;
    }
    public void OnAxeThrowEvent()
    {
        if (axe != null)
            axe.gameObject.SetActive(false);
        Invoke(nameof(RecoverHandAxe), recoverAxeTime);
        SpawnFlyingAxe();
    }
    void RecoverHandAxe()
    {
        if (axe != null)
            axe.gameObject.SetActive(true);
    }
    void SpawnFlyingAxe()
    {
        Vector3 spawnPos = throwSpawnPoint.position;
        Quaternion spawnRot = throwSpawnPoint.rotation;
        Instantiate(flyingAxePrefab, spawnPos, spawnRot);
    }
    #endregion
    /// <summary>死亡回调：清理硬直 + 取消斧头恢复延时</summary>
    protected override void OnDie()
    {
        base.OnDie();
        CancelInvoke(nameof(RecoverHandAxe));
        // 清空硬直状态
        isStunned = false;
        currentStunDuration = 0;
        if (animator != null)
        {
            animator.ResetTrigger("GetStun");
            animator.ResetTrigger("StunEnd");
        }
    }
    /// <summary>重写行走低吼：硬直状态禁止播放走路音效，对齐原版行为</summary>
    protected override void UpdateWalkGrowl()
    {
        if (isDead || isStunned || agent == null || audioSource == null)
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
