using StarterAssets;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

// 本脚本是 ThirdPersonShooterController 的副本（树驱动版），仅供 RobotKyle (1) 副本试验使用。
// 原版 ThirdPersonShooterController.cs 未做任何修改。
// treeDriven = false 时行为与原脚本完全一致；treeDriven = true 时：
//   瞄准输入决策仍由本脚本处理（持续状态，脚本管更顺）；
//   射击 / 换弹 / 手雷 的决策改由行为树接管，本脚本只提供 public 动作方法。
public class ThirdPersonShooterController_Tree : MonoBehaviour
{
    [Header("行为树驱动模式")]
    [Tooltip("true：射击/换弹/手雷由行为树决策；false：行为与原脚本完全一致")]
    public bool treeDriven;

    [Header("瞄准相关")]
    [SerializeField] private CinemachineCamera aimVirtualCamera;
    [SerializeField] private float normalSensitivity;
    [SerializeField] private float aimSensitivity;
    [SerializeField] private LayerMask aimColliderLayerMask;
    [SerializeField] private Transform debugTransform;

    [Header("动画层IK")]
    [SerializeField] private int holdLayerIndex = 1;
    [SerializeField] private int aimingLayerIndex = 2;
    [SerializeField] private int reloadLayerIndex = 3;
    [SerializeField] private int throwLayerIndex = 4;
    [SerializeField] private float reloadSmoothSpeed = 13f;

    [Header("准星设置")]
    [SerializeField] private GameObject crosshair;

    [Header("手雷设置")]
    public GameObject grenadePrefab;
    public Transform grenadeHandHolder;
    public float throwForce = 10f;
    public float holdAtNormalizedTime = 0.329f;

    [Header("手雷轨迹预演")]
    public LineRenderer grenadeTrajectoryLine;
    [Tooltip("轨迹点数量")]
    public int trajectoryPointCount = 30;
    private Vector3[] _trajectoryPoints;
    private Rigidbody _cachedGrenadeRb;

    [Header("编辑模式")]
    [SerializeField] private bool isEditMode;

    [Header("落地缓冲相关")]
    public float landAnimationBufferTime = 0.25f;
    private float _landBufferTimer;
    private bool _prevPhysicsGrounded;

    private ThirdPersonController thirdPersonController;
    private StarterAssetsInputs starterAssetsInputs;
    private Animator animator;
    private CinemachineBasicMultiChannelPerlin recoilNoise;
    private float recoilTimer;
    private float holdWeight;
    private float aimWeight;
    private float reloadWeight;
    private float currentRecoilDuration;
    private bool isAimingState = false;
    private bool isHoldingGrenade;
    private bool isThrowCharging;
    private GameObject currentGrenade;
    private WeaponManager weaponManager;
    private Weapon currentWeapon => weaponManager.currentWeapon;
    private GunWeapon currentGun => currentWeapon as GunWeapon;
    public Knife currentKnife => currentWeapon as Knife;
    private Weapon _lastWeapon;
    public static ThirdPersonShooterController_Tree Instance;
    private readonly Vector3 THROW_POINT_RELATIVE_TO_ROOT = new Vector3(0.394f, 1.904f, -0.089f);
    private bool animGrounded;

