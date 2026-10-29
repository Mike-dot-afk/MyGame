using UnityEngine;
using UnityEngine.Animations.Rigging;

public class Pistol : GunWeapon
{
    [SerializeField] private Rig aimRig;

    public GameObject bulletPrefab;

    public override Rig GetAimRig() => aimRig;

    public override void OnFire()
    {
        Vector3 shootDirection = (ThirdPersonShooterController.Instance.mouseWorldPos - firePoint.position).normalized;
        Quaternion bulletRot = Quaternion.LookRotation(shootDirection);
        Instantiate(bulletPrefab, firePoint.position, bulletRot);
    }
}