using UnityEngine;
using UnityEngine.AI;

public class MonsterAI : EnemyAIBase
{
    [Header("移动速度")]
    public float walkSpeed = 2f;
    public float runSpeed = 4f;

    [Header("攻击设置")]
    public float lightAttackRange = 1.5f;
    public float heavyAttackRange = 20f;
    public float leaveRangeBuffer = 1.5f;

    public Transform lightAttackPoint;
    public float lightAttackSphere = 1.2f;
    public float lightAttackDamage = 20f;

    public bool isChasing = false;
    public bool isRunning = false;
    private bool isInAttackRange = false;

    private MonsterAnimationController animController;
    private bool isPausedByAnimation = false;

    // 抛物线跳跃专用
    private bool isJumping = false;
    private Vector3 jumpStartPos;
    private Vector3 jumpTargetPos;
    private float jumpTotalTime = 1.5f;
    private float jumpCurrentTime = 0f;
    private Vector3 jumpDir;
    private Vector3 jumpPerpDir;
    private float jumpPerpendicularVelocity;
    private float gravity = -9.81f;
    private float jumpTotalDistance;

    protected override void Awake()
    {
        base.Awake();
        animController = GetComponent<MonsterAnimationController>();
        if (agent != null)
        {
            agent.autoBraking = false;
            agent.updateRotation = true;
        }
    }

    /// <summary>核心AI行为，每帧传入玩家距离；基类Update已经处理isDead、StartGameFlow.IsFightFrozen</summary>
    protected override void UpdateAIBehavior(float distanceToPlayer)
    {
        if (playerTransform == null) return;

        // 抛物线跳跃优先
        if (isJumping)
        {
            UpdateJump();
            return;
        }

        if (isPausedByAnimation)
        {
            agent.isStopped = true;
            return;
        }

        // 永远看向玩家
        Vector3 lookDir = playerTransform.position - transform.position;
        lookDir.y = 0;
        if (lookDir.magnitude > 0.1f)
            transform.rotation = Quaternion.LookRotation(lookDir);

        if (isInAttackRange)
        {
            if (distanceToPlayer > lightAttackRange + leaveRangeBuffer)
            {
                isInAttackRange = false;
                isChasing = true;
                agent.isStopped = false;
                ChasePlayer();
            }
            else
            {
                if (distanceToPlayer <= lightAttackRange)
                {
                    isChasing = false;
                    agent.isStopped = true;
                    TryAttack();
                }
                else
                {
                    isChasing = true;
                    agent.isStopped = false;
                    agent.speed = walkSpeed * 0.5f;
                    agent.SetDestination(playerTransform.position);
                }
            }
        }
        else
        {
            if (distanceToPlayer > heavyAttackRange)
            {
                isChasing = true;
                isRunning = true;
                agent.isStopped = false;
                ChasePlayer();
            }
            else if (distanceToPlayer > lightAttackRange)
            {
                isChasing = true;
                agent.isStopped = false;
                ChasePlayer();
                TryAttack();
            }
            else
            {
                isInAttackRange = true;
                isChasing = false;
                isRunning = false;
                agent.isStopped = true;
                TryAttack();
            }
        }
    }

    void ChasePlayer()
    {
        agent.SetDestination(playerTransform.position);
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        isRunning = distance > heavyAttackRange;
        agent.speed = isRunning ? runSpeed : walkSpeed;
    }

    void TryAttack()
    {
        MonsterHealth health = GetComponent<MonsterHealth>();
        bool isPhase2 = (health != null) ? health.IsInPhase2() : false;
        float distance = Vector3.Distance(transform.position, playerTransform.position);

        if (isPhase2 && distance <= heavyAttackRange && distance > lightAttackRange && CanPerformHeavyAttack())
        {
            animController.TriggerHeavyAttack();
        }
        else if (distance <= lightAttackRange)
        {
            animController.TriggerLightAttack();
        }
    }

    void UpdateJump()
    {
        jumpCurrentTime += Time.deltaTime;
        float t = Mathf.Clamp01(jumpCurrentTime / jumpTotalTime);
        jumpPerpendicularVelocity += gravity * Time.deltaTime;

        Vector3 moveStep = jumpDir * (jumpTotalDistance / jumpTotalTime) * Time.deltaTime
                         + jumpPerpDir * jumpPerpendicularVelocity * Time.deltaTime;
        transform.position += moveStep;

        if (t >= 1f)
        {
            transform.position = jumpTargetPos;
            isJumping = false;
            agent.Warp(jumpTargetPos);
            DealHeavyAttackDamage();
            Invoke(nameof(ResumeChasing), 2.3f);
        }
    }

