using UnityEngine;
using UnityEngine.AI;
/// <summary>
/// All enemy AI base class.
/// Common logic: agent, player ref, attack cooldown, melee attack hit detection, audio, animation, patrol utilities.
/// Subclasses override UpdateAIBehavior for their own state machine / movement logic.
/// </summary>
public abstract class EnemyAIBase : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected AudioSource audioSource;
    protected NavMeshAgent agent;
    protected Transform playerTransform;

    [Header("Attack Settings")]
    [SerializeField] protected float attackRange = 1f;
    [SerializeField] protected float attackCooldown = 1.5f;
    [SerializeField] protected Transform attackPoint;
    [SerializeField] protected float attackSphere = 1.2f;
    [SerializeField] protected int attackDamage = 20;
    [SerializeField] protected LayerMask playerLayer;
    protected float attackCooldownTimer;

    [Header("Audio Clips")]
    [SerializeField] protected AudioClip[] alertRoars;
    [SerializeField] protected AudioClip[] attackGroans;
    [SerializeField] protected AudioClip[] walkClips;
    [SerializeField] protected AudioClip[] deathScreams;
    [SerializeField] protected float walkGrowlMinInterval = 2.5f;
    [SerializeField] protected float walkGrowlMaxInterval = 5.5f;
    protected float walkGrowlTimer;
    protected bool hasAlertPlayer = false;
    public bool HasAlertPlayer => hasAlertPlayer;

    [Header("Patrol Settings (only for enemies that patrol)")]
    [SerializeField] protected float patrolActivateRange = 12f;
    [SerializeField] protected float giveUpRange = 15f;
    [SerializeField] protected float patrolRadius = 6f;
    [SerializeField] protected float patrolWaitMin = 1.5f;
    [SerializeField] protected float patrolWaitMax = 3.5f;
    protected Vector3 patrolTarget;
    protected float patrolWaitTimer;
    protected bool hasPatrolTarget = false;

    [Header("Alert Propagate")]
    [Tooltip("自身发现玩家时，通知周围多少米内的敌人一起进入追击")]
    [SerializeField] protected float alertPropagateRadius = 10f;
    [Tooltip("触发警报时是否播放自身警告吼叫音效")]
    [SerializeField] protected bool playAlertRoarOnPropagate = true;

    [Header("CG设置")]
    [Tooltip("CG参演角色自动置true；true=CG冻结时不关闭Animator，交给Timeline驱动")]
    public bool ignoreCinematicAnimatorDisable;

    [Header("State")]
    public bool isDead = false;

    private bool _cachedAgentIsStopped;
    private bool _cachedAnimatorEnabled;
    private bool _hasCachedAnimatorState;
    private bool _prevFightFrozenState;

    protected virtual void Awake()
    {
        attackCooldownTimer = 0f;
        walkGrowlTimer = Random.Range(walkGrowlMinInterval, walkGrowlMaxInterval);
        if (animator == null)
            animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    protected virtual void Update()
    {
        if (isDead) return;
        bool fightFrozen = StartGameFlow.IsFightFrozen;

        // 仅当冻结状态发生变化时执行一次状态保存/切换
        if (fightFrozen != _prevFightFrozenState)
        {
            _prevFightFrozenState = fightFrozen;
            if (fightFrozen)
            {
                // ==========进入CG冻结==========
                if (agent != null)
                {
                    _cachedAgentIsStopped = agent.isStopped;
                    agent.isStopped = true;
                }

                // 只有非CG参演角色才操作Animator；参演角色跳过，交给Timeline
                if (animator != null && !ignoreCinematicAnimatorDisable)
                {
                    _cachedAnimatorEnabled = animator.enabled;
                    animator.enabled = false;
                    _hasCachedAnimatorState = true;
                }
                else
                {
                    _hasCachedAnimatorState = false;
                }
            }
            else
            {
                // ==========退出CG冻结，恢复之前状态==========
                if (agent != null)
                {
                    agent.isStopped = _cachedAgentIsStopped;
                }

                // 只有非CG参演角色且真正缓存过状态，才恢复Animator
                if (animator != null && !ignoreCinematicAnimatorDisable && _hasCachedAnimatorState)
                {
                    animator.enabled = _cachedAnimatorEnabled;
                }
            }
        }

        // 冻结：直接退出，不再执行AI逻辑、动画更新、低吼音效
        if (fightFrozen)
        {
            return;
        }

        // 正常运行逻辑
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            UpdateAIBehavior(distanceToPlayer);
        }
        UpdateAnimation();
        UpdateWalkGrowl();
    }

    /// <summary>
    /// Core behavior entry. Override this in subclasses to implement your own state machine / distance branches.
    /// Called every frame with the distance to the player.
    /// </summary>
    protected virtual void UpdateAIBehavior(float distanceToPlayer) { }

    /// <summary>
    /// 当本敌人检测到玩家，调用这个，通知周围敌人也收到警报
    /// 会设置自身 hasAlertPlayer=true，并且广播给范围内其他EnemyAIBase
    /// </summary>
    public virtual void TriggerAlertToNearbyEnemies()
    {
        if (isDead) return;

        // 自己先标记已经被警报
        hasAlertPlayer = true;

        if (playAlertRoarOnPropagate)
        {
            PlayRandomClip(alertRoars);
        }

        // 获取范围内所有碰撞体
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, alertPropagateRadius);
        foreach (var col in hitColliders)
        {
            EnemyAIBase otherEnemy = col.GetComponent<EnemyAIBase>();
            if (otherEnemy == null) continue;
            if (otherEnemy == this) continue; // 跳过自己
            if (otherEnemy.isDead) continue; // 死亡敌人忽略

            // 强制让别的敌人收到警报，不管它们有没有看见玩家
            otherEnemy.hasAlertPlayer = true;
        }
    }

    /// <summary>
    /// 清除警报状态，比如玩家脱离所有敌人之后调用
    /// </summary>
    public virtual void ClearAlertState()
    {
        hasAlertPlayer = false;
    }

    /// <summary>读档后重置AI为待机巡逻状态，清除战斗遗留的追击/警报状态</summary>
    public virtual void ResetToPatrolState()
    {
        hasAlertPlayer = false;
        if (agent != null)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
    }

    /// <summary>
    /// Default melee attack handling when player is within attackRange.
    /// Stops agent, zeroes move speed, triggers IsAttacking on cooldown, faces player.
    /// Call this from UpdateAIBehavior when distanceToPlayer <= attackRange.
    /// </summary>
    protected virtual void HandleMeleeAttackInRange()
    {
        if (agent != null)
            agent.isStopped = true;
        if (animator != null)
            animator.SetFloat("MoveSpeed", 0f);
        if (attackCooldownTimer <= 0)
        {
            if (animator != null)
                animator.SetTrigger("IsAttacking");
            attackCooldownTimer = attackCooldown;
        }
        FacePlayer();
    }

    /// <summary>
    /// Rotate to face the player on the XZ plane.
    /// </summary>
    protected virtual void FacePlayer()
    {
        if (playerTransform == null) return;
        Vector3 directionToPlayer = playerTransform.position - transform.position;
        directionToPlayer.y = 0f;
        if (directionToPlayer.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
        }
    }

    /// <summary>
    /// Default animation update: drives MoveSpeed from agent velocity.
    /// Override to empty if a separate animation controller handles this (e.g. MonsterAnimationController).
    /// </summary>
    protected virtual void UpdateAnimation()
    {
        if (isDead || animator == null || agent == null) return;
        float forwardSpeed = Vector3.Dot(transform.InverseTransformDirection(agent.velocity), Vector3.forward);
        animator.SetFloat("MoveSpeed", Mathf.Clamp01(forwardSpeed));
    }

    /// <summary>
    /// Walk growl timer. Plays a random walk clip while moving.
    /// </summary>
    protected virtual void UpdateWalkGrowl()
    {
        if (isDead || agent == null || audioSource == null) return;
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

    /// <summary>
    /// Called by EnemyHealthBase.OnDeath. Stops agent, plays death sound, resets triggers.
    /// Override OnDie() to clean up subclass-specific state.
    /// </summary>
    public virtual void Die()
    {
        if (isDead) return;
        isDead = true;
        PlayRandomClip(deathScreams);
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
        if (animator != null)
        {
            animator.ResetTrigger("IsAttacking");
        }
        OnDie();
    }

    /// <summary>
    /// Called by EnemyHealthBase.Revive. Restores agent and re-enables this component.
    /// </summary>
    public virtual void ReviveAI()
    {
        isDead = false;
        hasAlertPlayer = false; // 复活清除警报标记
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;
        }
        enabled = true;
    }

    /// <summary>
    /// Called at the end of Die(). Override to reset subclass-specific flags (stun, execution, etc).
    /// </summary>
    protected virtual void OnDie() { }

    /// <summary>
    /// Default melee hit detection via OverlapSphere at attackPoint.
    /// Called from animation events.
    /// </summary>
    public virtual void CheckAttackHit()
    {
        if (attackPoint == null) return;
        Collider[] hitColliders = Physics.OverlapSphere(attackPoint.position, attackSphere, playerLayer);
        foreach (var hit in hitColliders)
        {
            if (hit is CharacterController) continue;
            if (hit.TryGetComponent<PlayerHealth>(out PlayerHealth playerHealth))
            {
                playerHealth.TakeDamage(attackDamage, attackPoint.position);
            }
        }
    }

    /// <summary>
    /// Pick a random reachable NavMesh point within patrolRadius of current position.
    /// Utility for patrolling enemies; call from your UpdateAIBehavior.
    /// </summary>
    protected virtual void PickNewPatrolTarget()
    {
        Vector3 selectedPoint = transform.position;
        for (int i = 0; i < 5; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            Vector3 candidate = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                continue;
            }
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete)
            {
                selectedPoint = hit.position;
                break;
            }
        }
        patrolTarget = selectedPoint;
        hasPatrolTarget = true;
        patrolWaitTimer = Random.Range(patrolWaitMin, patrolWaitMax);
    }

    protected virtual void PlayRandomClip(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;
        AudioClip selected = clips[Random.Range(0, clips.Length)];
        audioSource.PlayOneShot(selected);
    }

    public virtual void PlayAttackSound()
    {
        if (isDead) return;
        PlayRandomClip(attackGroans);
    }
}
