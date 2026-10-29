using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections;

public class Shotgun : GunWeapon
{
    [SerializeField] private Rig holdRig;
    [SerializeField] private Rig aimRig;

    [Header("霰弹枪专属")]
    public GameObject bulletPrefab;
    public int pelletCount = 8;
    public float spreadAngle = 15f;

    [Header("泵动IK参数")]
    public float pumpResetSpeed = 8f;
    public float pumpCooldown = 0.25f;
    public TwoBoneIKConstraint leftHandAimIk;

    private Vector3 originalTargetLocalPos;
    private bool isPumping;
    private float pumpTimer;

    public override Rig GetHoldRig() => holdRig;
    public override Rig GetAimRig() => aimRig;

    private new void Awake()
    {
        base.Awake();
        if (leftHandAimIk != null && leftHandAimIk.data.target != null)
        {
            originalTargetLocalPos = leftHandAimIk.data.target.localPosition;
        }
    }

    private void Update()
    {
        if (pumpTimer > 0)
        {
            pumpTimer -= Time.deltaTime;
        }
    }

    private void PumpAction()
    {
        if (pumpTimer > 0 || isPumping || leftHandAimIk == null || leftHandAimIk.data.target == null)
            return;

        StartCoroutine(PumpActionDelayed());
    }

    private IEnumerator PumpActionDelayed()
    {
        isPumping = true;
        pumpTimer = pumpCooldown;

        yield return new WaitForSeconds(0.2f);

        Transform target = leftHandAimIk.data.target;
        Vector3 pumpOffset = new Vector3(0.04f, 0f, -0.101f);
        target.localPosition = originalTargetLocalPos + pumpOffset;

        float elapsedTime = 0f;
        float totalResetTime = 1f / pumpResetSpeed;

        while (elapsedTime < totalResetTime)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / totalResetTime;
            target.localPosition = Vector3.Lerp(
                target.localPosition,
                originalTargetLocalPos,
                t
            );
            yield return null;
        }

        target.localPosition = originalTargetLocalPos;
        isPumping = false;
    }

    public override void OnFire()
    {
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 baseDir = (ThirdPersonShooterController.Instance.mouseWorldPos - firePoint.position).normalized;

            Vector3 randDir = Quaternion.Euler(
                Random.Range(-spreadAngle, spreadAngle),
                Random.Range(-spreadAngle, spreadAngle),
                0f
            ) * baseDir;

            Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(randDir));
        }

        PumpAction();
    }
}