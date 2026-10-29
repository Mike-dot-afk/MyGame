using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance;

    [Header("场景中的所有武器")]
    public List<Weapon> allWeapons;

    public Weapon currentWeapon;

    private void Awake()
    {
        Instance = this;
    }

    public void EquipWeaponByItem(ItemData item)
    {
        if (item == null) return;

        Weapon newWeapon = FindWeaponByItemData(item);
        if (newWeapon == null || newWeapon == currentWeapon) return;

        ClearHeldGrenade();  // 销毁手雷

        if (currentWeapon != null)
        {
            currentWeapon.Unequip();

            if (currentWeapon is Knife knife)
            {
                knife.OnWeaponHolstered();
            }
        }

        currentWeapon = newWeapon;
        currentWeapon.Equip();

        if (currentWeapon is Knife)
        {
            ((Knife)currentWeapon).OnWeaponEquipped();
        }

        if (currentWeapon is GunWeapon gun)
        {
            UIManager.Instance.OnAmmoChanged(gun.currentClipAmmo, gun.GetTotalAmmoInBag());
        }
    }

    private Weapon FindWeaponByItemData(ItemData item)
    {
        foreach (Weapon w in allWeapons)
        {
            if (w.weaponData == item)
            {
                return w;
            }
        }
        return null;
    }

    // 获取当前武器的子弹数据（兼容枪类武器）
    public void GetCurrentAmmo(out int currentAmmo, out int remainAmmo)
    {
        currentAmmo = 0;
        remainAmmo = 0;

        if (currentWeapon == null) return;

        // 安全判断：当前武器是不是枪（GunWeapon子类）
        if (currentWeapon is GunWeapon gun)
        {
            currentAmmo = gun.currentClipAmmo;
            remainAmmo = gun.GetTotalAmmoInBag(); // 假设remainAmmo是GunWeapon里的剩余子弹字段
        }
    }

    // 切武器时销毁手中手雷
    public void ClearHeldGrenade()
    {
        var controller = ThirdPersonShooterController.Instance;
        if (controller != null)
        {
            controller.ClearCurrentGrenade();
        }
    }

    // 卸下当前武器 → 空手
    public void UnequipCurrentWeapon()
    {
        if (currentWeapon != null)
        {
            currentWeapon.Unequip();

            if (currentWeapon is Knife knife)
            {
                knife.OnWeaponHolstered();
            }

            currentWeapon = null;
        }

        ClearHeldGrenade();
    }

    // 判断当前是否空手
    public bool IsUnarmed()
    {
        return currentWeapon == null && ThirdPersonShooterController.Instance.IsCompletelyUnarmed();
    }
}