    private void Awake()
    {
        Instance = this;
        thirdPersonController = GetComponent<ThirdPersonController>();
        starterAssetsInputs = GetComponent<StarterAssetsInputs>();
        animator = GetComponent<Animator>();
        weaponManager = GetComponent<WeaponManager>();
        if (aimVirtualCamera != null)
            aimVirtualCamera.TryGetComponent(out recoilNoise);
        _trajectoryPoints = new Vector3[trajectoryPointCount];
        if (grenadeTrajectoryLine != null)
        {
            grenadeTrajectoryLine.positionCount = trajectoryPointCount;
            grenadeTrajectoryLine.enabled = false;
        }
        // 缓存手雷预制体刚体，用于开场前预测轨迹速度
        if (grenadePrefab != null)
        {
            _cachedGrenadeRb = grenadePrefab.GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        if (_lastWeapon != currentWeapon)
        {
            if (_lastWeapon is GunWeapon oldGun)
            {
                Rig oldHold = oldGun.GetHoldRig();
                Rig oldAim = oldGun.GetAimRig();
                if (oldHold != null) oldHold.weight = 0f;
                if (oldAim != null) oldAim.weight = 0f;
            }
            _lastWeapon = currentWeapon;
            aimWeight = 0f;
        }
        bool physicsGrounded = thirdPersonController.Grounded;
        // 检测：刚从空中落地（刚刚接触地面）
        if (physicsGrounded && !_prevPhysicsGrounded)
        {
            _landBufferTimer = landAnimationBufferTime;
        }
        // 只要落地缓冲时间>0，就在时间内禁止直接瞄准
        if (_landBufferTimer > 0)
        {
            _landBufferTimer -= Time.deltaTime;
        }
        if (!physicsGrounded)
        {
            _landBufferTimer = 0f;
        }
        // 落地缓冲IK权重逻辑（悬空时 也已经压缩处理过）
        animGrounded = physicsGrounded && _landBufferTimer <= 0f;
        _prevPhysicsGrounded = physicsGrounded;
        float ikDampFactor = animGrounded ? 1f : 0.25f;
        float targetHoldWeight = 0f;
        if (!isHoldingGrenade && currentGun != null && currentGun is not Pistol)
        {
            targetHoldWeight = 1f * ikDampFactor;
        }
        holdWeight = Mathf.Lerp(holdWeight, targetHoldWeight, Time.deltaTime * reloadSmoothSpeed);
        animator.SetLayerWeight(holdLayerIndex, holdWeight);
        if (!isHoldingGrenade && currentGun != null && currentGun is not Pistol)
        {
            TwoBoneIKConstraint leftHandIk = currentGun.leftHandIK;
            if (leftHandIk != null)
            {
                float targetLeftIkWeight = currentGun.isReloading ? 0f : 1f;
                leftHandIk.weight = Mathf.Lerp(
                    leftHandIk.weight,
                    targetLeftIkWeight,
                    Time.deltaTime * reloadSmoothSpeed
                );
            }
        }
        if (isEditMode)
        {
            aimWeight = 1f * ikDampFactor;
            animator.SetLayerWeight(aimingLayerIndex, 1f * ikDampFactor);
            if (currentGun != null)
            {
                Rig holdRig = currentGun.GetHoldRig();
                Rig aimRig = currentGun.GetAimRig();
                if (holdRig != null) holdRig.weight = 0f;
                if (aimRig != null) aimRig.weight = 1f * ikDampFactor;
            }
            UpdateWeaponPose();
            return;
        }
        UpdateAimRay();
        if (treeDriven)
        {
            // 行为树模式：瞄准决策由脚本处理，射击/换弹/手雷决策由树接管
            HandleAimDecision();
            EnsureAimState();
            HandleAimExecution();
            UpdateWeaponPose();
            UpdateRecoil();
            HandleReloadExecution();
            HandleGrenadeChargeExecution();
        }
        else
        {
            // 非树模式：与原脚本行为完全一致
            HandleAim();
            HandleShoot();
            UpdateWeaponPose();
            UpdateRecoil();
            HandleReload();
            // 第 6 键手雷
            if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                TryStartHoldGrenade();
            }
            // 按住右键开始蓄力
            if (isHoldingGrenade && Input.GetMouseButtonDown(1))
            {
                StartCharge();
            }
            if (isThrowCharging)
            {
                HandleGrenadeChargeExecution();
                if (Input.GetMouseButtonDown(0))
                {
                    CancelGrenadeThrow();
                }
            }
            // 松开右键掷出手雷
            if (isThrowCharging && Input.GetMouseButtonUp(1))
            {
                LockForThrow();
                StartCoroutine(DoThrowGrenadeAfterAnimation());
            }
        }
    }

    private Ray aimRay;
    public Vector3 mouseWorldPos;

