using UnityEngine;

public class WeaponAnimationEvents : MonoBehaviour
{
    private WeaponManager weaponManager;

    private void Awake()
    {
        weaponManager = GetComponent<WeaponManager>();
    }

    // 在换弹动画的最后一帧调用此方法
    public void OnReloadAnimationFinished()
    {
        if (weaponManager.currentWeapon is GunWeapon gun)
        {
            gun.OnReloadFinish();
        }
    }
}