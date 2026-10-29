using UnityEngine;
using UnityEngine.AI;
/// <summary>
/// All enemy health base class. Implements IEnemyHealth.
/// Common logic: health, damage, death, revive.
/// Subclasses override OnAfterTakeDamage / OnPostDeath / OnAfterRevive for unique behavior.
/// </summary>
public abstract class EnemyHealthBase : MonoBehaviour, IEnemyHealth
{
    [Header("Health Settings")]
    [SerializeField] public float maxHealth = 100f;
    [SerializeField] protected float destroyDelay = 5f;
    [SerializeField] protected Animator animator;
    protected float currentHealth;
    protected Vector3 lastHitPoint;
    protected bool _isDeadInvoked = false;
    protected EnemyAIBase enemyAI;

    // ==========方案B新增：实例死亡事件，仅当前对象死亡触发，非静态==========
    /// <summary>
    /// 当前敌人完整走完死亡流程后触发，只会执行一次
    /// </summary>
    public event System.Action OnEnemyDied;

    public virtual float currentHp
    {
        get => currentHealth;
        set => currentHealth = value;
    }
    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        enemyAI = GetComponent<EnemyAIBase>();
    }
    /// <summary>
    /// Damage entry. Template method: subtract health -> death check -> OnAfterTakeDamage.
    /// </summary>
    public virtual void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (isDead()) return;
        lastHitPoint = hitPoint;
        currentHealth = Mathf.Max(currentHealth - damage, 0f);
        if (currentHealth <= 0)
        {
            OnDeath();
            return;
        }
        OnAfterTakeDamage(hitPoint);
    }
    /// <summary>
    /// Death flow. Template method: play death anim -> disable collider -> notify AI -> disable agent -> OnPostDeath -> delay hide.
    /// </summary>
    protected virtual void OnDeath()
    {
        if (_isDeadInvoked) return;
        _isDeadInvoked = true;
        PlayDeathAnimation();
        if (TryGetComponent<Collider>(out var col))
            col.enabled = false;
        if (enemyAI != null)
        {
            enemyAI.Die();
        }
        if (TryGetComponent<NavMeshAgent>(out var agent))
        {
            agent.isStopped = true;
            agent.enabled = false;
        }
        OnPostDeath();

        // ==========新增：触发实例死亡事件==========
        OnEnemyDied?.Invoke();

        Invoke(nameof(SetInactiveAfterDeath), destroyDelay);
    }
    /// <summary>
    /// Default directional death animation (DieBack / DieFront).
    /// Subclasses (e.g. MonsterHealth) can override to use a different trigger.
    /// </summary>
    protected virtual void PlayDeathAnimation()
    {
        if (animator == null) return;
        Vector3 toHit = lastHitPoint - transform.position;
        toHit.y = 0;
        float dot = Vector3.Dot(transform.forward, toHit.normalized);
        if (dot > 0)
        {
            animator.SetTrigger("DieBack");
        }
        else
        {
            animator.SetTrigger("DieFront");
        }
    }
    /// <summary>
    /// Revive flow. Template method: cancel hide -> restore health -> enable collider -> revive AI -> enable agent -> reset animator -> OnAfterRevive.
    /// </summary>
    public virtual void Revive()
    {
        CancelInvoke(nameof(SetInactiveAfterDeath));
        _isDeadInvoked = false;
        currentHealth = maxHealth;
        if (TryGetComponent<Collider>(out var col))
            col.enabled = true;
        if (enemyAI != null)
        {
            enemyAI.ReviveAI();
        }
        if (TryGetComponent<NavMeshAgent>(out var agent))
        {
            agent.enabled = true;
            agent.isStopped = false;
        }
        if (animator != null)
        {
            animator.Rebind();
            animator.ResetTrigger("DieBack");
            animator.ResetTrigger("DieFront");
        }
        OnAfterRevive();
    }
    /// <summary>
    /// Called when health > 0 after taking damage.
    /// Override here for: stun, execution trigger, stagger accumulation, phase transition.
    /// </summary>
    protected virtual void OnAfterTakeDamage(Vector3 hitPoint) { }
    /// <summary>
    /// Called at the end of OnDeath.
    /// Override here for: loot spawn, disabling weapon models, special death effects.
    /// </summary>
    protected virtual void OnPostDeath() { }
    /// <summary>
    /// Called at the end of Revive.
    /// Override here for: resetting stun flags, phase state, stagger values.
    /// </summary>
    protected virtual void OnAfterRevive() { }
    protected virtual void SetInactiveAfterDeath()
    {
        if (_isDeadInvoked)
        {
            gameObject.SetActive(false);
        }
    }
    /// <summary>取消死亡后的延迟隐藏（CG演出期间调用，防止怪物中途消失）</summary>
    public void CancelDeathDispose()
    {
        CancelInvoke(nameof(SetInactiveAfterDeath));
    }
    public virtual bool isDead()
    {
        return currentHealth <= 0;
    }
}
