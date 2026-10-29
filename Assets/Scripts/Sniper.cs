using UnityEngine;

public class Sniper : GunWeapon
{
    public GameObject bulletPrefab;

    public override void Attack()
    {
        Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
    }

    public override void Reload()
    {
        // 可以在这里播放换弹动画或音效
    }
}