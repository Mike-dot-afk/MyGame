using UnityEngine;

public class Knife : Weapon
{
    [Header("动画挥砍参数")]
    public Animator playerAnim;

    [Header("近战挥砍参数")]
    public float attackDamage = 30f;
    public float attackRange = 0.2f;
    public float attackCooldown = 1.2f;
    public LayerMask enemyLayer;
    public ParticleSystem slashVFX;
    public AudioClip slashSFX;

    [Header("弹反参数")]
    public float parryDuration = 0.3f;
    public float parryCooldown = 2f;
    public float parryStunDuration = 1.5f;
    public ParticleSystem parryVFX;
    public AudioClip parrySFX;

    [Header("连续攻击设置")]
    public float comboTimeout = 0.8f;      // 超过这个时间没点，断开连击

    [Header("挥刀动画层权重控制")]
    public int SlashLayerIndex = 6;
    public float slashLayerLerpSpeed = 13f;

    [Header("挥刀动画有效帧区间(0~1)")]

    private float currentSlashWeight;
    private float targetSlashWeight;
    private bool canAttack = true;
    private bool canParry = true;
    private AudioSource audioSource;

    // 内部状态 完全跟你原脚本一致
    private float attackTimer;
    private bool isAttacking;
    private float lastAttackTime;
    private bool inCombo;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        // 动画层权重平滑过渡
        currentSlashWeight = Mathf.Lerp(currentSlashWeight, targetSlashWeight, Time.deltaTime * slashLayerLerpSpeed);
        playerAnim.SetLayerWeight(SlashLayerIndex, currentSlashWeight);

        if (isAttacking)
        {
            attackTimer += Time.deltaTime;

            // 动画时长到了 → 解除攻击锁定
            if (attackTimer >= attackCooldown)
            {
                attackTimer = 0f;
                isAttacking = false;
                canAttack = true;
            }
        }

        inCombo = (Time.time - lastAttackTime) < comboTimeout;
        playerAnim.SetBool("InCombo", inCombo);
    }

    // 对外攻击入口 和原Attack()完全一致
    public override void Attack()
    {
        lastAttackTime = Time.time;  // 此处存疑：个人认为这一句反应玩家上一次按攻击键的时间，无论是否允许攻击都应计算
        if (!canAttack) return;

        canAttack = false;
        isAttacking = true;
        attackTimer = 0f;

        // 触发挥刀动画
        string currentAnimName = playerAnim.GetCurrentAnimatorStateInfo(SlashLayerIndex).IsName("Idle") ? "Idle" : "";
        // Debug.Log($"currentAnimName:{currentAnimName}");
        if (!inCombo || currentAnimName == "Idle")
        {
            playerAnim.SetTrigger("TriggerAttack");
        }
        currentSlashWeight = 1f;
        targetSlashWeight = 1f;
    }

    // 伤害检测 完全照搬你原来的
    public void OnSlashHit()
    {
        Collider[] hitColliders = Physics.OverlapSphere(firePoint.position, attackRange, enemyLayer);

        if (hitColliders.Length > 0)
        {
            if (slashVFX != null)
                Instantiate(slashVFX, firePoint.position, firePoint.rotation);

            if (slashSFX != null)
                audioSource.PlayOneShot(slashSFX);
        }

        foreach (Collider col in hitColliders)
        {
            if (col.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(attackDamage, firePoint.position);
            }
        }
    }

    // 弹反逻辑原样保留
    public void Parry()
    {
        if (!canParry) return;
        canParry = false;
        Invoke(nameof(ResetParry), parryCooldown);
    }

    public void OnParryActive() { }

    void OnParrySuccess()
    {
        if (parryVFX != null)
            Instantiate(parryVFX, firePoint.position, firePoint.rotation);
        if (parrySFX != null)
            audioSource.PlayOneShot(parrySFX);
    }

    void ResetParry()
    {
        canParry = true;
    }

    // 切武器 重置状态 原样照搬
    public void OnWeaponHolstered()
    {
        isAttacking = false;
        attackTimer = 0f;
        lastAttackTime = 0f;
        canAttack = true;
        currentSlashWeight = 0f;
        targetSlashWeight = 0f;
        if (playerAnim != null) playerAnim.SetLayerWeight(SlashLayerIndex, 0f);
        if (playerAnim != null) playerAnim.Play("Idle", SlashLayerIndex, 0f);
    }

    public void OnWeaponEquipped()
    {
        isAttacking = false;
        attackTimer = 0f;
        lastAttackTime = 0f;
        canAttack = true;
        currentSlashWeight = 0f;
        targetSlashWeight = 0f;
        if (playerAnim != null) playerAnim.SetLayerWeight(SlashLayerIndex, 0f);
        if (playerAnim != null) playerAnim.Play("Idle", SlashLayerIndex, 0f);
    }

    // 动画最后一帧事件调用
    public void SlashEnd()
    {
        if (inCombo) return;

        // 只设目标为0，Update自动平滑降回去
        targetSlashWeight = 0f;
    }
}