    private void UpdateAimRay()
    {
        Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);
        aimRay = Camera.main.ScreenPointToRay(center);
        if (Physics.Raycast(aimRay, out RaycastHit hit, 999f, aimColliderLayerMask))
        {
            if (debugTransform != null) debugTransform.position = hit.point;
            mouseWorldPos = hit.point;
        }
        else
        {
            mouseWorldPos = aimRay.GetPoint(100f);
        }
    }

    // ==================== 行为树调用的 public 接口 ====================

    /// <summary>当前是否持枪（GunWeapon 及其子类）</summary>
    public bool HasGun() => currentWeapon is GunWeapon;

    /// <summary>当前是否持刀（Knife）</summary>
    public bool HasKnife() => currentWeapon is Knife;

    /// <summary>射击输入是否按下（不消费，供近战条件使用）</summary>
    public bool IsShootInputPressed() => starterAssetsInputs.shoot;

    /// <summary>当前是否处于瞄准状态</summary>
    public bool IsAiming() => isAimingState;

    /// <summary>当前是否处于手雷蓄力中</summary>
    public bool IsThrowCharging() => isThrowCharging;

    /// <summary>当前是否正在换弹</summary>
    public bool IsReloading() => currentGun != null && currentGun.isReloading;

    /// <summary>行为树射击入口：内部做全部校验并消费 shoot 输入（与原 HandleShoot 一致）</summary>
    public void FireWeapon()
    {
        if (isHoldingGrenade) return;
        bool wantShoot = starterAssetsInputs.shoot;
        starterAssetsInputs.shoot = false;
        if (!wantShoot || currentWeapon == null) return;
        if (currentKnife != null)
        {
            currentKnife.Attack();
            return;
        }
        if (currentGun == null) return;
        if (!starterAssetsInputs.aim) return;
        if (currentGun.isReloading) return;
        if (!currentGun.CanFire()) return;
        currentGun.Attack();
        ApplyRecoil(currentGun.recoilAmplitude, currentGun.recoilDuration);
    }

    /// <summary>行为树射击前置条件（不消费输入）</summary>
    public bool CanFireNow()
    {
        if (!starterAssetsInputs.shoot) return false;
        if (currentGun == null) return false;
        if (!starterAssetsInputs.aim) return false;
        if (currentGun.isReloading) return false;
        return currentGun.CanFire();
    }

    /// <summary>行为树换弹入口：与原 HandleReload 的 R 键块一致</summary>
    public void StartReload()
    {
        if (currentGun == null) return;
        if (!currentGun.isReloading && currentGun.CanReload())
        {
            currentGun.Reload();
            animator.Play("Reload", reloadLayerIndex, 0f);
        }
    }

    /// <summary>行为树换弹前置条件</summary>
    public bool CanReloadNow()
    {
        if (currentGun == null) return false;
        return currentGun.CanReload();
    }

    /// <summary>行为树消耗一次 shoot 输入（近战点击用）</summary>
    public void ConsumeShootInput()
    {
        starterAssetsInputs.shoot = false;
    }

    /// <summary>行为树蓄力入口：与原 RMB 按下块一致</summary>
    public void StartCharge()
    {
        animator.SetLayerWeight(throwLayerIndex, 1f);
        isThrowCharging = true;
        animator.SetBool("ThrowHold", true);
        // 显示轨迹线
        if (grenadeTrajectoryLine != null)
            grenadeTrajectoryLine.enabled = true;
        thirdPersonController.SetRotateOnMove(false);
        if (aimVirtualCamera != null)
            aimVirtualCamera.gameObject.SetActive(true);
        starterAssetsInputs.sprint = false;
        starterAssetsInputs.jump = false;
    }

    /// <summary>行为树投掷锁定：与原 RMB 松开块一致（不启动协程，时序由树控制）</summary>
    public void LockForThrow()
    {
        thirdPersonController.lockPlayerControl = true;
        isThrowCharging = false;
        thirdPersonController.SetRotateOnMove(true);
        if (aimVirtualCamera != null)
            aimVirtualCamera.gameObject.SetActive(false);
        if (grenadeTrajectoryLine != null)
            grenadeTrajectoryLine.enabled = false;
    }

    /// <summary>行为树抛出+解锁：等价于原协程前半段（Wait 0.75s 之后）</summary>
    public void FinishThrow()
    {
        ThrowGrenade();
        animator.SetBool("ThrowHold", false);
        thirdPersonController.lockPlayerControl = false;
    }

    /// <summary>行为树投掷清理：等价于原协程后半段（再 Wait 1.25s 之后）</summary>
    public void FinishThrowCleanup()
    {
        animator.SetLayerWeight(throwLayerIndex, 0f);
    }

    // ==================== 瞄准（树模式用：决策 + 执行） ====================

    private void HandleAimDecision()
    {
        if (isThrowCharging) return;
        bool isAimingInput = starterAssetsInputs.aim;
        bool isReloading = currentGun != null && currentGun.isReloading;
        bool wantAim = isAimingInput && !isReloading && currentGun != null;
        if (wantAim != isAimingState)
        {
            SetAiming(wantAim);
        }
    }

    // 每帧校正瞄准相关状态，与原脚本 HandleAim 每帧执行的行为对齐：
    // 未瞄准时确保瞄准相机/准星关闭、灵敏度为 normal；瞄准时确保开启、灵敏度为 aim。
    // 只在值不一致时才设置，避免每帧重复调用。
    private void EnsureAimState()
    {
        if (isThrowCharging) return;
        if (isAimingState)
        {
            if (aimVirtualCamera != null && !aimVirtualCamera.gameObject.activeSelf)
                aimVirtualCamera.gameObject.SetActive(true);
            if (crosshair != null && !crosshair.activeSelf)
                crosshair.SetActive(true);
            if (thirdPersonController.Sensitivity != aimSensitivity)
                thirdPersonController.SetSensitivity(aimSensitivity);
        }
        else
        {
            if (aimVirtualCamera != null && aimVirtualCamera.gameObject.activeSelf)
                aimVirtualCamera.gameObject.SetActive(false);
            if (crosshair != null && crosshair.activeSelf)
                crosshair.SetActive(false);
            if (thirdPersonController.Sensitivity != normalSensitivity)
                thirdPersonController.SetSensitivity(normalSensitivity);
        }
    }

    /// <summary>切换瞄准的副作用（相机/灵敏度/旋转/标志/冲刺复位），树与脚本共用</summary>
    public void SetAiming(bool value)
    {
        bool changed = value != isAimingState;
        isAimingState = value;
        if (!changed) return;
        if (crosshair != null)
        {
            crosshair.SetActive(value);
        }
        if (value)
        {
            aimVirtualCamera.gameObject.SetActive(true);
            thirdPersonController.SetSensitivity(aimSensitivity);
            thirdPersonController.SetRotateOnMove(false);
            thirdPersonController.isAiming = true;
            starterAssetsInputs.sprint = false;
            starterAssetsInputs.jump = false;
        }
        else
        {
            aimVirtualCamera.gameObject.SetActive(false);
            thirdPersonController.SetSensitivity(normalSensitivity);
            thirdPersonController.SetRotateOnMove(true);
            thirdPersonController.isAiming = false;
            if (Keyboard.current != null)
            {
                starterAssetsInputs.sprint = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            }
        }
    }

    private void HandleAimExecution()
    {
        float ikDampFactor = animGrounded ? 1f : 0.25f;
        if (!isHoldingGrenade)
        {
            aimWeight = Mathf.Lerp(aimWeight, isAimingState ? 1f * ikDampFactor : 0f, Time.deltaTime * 13f);
        }
        else
        {
            aimWeight = 0f;
        }
        animator.SetLayerWeight(aimingLayerIndex, aimWeight);
        if (currentGun != null)
        {
            Rig holdRig = currentGun.GetHoldRig();
            Rig aimRig = currentGun.GetAimRig();
            if (isAimingState)
            {
                if (holdRig != null) holdRig.weight = 0f;
                if (aimRig != null) aimRig.weight = aimWeight;
            }
            else
            {
                if (holdRig != null)
                {
                    float targetWeight;
                    if (!isHoldingGrenade && currentGun is not Pistol)
                    {
                        targetWeight = (1f - aimWeight) * ikDampFactor;
                    }
                    else
                    {
                        targetWeight = 1f - aimWeight;
                    }
                    holdRig.weight = Mathf.Lerp(holdRig.weight, targetWeight, Time.deltaTime * reloadSmoothSpeed);
                }
                if (aimRig != null) aimRig.weight = aimWeight;
            }
        }
        if (isAimingState && !isThrowCharging)
        {
            Vector3 aimTarget = mouseWorldPos;
            aimTarget.y = transform.position.y;
            Vector3 dir = (aimTarget - transform.position).normalized;
            transform.forward = Vector3.Lerp(transform.forward, dir, Time.deltaTime * 20f);
        }
    }

    private void HandleReloadExecution()
    {
        if (currentGun == null) return;
        animator.SetBool("isReloading", currentGun.isReloading);
        reloadWeight = Mathf.Lerp(reloadWeight, currentGun.isReloading ? 1f : 0f, Time.deltaTime * reloadSmoothSpeed);
        animator.SetLayerWeight(reloadLayerIndex, reloadWeight);
    }

    private void HandleGrenadeChargeExecution()
    {
        if (!isThrowCharging) return;
        DrawGrenadeTrajectory();
        animator.Play("Throw", throwLayerIndex, holdAtNormalizedTime);
        Vector3 aimTarget = mouseWorldPos;
        aimTarget.y = transform.position.y;
        Vector3 dir = (aimTarget - transform.position).normalized;
        transform.forward = Vector3.Lerp(transform.forward, dir, Time.deltaTime * 20f);
        starterAssetsInputs.sprint = false;
        starterAssetsInputs.jump = false;
    }

    // ==================== 非树模式（与原脚本一致的决策） ====================

    private void HandleAim()
    {
        if (isThrowCharging)
        {
            aimWeight = 0f;
            animator.SetLayerWeight(aimingLayerIndex, aimWeight);
            return;
        }
        bool isAimingInput = starterAssetsInputs.aim;
        bool isReloading = currentGun != null && currentGun.isReloading;
        bool isAiming = isAimingInput && !isReloading && currentGun != null;
        if (currentGun != null)
        {
            if (isAiming != isAimingState)
            {
                isAimingState = isAiming;
                if (crosshair != null)
                {
                    crosshair.SetActive(isAiming);
                }
            }
        }
        else
        {
            if (isAimingState)
            {
                isAimingState = false;
                if (crosshair != null)
                {
                    crosshair.SetActive(false);
                }
            }
        }
        if (isAiming)
        {
            aimVirtualCamera.gameObject.SetActive(true);
            thirdPersonController.SetSensitivity(aimSensitivity);
            thirdPersonController.SetRotateOnMove(false);
            thirdPersonController.isAiming = true;
            starterAssetsInputs.sprint = false;
            starterAssetsInputs.jump = false;
            Vector3 aimTarget = mouseWorldPos;
            aimTarget.y = transform.position.y;
            Vector3 dir = (aimTarget - transform.position).normalized;
            transform.forward = Vector3.Lerp(transform.forward, dir, Time.deltaTime * 20f);
        }
        else
        {
            aimVirtualCamera.gameObject.SetActive(false);
            thirdPersonController.SetSensitivity(normalSensitivity);
            thirdPersonController.SetRotateOnMove(true);
            thirdPersonController.isAiming = false;
            if (Keyboard.current != null)
            {
                starterAssetsInputs.sprint = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            }
        }
        float ikDampFactor = animGrounded ? 1f : 0.25f;
        if (!isHoldingGrenade)
        {
            aimWeight = Mathf.Lerp(aimWeight, isAiming ? 1f * ikDampFactor : 0f, Time.deltaTime * 13f);
        }
        else
        {
            aimWeight = 0f;
        }
        animator.SetLayerWeight(aimingLayerIndex, aimWeight);
        if (currentGun != null)
        {
            Rig holdRig = currentGun.GetHoldRig();
            Rig aimRig = currentGun.GetAimRig();
            if (isAiming)
            {
                if (holdRig != null) holdRig.weight = 0f;
                if (aimRig != null) aimRig.weight = aimWeight;
            }
            else
            {
                if (holdRig != null)
                {
                    float targetWeight;
                    if (!isHoldingGrenade && currentGun is not Pistol)
                    {
                        targetWeight = (1f - aimWeight) * ikDampFactor;
                    }
                    else
                    {
                        targetWeight = 1f - aimWeight;
                    }
                    holdRig.weight = Mathf.Lerp(holdRig.weight, targetWeight, Time.deltaTime * reloadSmoothSpeed);
                }
                if (aimRig != null) aimRig.weight = aimWeight;
            }
        }
    }

    private void HandleShoot()
    {
        if (isHoldingGrenade) return;
        bool wantShoot = starterAssetsInputs.shoot;
        starterAssetsInputs.shoot = false;
        if (!wantShoot || currentWeapon == null) return;
        if (currentKnife != null)
        {
            currentKnife.Attack();
            return;
        }
        if (currentGun == null) return;
        if (!starterAssetsInputs.aim) return;
        if (currentGun.isReloading) return;
        if (!currentGun.CanFire()) return;
        currentGun.Attack();
        ApplyRecoil(currentGun.recoilAmplitude, currentGun.recoilDuration);
    }

    private void HandleReload()
    {
        if (currentGun == null) return;
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (!currentGun.isReloading && currentGun.CanReload())
            {
                currentGun.Reload();
                animator.Play("Reload", reloadLayerIndex, 0f);
            }
        }
        animator.SetBool("isReloading", currentGun.isReloading);
        reloadWeight = Mathf.Lerp(reloadWeight, currentGun.isReloading ? 1f : 0f, Time.deltaTime * reloadSmoothSpeed);
        animator.SetLayerWeight(reloadLayerIndex, reloadWeight);
    }

    // ==================== 武器/手雷共用逻辑（原样保留） ====================

    private void UpdateWeaponPose()
    {
        if (currentWeapon == null) return;
        var w = currentWeapon;
        w.transform.SetPositionAndRotation(
            Vector3.Lerp(w.holdPoint.position, w.aimPoint.position, aimWeight),
            Quaternion.Lerp(w.holdPoint.rotation, w.aimPoint.rotation, aimWeight)
        );
    }

    private void ApplyRecoil(float amp, float duration)
    {
        if (recoilNoise == null) return;
        recoilTimer = duration;
        currentRecoilDuration = duration;
        recoilNoise.AmplitudeGain = amp;
    }

    private void UpdateRecoil()
    {
        if (recoilTimer > 0)
        {
            recoilTimer -= Time.deltaTime;
            if (recoilNoise != null)
                recoilNoise.AmplitudeGain = Mathf.Lerp(0f, recoilNoise.AmplitudeGain, recoilTimer / currentRecoilDuration);
        }
    }

    public void TryStartHoldGrenade()
    {
        SlotUI grenadeSlot = null;
        foreach (var slot in Inventory.instance.slots)
        {
            if (slot.currentItem != null && slot.currentItem.itemName == "手雷")
            {
                grenadeSlot = slot;
                break;
            }
        }
        if (grenadeSlot == null) return;
        HolsterCurrentWeapon();
        SpawnGrenadeInHand();
        weaponManager.currentWeapon = null;
    }

    private void HolsterCurrentWeapon()
    {
        if (currentWeapon != null)
        {
            if (currentGun != null)
            {
                Rig holdRig = currentGun.GetHoldRig();
                Rig aimRig = currentGun.GetAimRig();
                if (holdRig != null) holdRig.weight = 0f;
                if (aimRig != null) aimRig.weight = 0f;
            }
            currentWeapon.gameObject.SetActive(false);
        }
        holdWeight = 0f;
        aimWeight = 0f;
        animator.SetLayerWeight(holdLayerIndex, 0f);
        animator.SetLayerWeight(aimingLayerIndex, 0f);
        aimVirtualCamera.gameObject.SetActive(false);
        crosshair.SetActive(false);
        isAimingState = false;
    }

    private void SpawnGrenadeInHand()
    {
        if (currentGrenade != null) Destroy(currentGrenade);
        currentGrenade = Instantiate(grenadePrefab, grenadeHandHolder);
        currentGrenade.transform.localPosition = Vector3.zero;
        currentGrenade.transform.localRotation = Quaternion.identity;
        isHoldingGrenade = true;
    }

    private void ThrowGrenade()
    {
        if (!isHoldingGrenade || currentGrenade == null) return;
        SlotUI grenadeSlot = null;
        foreach (var slot in Inventory.instance.slots)
        {
            if (slot.currentItem != null && slot.currentItem.itemName == "手雷")
            {
                grenadeSlot = slot;
                break;
            }
        }
        if (grenadeSlot != null)
        {
            Inventory.instance.UseItem(grenadeSlot);
        }
        currentGrenade.transform.SetParent(null);
        currentGrenade.transform.position = transform.TransformPoint(THROW_POINT_RELATIVE_TO_ROOT);
        Rigidbody rb = currentGrenade.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            Vector3 throwDir = Camera.main.transform.forward + Vector3.up * 0.3f;
            rb.AddForce(throwDir * throwForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        }
        currentGrenade = null;
        isHoldingGrenade = false;
    }

    public void ClearCurrentGrenade()
    {
        if (currentGrenade != null)
        {
            Destroy(currentGrenade);
            currentGrenade = null;
        }
        isHoldingGrenade = false;
        animator.SetLayerWeight(throwLayerIndex, 0f);
        if (grenadeTrajectoryLine != null)
            grenadeTrajectoryLine.enabled = false;
        thirdPersonController.lockPlayerControl = false;
    }

    private IEnumerator DoThrowGrenadeAfterAnimation()
    {
        yield return new WaitForSeconds(0.75f);
        ThrowGrenade();
        animator.SetBool("ThrowHold", false);
        thirdPersonController.lockPlayerControl = false;
        yield return new WaitForSeconds(1.25f);
        animator.SetLayerWeight(throwLayerIndex, 0f);
    }

    public bool IsHoldingGrenade()
    {
        return isHoldingGrenade;
    }

    public bool IsCompletelyUnarmed()
    {
        return currentWeapon == null && !isHoldingGrenade;
    }

    public void CancelGrenadeThrow()
    {
        if (!isThrowCharging) return;
        isThrowCharging = false;
        thirdPersonController.lockPlayerControl = false;
        thirdPersonController.SetRotateOnMove(true);
        if (aimVirtualCamera != null)
            aimVirtualCamera.gameObject.SetActive(false);
        animator.SetBool("ThrowHold", false);
        animator.SetLayerWeight(throwLayerIndex, 0f);
        animator.Play("Empty", throwLayerIndex, 0f);
        if (grenadeTrajectoryLine != null)
            grenadeTrajectoryLine.enabled = false;
    }

    private void DrawGrenadeTrajectory()
    {
        if (grenadeTrajectoryLine == null || _cachedGrenadeRb == null)
            return;
        // 简化：直接以角色胸口硬编码点为起点（与原脚本一致）
        Vector3 startPos = transform.TransformPoint(THROW_POINT_RELATIVE_TO_ROOT);
        Vector3 throwDir = Camera.main.transform.forward + Vector3.up * 0.3f;
        float previewVel = throwForce / _cachedGrenadeRb.mass;
        Vector3 currentVel = throwDir.normalized * previewVel;
        Vector3 pos = startPos;
        Vector3 gravity = Physics.gravity;
        bool hitOccurred = false;
        for (int i = 0; i < trajectoryPointCount; i++)
        {
            _trajectoryPoints[i] = pos;
            if (hitOccurred)
            {
                continue;
            }
            Vector3 nextPos = pos + currentVel * Time.fixedDeltaTime;
            if (Physics.Linecast(pos, nextPos, out RaycastHit hit))
            {
                _trajectoryPoints[i] = hit.point;
                hitOccurred = true;
            }
            else
            {
                pos = nextPos;
            }
            currentVel += gravity * Time.fixedDeltaTime;
        }
        grenadeTrajectoryLine.SetPositions(_trajectoryPoints);
    }
}
