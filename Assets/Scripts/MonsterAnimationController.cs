using UnityEngine;

public class MonsterAnimationController : MonoBehaviour
{
    private Animator animator;
    private MonsterAI monsterAI; // 引用你的AI脚本
    private MonsterHealth monsterHealth;
    private AudioSource audioSource;
    [SerializeField] protected AudioClip[] roarClips;

    void Start()
    {
        animator = GetComponent<Animator>();
        monsterAI = GetComponent<MonsterAI>();
        monsterHealth = GetComponent<MonsterHealth>();
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (StartGameFlow.IsFightFrozen) return;
        // 同步AI的追击/奔跑状态到Animator
        animator.SetBool("IsChasing", monsterAI.isChasing);
        animator.SetBool("IsRunning", monsterAI.isRunning);
    }

    // 由AI调用：触发轻攻击
    public void TriggerLightAttack()
    {
        //Debug.Log("尝试轻攻击");
        animator.SetInteger("AttackType", 0);
        animator.SetTrigger("IsAttacking");
    }

    // 由AI调用：触发重击
    public void TriggerHeavyAttack()
    {
        animator.SetInteger("AttackType", 1);
        animator.SetTrigger("IsAttacking");
    }

    // 新增：用于外部（比如MutantHealth）触发硬直动画
    public void TriggerStagger()
    {
        //Debug.Log("触发硬直");
        animator.SetTrigger("IsHit");
        monsterAI.PauseMovement(); // 硬直时暂停AI移动
    }

    // 动画事件：硬直动画结束时调用
    public void OnStaggerAnimationEnd()
    {
        // 硬直结束后自动恢复追击，可在这里添加逻辑
        if (monsterAI != null)
        {
            monsterAI.ResumeChasing();
        }
    }

    // 动画事件：咆哮动画开始时调用，停止移动
    public void OnRoarAnimationStart()
    {
        // 转换阶段咆哮音效，与咆哮动画同步，从怪自身AudioSource发出
        if (audioSource != null && roarClips != null && roarClips.Length > 0)
        {
            AudioClip clip = roarClips[Random.Range(0, roarClips.Length)];
            audioSource.PlayOneShot(clip);
        }
        if (monsterHealth != null)
        {
            monsterHealth.LockHealth(true);
        }
        if (monsterAI != null)
        {
            monsterAI.PauseMovement();
        }
    }

    // 动画事件：咆哮动画结束时调用，重置IsPhase2
    public void OnRoarAnimationEnd()
    {
        if (monsterHealth != null)
        {
            monsterHealth.LockHealth(false);
        }
        animator.SetBool("IsPhase2", false);
        if (monsterAI != null)
        {
            monsterAI.ResumeChasing();
        }
    }

    // 动画事件：死亡动画结束时调用
    public void OnDeathAnimationEnd()
    {
        // 死亡后禁用组件
        if (monsterAI != null) monsterAI.enabled = false;
        this.enabled = false;
    }
}