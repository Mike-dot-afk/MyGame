using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public interface IKnockbackReceiver
{
    void ApplyKnockback(Vector3 hitFrom);
}
public class ZombieControl : MonoBehaviour, IKnockbackReceiver
{
    private Animator animator;
    private NavMeshAgent agent;
    private Transform playerTransform;
    private RagdollHelper _ragdollHelper;
    [Header("追击设置")]
    public float chaseRange = 8f;
    public float attackRange = 1f;
    [Header("攻击冷却")]
    public float attackCooldown = 1.5f;
    private float attackCooldownTimer;
    [Header("僵尸攻击检测")]
    public Transform attackPoint;
    public float attackSphere = 1.2f;
    public int attackDamage = 20;
    public LayerMask playerLayer;
    [Header("普通受击硬直")]
    public bool isStunned = false;
    public float currentStunDuration = 0f;
    public float defaultStunTime = 1.2f;
    public bool canBeStun = true;
    [Header("韧性系统")]
    public float currentStunResist = 0f;
    public float maxStunResist = 1.0f;
    public float resistDecaySpeed = 0.3f;
    public float minStunTime = 0.25f;
    [Header("处决大硬直")]
    public bool isExecutionStun = false;
    public float executionStunTime = 2.5f;
    public float currentExecutionTimer = 0f;
    public bool hasTriggeredExecution = false;
    [Header("【全程布娃娃击飞】回旋踢参数")]
    public float knockBackForce = 5.5f;
    public float knockUpForce = 2.0f;
    public float ragdollTotalTime = 0.75f;
    private bool _isInRagdollState;
    [Header("Animator起身状态名称")]
    public string faceUpGetUpStateName = "FaceUpGetUpState";
    public string faceDownGetUpStateName = "FaceDownGetUpState";

    private bool _isGettingUp;
    private bool _pendingStartGetUp;
    private string _pendingGetUpState;
    private bool _animJustEnabled;

    //==== 固定根偏移 ====
    private bool _isApplyingRootOffset;
    private Vector3 _rootPosOffset;
    private Quaternion _rootRotOffset;
    private Vector3 _ragdollEndHipsPos;
    private Quaternion _ragdollEndHipsRot;

    private bool _faceUp;

