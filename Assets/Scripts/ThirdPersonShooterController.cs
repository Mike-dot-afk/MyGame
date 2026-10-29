using StarterAssets;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
public class ThirdPersonShooterController : MonoBehaviour
{
    [Header("相机设置")]
    [SerializeField] private CinemachineCamera aimVirtualCamera;
    [SerializeField] private float normalSensitivity;
    [SerializeField] private float aimSensitivity;
    [SerializeField] private LayerMask aimColliderLayerMask;
    [SerializeField] private Transform debugTransform;
    [Header("动画与IK")]
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
    [Header("手雷抛物线预览")]
    public LineRenderer grenadeTrajectoryLine;
    [Tooltip("轨迹采样点数量")]
    public int trajectoryPointCount = 30;
    private Vector3[] _trajectoryPoints;
    private Rigidbody _cachedGrenadeRb;
    [Header("编辑模式")]
    [SerializeField] private bool isEditMode;
    [Header("落地动画缓冲设置")]
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
    private Coroutine _throwGrenadeCoroutine;
    private WeaponManager weaponManager;
    private Weapon currentWeapon => weaponManager.currentWeapon;
    private GunWeapon currentGun => currentWeapon as GunWeapon;
    public Knife currentKnife => currentWeapon as Knife;
    private Weapon _lastWeapon;
    public static ThirdPersonShooterController Instance;
    private readonly Vector3 THROW_POINT_RELATIVE_TO_ROOT = new Vector3(0.394f, 1.904f, -0.089f);
    private bool animGrounded;
    private float _postUIInputBlockTimer;
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
        //缓存手雷预制体刚体，用于冲量换算预览初速度
        if (grenadePrefab != null)
        {
            _cachedGrenadeRb = grenadePrefab.GetComponent<Rigidbody>();
        }
    }
    private void Update()
    {
        // UI打开期间（timeScale=0）屏蔽所有旧输入
        if (Time.timeScale == 0f) return;
        // UI关闭后短暂防误触窗口：吃掉InputSystem补发的shoot/look
        if (_postUIInputBlockTimer > 0f)
        {
            _postUIInputBlockTimer -= Time.deltaTime;
            starterAssetsInputs.shoot = false;
            starterAssetsInputs.look = Vector2.zero;
            return;
        }
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
        // 检测：刚刚从空中落地（物理刚接触地面）
        if (physicsGrounded && !_prevPhysicsGrounded)
        {
            _landBufferTimer = landAnimationBufferTime;
        }
        // 只要缓冲计时器>0，就倒计时；在空中直接清零
        if (_landBufferTimer > 0)
        {
            _landBufferTimer -= Time.deltaTime;
        }
        if (!physicsGrounded)
        {
            _landBufferTimer = 0f;
        }
        // 真正用于IK的逻辑：物理落地 并且 落地缓冲已经结束，才算真正“动画上的落地”
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
        HandleAim();
        HandleShoot();
        UpdateWeaponPose();
        UpdateRecoil();
        HandleReload();
        // 按 6 拿出手雷
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            TryStartHoldGrenade();
        }
        // 按住左键，开始蓄力
        if (isHoldingGrenade && Input.GetMouseButtonDown(0))
        {
            animator.SetLayerWeight(throwLayerIndex, 1f);
            isThrowCharging = true;
            animator.SetBool("ThrowHold", true);
            //打开抛物线线
            if (grenadeTrajectoryLine != null)
                grenadeTrajectoryLine.enabled = true;
            thirdPersonController.SetRotateOnMove(false);
            if (aimVirtualCamera != null)
                aimVirtualCamera.gameObject.SetActive(true);
            starterAssetsInputs.sprint = false;
            starterAssetsInputs.jump = false;
        }
        if (isThrowCharging)
        {
            DrawGrenadeTrajectory();
            animator.Play("Throw", throwLayerIndex, holdAtNormalizedTime);
            Vector3 aimTarget = mouseWorldPos;
            aimTarget.y = transform.position.y;
            Vector3 dir = (aimTarget - transform.position).normalized;
            transform.forward = Vector3.Lerp(transform.forward, dir, Time.deltaTime * 20f);
            starterAssetsInputs.sprint = false;
            starterAssetsInputs.jump = false;
            if (Input.GetMouseButtonDown(1))
            {
                CancelGrenadeThrow();
            }
        }
        // 松开左键，扔出去
        if (isThrowCharging && Input.GetMouseButtonUp(0))
        {
            thirdPersonController.lockPlayerControl = true;
            isThrowCharging = false;
            thirdPersonController.SetRotateOnMove(true);
            if (aimVirtualCamera != null)
                aimVirtualCamera.gameObject.SetActive(false);
            if (grenadeTrajectoryLine != null)
                grenadeTrajectoryLine.enabled = false;
            _throwGrenadeCoroutine = StartCoroutine(DoThrowGrenadeAfterAnimation());
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
    private void CancelGrenadeThrow()
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
    /// <summary>UI关闭后调用：开启短暂防误触窗口，窗口内忽略补发的shoot/look</summary>
    public void BlockInputAfterUI(float seconds)
    {
        _postUIInputBlockTimer = seconds;
    }
    /// <summary>UI打开时调用：取消进行中的手雷蓄力或投掷，手雷留在手里</summary>
    public void CancelGrenadeForUI()
    {
        if (_throwGrenadeCoroutine != null)
        {
            StopCoroutine(_throwGrenadeCoroutine);
            _throwGrenadeCoroutine = null;
        }
        if (isThrowCharging)
        {
            CancelGrenadeThrow();
        }
    }
    private void DrawGrenadeTrajectory()
    {
        if (grenadeTrajectoryLine == null || _cachedGrenadeRb == null)
            return;
        // 核心：直接拿相对于角色根的硬编码点，算出世界出手位置
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