    void DealHeavyAttackDamage()
    {
        float damageRadius = 3f;
        float damageAmount = 30f;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, damageRadius);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                if (hitCollider is CharacterController) continue;
                IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(damageAmount, transform.position);
                }
            }
        }
    }

    public void OnHeavyAttack()
    {
        Vector3 originalTarget = playerTransform.position - (playerTransform.position - transform.position).normalized * 0.5f;
        RaycastHit hit;
        if (Physics.Raycast(originalTarget + Vector3.up * 10f, Vector3.down, out hit, 20f))
        {
            jumpTargetPos = hit.point;
        }
        else
        {
            jumpTargetPos = originalTarget;
            jumpTargetPos.y = transform.position.y;
        }

        NavMeshHit navHit;
        NavMesh.SamplePosition(jumpTargetPos, out navHit, 2f, NavMesh.AllAreas);
        jumpTargetPos = navHit.position;

        jumpStartPos = transform.position;
        Vector3 delta = jumpTargetPos - jumpStartPos;
        jumpTotalDistance = delta.magnitude;

        if (jumpTotalDistance > 0.01f)
        {
            jumpDir = delta.normalized;
        }
        else
        {
            jumpDir = transform.forward;
        }

        jumpTotalTime = jumpTotalDistance / 8f;
        jumpPerpDir = GetPerpendicularUp(jumpDir);
        jumpPerpendicularVelocity = -0.5f * gravity * jumpTotalTime;

        isJumping = true;
        jumpCurrentTime = 0f;
        agent.isStopped = true;
        isPausedByAnimation = true;
        agent.velocity = Vector3.zero;
    }

    Vector3 GetPerpendicularUp(Vector3 direction)
    {
        if (Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.99f)
        {
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            return Vector3.Cross(direction, right).normalized;
        }
        else
        {
            Vector3 right = Vector3.Cross(Vector3.right, direction).normalized;
            return Vector3.Cross(direction, right).normalized;
        }
    }

    bool CanPerformHeavyAttack()
    {
        Vector3 originalTarget = playerTransform.position - (playerTransform.position - transform.position).normalized * 0.5f;
        RaycastHit hit;
        Vector3 targetPos;
        if (Physics.Raycast(originalTarget + Vector3.up * 10f, Vector3.down, out hit, 20f))
        {
            targetPos = hit.point;
        }
        else
        {
            targetPos = originalTarget;
            targetPos.y = transform.position.y;
        }

        NavMeshHit navHit;
        if (!NavMesh.SamplePosition(targetPos, out navHit, 2f, NavMesh.AllAreas))
        {
            return false;
        }
        return true;
    }

    public void CheckLightAttackHit()
    {
        Collider[] hitColliders = Physics.OverlapSphere(lightAttackPoint.position, lightAttackSphere, playerLayer);
        foreach (var hit in hitColliders)
        {
            if (hit is CharacterController) continue;
            if (hit.TryGetComponent<PlayerHealth>(out PlayerHealth playerHealth))
            {
                playerHealth.TakeDamage(lightAttackDamage, lightAttackPoint.position);
            }
        }
    }

    public void PauseMovement()
    {
        agent.isStopped = true;
        isPausedByAnimation = true;
    }

    public void ResumeChasing()
    {
        agent.isStopped = false;
        isPausedByAnimation = false;
    }

    /// <summary>死亡回调：清理跳跃延时、跳跃状态</summary>
    protected override void OnDie()
    {
        base.OnDie();
        CancelInvoke(nameof(ResumeChasing));
        isJumping = false;
        isPausedByAnimation = false;
    }

    // 重写动画更新：怪物使用MonsterAnimationController，禁用基类MoveSpeed逻辑
    protected override void UpdateAnimation()
    {
        // 空实现，动画完全交给MonsterAnimationController
    }

    // 怪物不使用基类行走低吼，覆盖为空
    protected override void UpdateWalkGrowl()
    {

    }
}
