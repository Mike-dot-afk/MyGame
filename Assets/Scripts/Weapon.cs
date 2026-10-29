using UnityEngine;

public abstract class Weapon : MonoBehaviour
{
    [Header("武器数据")]
    public ItemData weaponData;

    [Header("姿态点位")]
    public Transform holdPoint;
    public Transform aimPoint;
    public Transform firePoint;

    public virtual void Equip()
    {
        gameObject.SetActive(true);
    }

    public virtual void Unequip()
    {
        gameObject.SetActive(false);
    }

    public abstract void Attack();
}