    void Awake()
    {
        attackCooldownTimer = 0f;
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        _ragdollHelper = GetComponent<RagdollHelper>();
        if (animator != null)
        {
            animator.writeDefaultValuesOnDisable = false;
            animator.keepAnimatorStateOnDisable = true;
        }
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    void Update()
    {
        if (StartGameFlow.IsFightFrozen) return;
        if (_isInRagdollState) return;
        if (_isGettingUp) return;
        if (isExecutionStun)
        {
            currentExecutionTimer -= Time.deltaTime;
            agent.isStopped = true;
            if (currentExecutionTimer <= 0) ExitExecutionStun();
            return;
        }
        if (isStunned)
        {
            currentStunDuration -= Time.deltaTime;
            if (currentStunDuration <= 0) ExitStun();
            else { agent.isStopped = true; return; }
        }
        if (currentStunResist > 0)
            currentStunResist = Mathf.Max(0, currentStunResist - resistDecaySpeed * Time.deltaTime);
        if (attackCooldownTimer > 0) attackCooldownTimer -= Time.deltaTime;
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= attackRange)
            {
                agent.isStopped = true;
                animator.SetFloat("MoveSpeed", 0f);
                if (attackCooldownTimer <= 0 && !_isInRagdollState)
                {
                    animator.SetTrigger("IsAttacking");
                    attackCooldownTimer = attackCooldown;
                }
                Vector3 directionToPlayer = playerTransform.position - transform.position;
                directionToPlayer.y = 0f;
                if (directionToPlayer.magnitude > 0.1f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                    transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
                }
            }
            else if (distanceToPlayer <= chaseRange)
            {
                agent.isStopped = false;
                agent.SetDestination(playerTransform.position);
                animator.ResetTrigger("IsAttacking");
            }
            else
            {
                agent.isStopped = true;
                animator.SetFloat("MoveSpeed", 0f);
                animator.ResetTrigger("IsAttacking");
            }
        }
        UpdateAnimation();
    }

    void LateUpdate()
    {
        if (_isApplyingRootOffset)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                hips.position += _rootPosOffset;
                hips.rotation = _rootRotOffset * hips.rotation;
            }
        }
        if (_pendingStartGetUp)
        {
            _pendingStartGetUp = false;
            animator.enabled = true;
            animator.ResetTrigger("GetStun");
            animator.ResetTrigger("StunEnd");
            animator.ResetTrigger("IsAttacking");
            animator.Play(_pendingGetUpState, 0, 0f);
            animator.Update(0f);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                _rootPosOffset = _ragdollEndHipsPos - hips.position;
                _rootRotOffset = _ragdollEndHipsRot * Quaternion.Inverse(hips.rotation);
                hips.position += _rootPosOffset;
                hips.rotation = _rootRotOffset * hips.rotation;
                _isApplyingRootOffset = true;
            }
            AnimatorStateInfo curState = animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"LateUpdate Play之后 IsName={curState.IsName(_pendingGetUpState)}");
        }
    }

    void UpdateAnimation()
    {
        float forwardSpeed = Vector3.Dot(transform.InverseTransformDirection(agent.velocity), Vector3.forward);
        animator.SetFloat("MoveSpeed", Mathf.Clamp01(forwardSpeed));
    }

    public void CheckAttackHit()
    {
        Collider[] hitColliders = Physics.OverlapSphere(attackPoint.position, attackSphere, playerLayer);
        foreach (var hit in hitColliders)
        {
            if (hit is CharacterController) continue;
            if (hit.TryGetComponent<PlayerHealth>(out PlayerHealth playerHealth))
                playerHealth.TakeDamage(attackDamage, attackPoint.position);
        }
    }

    #region 普通受击硬直
    public void SetStun(float stunTime = 0)
    {
        if (_isGettingUp) return;
        if (!canBeStun) return;
        if (isStunned) return;
        isStunned = true;
        float rawTime = stunTime <= 0 ? defaultStunTime : stunTime;
        float finalStun = Mathf.Max(minStunTime, rawTime - currentStunResist);
        currentStunDuration = finalStun;
        currentStunResist = Mathf.Min(currentStunResist + 0.3f, maxStunResist);
        agent.isStopped = true;
        animator.ResetTrigger("IsAttacking");
        animator.SetTrigger("GetStun");
    }
    public void ExitStun()
    {
        isStunned = false;
        currentStunDuration = 0;
        animator.SetTrigger("StunEnd");
    }
    #endregion

    #region 处决大硬直
    public void EnterExecutionStun()
    {
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
        isExecutionStun = false;
        currentExecutionTimer = 0;
        animator.SetTrigger("StunEnd");
    }
    #endregion

    #region 布娃娃击飞
    public void ApplyKnockback(Vector3 hitFrom)
    {
        if (_ragdollHelper == null) return;
        if (_isInRagdollState) return;
        _isInRagdollState = true;
        isExecutionStun = false;
        currentExecutionTimer = 0;
        animator.ResetTrigger("IsAttacking");
        animator.ResetTrigger("GetStun");
        animator.ResetTrigger("StunEnd");
        Vector3 dir = (transform.position - hitFrom).normalized;
        Vector3 impulse = dir * knockBackForce;
        impulse.y = knockUpForce;
        agent.enabled = false;
        _ragdollHelper.SetRagdollActive(true, impulse);
        Invoke(nameof(RecoverFromRagdoll), ragdollTotalTime);
    }

    void RecoverFromRagdoll()
    {
        if (_ragdollHelper == null)
        {
            Debug.LogError("RecoverFromRagdoll：_ragdollHelper == null");
            return;
        }
        Debug.Log("进入 RecoverFromRagdoll，准备关闭布娃娃");
        _ragdollHelper.SetRagdollActive(false);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips != null)
        {
            // 1. 记录骨盆当前世界坐标
            Vector3 hipsWorldPos = hips.position;
            Quaternion hipsWorldRot = hips.rotation;

            // 2. 采样导航点
            Vector3 finalPos = hipsWorldPos;
            bool isOnNavMesh = NavMesh.SamplePosition(hipsWorldPos, out NavMeshHit hit, 0.05f, NavMesh.AllAreas);
            if (!isOnNavMesh)
            {
                if (NavMesh.SamplePosition(hipsWorldPos, out NavMeshHit bigHit, 2f, NavMesh.AllAreas))
                    finalPos = bigHit.position;
            }

            // 3. 父物体旋转(只Y轴) + 位置
            transform.rotation = Quaternion.Euler(0, hipsWorldRot.eulerAngles.y, 0);
            transform.position = finalPos;

            // 4. 骨盆负位移补偿，模型视觉不动
            hips.position = hipsWorldPos;
            hips.rotation = hipsWorldRot;

            // 5. X轴立即转正：骨盆up对齐到正上/正下（一帧完成，无穿地）
            _faceUp = hips.up.y > 0;
            Vector3 targetUp = _faceUp ? Vector3.up : Vector3.down;
            Vector3 forward = hips.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();
            Quaternion targetRot = Quaternion.LookRotation(forward, targetUp);
            hips.rotation = targetRot;

            // 6. 记录X轴转正后的骨盆姿态
            _ragdollEndHipsPos = hips.position;
            _ragdollEndHipsRot = hips.rotation;

            Debug.Log($"RecoverFromRagdoll完成 faceUp={_faceUp} 父物体={transform.position} 骨盆={hips.position}");
        }
        else
        {
            Debug.LogError("找不到Hips骨盆骨骼！");
        }
        StartCoroutine(DelayedPlayGetUp());
    }

    private IEnumerator DelayedPlayGetUp()
    {
        // 恢复原协程逻辑：先等3帧物理安定，再开agent
        yield return null;
        yield return null;
        yield return null;
        _isInRagdollState = false;
        _isGettingUp = true;
        Debug.Log($"协程，根物体已经就位 transform.position={transform.position}");

        // 消耗agent内部旧nextPosition缓存，防止回弹
        yield return null;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.nextPosition = transform.position;
        agent.enabled = true;
        agent.updatePosition = true;
        agent.updateRotation = true;
        Debug.Log($"协程内开启agent transform={transform.position} nextPosition={agent.nextPosition}");

        _pendingGetUpState = _faceUp ? faceUpGetUpStateName : faceDownGetUpStateName;
        Debug.Log($"准备播放状态：{_pendingGetUpState}");
        _pendingStartGetUp = true;
    }

    public void OnGetUpAnimationFinished()
    {
        _isGettingUp = false;
        _isApplyingRootOffset = false;
        // Y/Z修正（原有代码不变）
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips != null)
        {
            Quaternion childWorldRot = hips.rotation;
            Quaternion fixedLocal = Quaternion.Euler(0f, -90f, -90f);
            Quaternion parentNewRot = childWorldRot * Quaternion.Inverse(fixedLocal);
            transform.rotation = parentNewRot;
            hips.localRotation = fixedLocal;
        }
    }
    #endregion

    public void OnZombieDeath()
    {
        CancelInvoke(nameof(RecoverFromRagdoll));
        StopCoroutine(nameof(DelayedPlayGetUp));
        _isInRagdollState = false;
        _isGettingUp = false;
        _pendingStartGetUp = false;
        _animJustEnabled = false;
        _isApplyingRootOffset = false;
        if (_ragdollHelper != null)
            _ragdollHelper.SetRagdollActive(false);
    }

#if UNITY_EDITOR
    [ContextMenu("测试直接触发布娃娃击飞")]
    public void TestKnockBack()
    {
        ApplyKnockback(transform.position - Vector3.right);
    }
#endif
}
