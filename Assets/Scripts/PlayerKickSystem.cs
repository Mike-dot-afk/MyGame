using UnityEngine;
using ArionDigital;
using StarterAssets;

public class PlayerKickSystem : MonoBehaviour
{
    [Header("踢动画层索引")]
    public int KickLayerIndex = 5;

    [Header("平滑过渡设置")]
    public float layerLerpSpeed = 13f;   // 越大过渡越快


    [Header("相机根节点（拖入 PlayerCameraRoot）")]
    public Transform playerCameraRoot; // 拖你的相机根

    public Animator playerAnimator;
    public CrashCrate currentTargetCrate;
    public ThirdPersonController thirdPersonController;
    public ThirdPersonShooterController thirdPersonShooterController;

    // 平滑插值变量（新增）
    private float currentLayerWeight;
    private float targetLayerWeight;
    private Quaternion _cachedCameraRootRot; // 缓存相机根旋转

    private void Update()
    {
        // 每帧平滑 Lerp
        currentLayerWeight = Mathf.Lerp(currentLayerWeight, targetLayerWeight, Time.deltaTime * layerLerpSpeed);
        playerAnimator.SetLayerWeight(KickLayerIndex, currentLayerWeight);
        // 动画播放期间，强制保持相机根不动
        if (targetLayerWeight > 0.5f && playerCameraRoot != null)
        {
            playerCameraRoot.rotation = _cachedCameraRootRot;
        }
    }

    public void OnInteractPress()
    {
        if (currentTargetCrate == null || currentTargetCrate.isBroken)
            return;
        if (thirdPersonController != null)
            thirdPersonController.enabled = false;
        if (thirdPersonShooterController != null)
            thirdPersonShooterController.enabled = false;
        // 按下时先保存相机根的旋转
        if (playerCameraRoot != null)
        {
            _cachedCameraRootRot = playerCameraRoot.rotation;
        }
        Vector3 targetPos = currentTargetCrate.transform.position;
        Vector3 lookDir = targetPos - transform.position;
        lookDir.y = 0f;
        if (lookDir.magnitude > 0.1f)
        {
            // 角色相对于相机朝向目标
            float targetAngle = Mathf.Atan2(lookDir.x, lookDir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, targetAngle, 0);
        }

        playerAnimator.SetTrigger("TriggerKick");
        currentLayerWeight = 1f;
        targetLayerWeight = 1f;
    }

    // 动画事件调用：函数名必须是 OnPlayerKickCrate
    public void OnPlayerKickCrate()
    {
        if (currentTargetCrate == null || currentTargetCrate.isBroken)
            return;

        currentTargetCrate.KickByPlayer();
    }

    public void OnKickAnimationEnd()
    {
        // 只改目标，不再直接SetLayerWeight
        targetLayerWeight = 0f;
        if (thirdPersonController != null)
            thirdPersonController.enabled = true;
        if (thirdPersonShooterController != null)
            thirdPersonShooterController.enabled = true;
    }
}