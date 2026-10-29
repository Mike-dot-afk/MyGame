using UnityEngine;

public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("回旋踢设置")]
    public Animator playerAnimator;
    [Tooltip("回旋踢球形检测球心，拖拽场景中的游戏物体，例如脚部空点")]
    public Transform kickHitPoint;
    public string spinKickAnimTrigger = "SpinKick";
    public float kickRadius = 3f;
    public int kickDamage = 40;
    public LayerMask enemyLayer;

    [Header("动画分层设置")]
    public int spinKickLayerIndex = 7;
    public float layerBlendSpeed = 4f;

    private bool _spinKickIsActive;

    void Update()
    {
        float targetWeight = _spinKickIsActive ? 1f : 0f;
        float currentWeight = playerAnimator.GetLayerWeight(spinKickLayerIndex);
        float newWeight = Mathf.Lerp(currentWeight, targetWeight, Time.deltaTime * layerBlendSpeed);
        playerAnimator.SetLayerWeight(spinKickLayerIndex, newWeight);
    }

    /// <summary>
    /// 被僵尸交互调用，执行回旋踢（仅启动动画，伤害交给动画事件OnSpinKickHit）
    /// </summary>
    public void DoSpinKick()
    {
        if (_spinKickIsActive) return;
        _spinKickIsActive = true;
        playerAnimator.SetTrigger(spinKickAnimTrigger);
    }

    /// <summary>
    /// 【动画事件】踢腿命中帧调用此方法
    /// </summary>
    public void OnSpinKickHit()
    {
        if (kickHitPoint == null)
        {
            Debug.LogError("请给kickHitPoint赋值一个游戏物体作为踢判定球心！");
            return;
        }

        // 使用指定物体的世界坐标作为OverlapSphere球心
        Collider[] hits = Physics.OverlapSphere(kickHitPoint.position, kickRadius, enemyLayer);
        foreach (var col in hits)
        {
            if (col.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                damageable.TakeDamage(kickDamage, kickHitPoint.position);
            }
            if (col.TryGetComponent<IKnockbackReceiver>(out IKnockbackReceiver knockbackReceiver))
            {
                knockbackReceiver.ApplyKnockback(kickHitPoint.position);
            }
        }
    }

    /// <summary>
    /// 【动画事件】动画末尾帧调用，结束回旋踢，Update会平滑把层权重降回0
    /// </summary>
    public void OnSpinKickAnimationEnd()
    {
        _spinKickIsActive = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (kickHitPoint != null)
        {
            Gizmos.DrawWireSphere(kickHitPoint.position, kickRadius);
        }
    }
}
