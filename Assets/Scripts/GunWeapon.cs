using UnityEngine;
using UnityEngine.Animations.Rigging;

public class GunWeapon : Weapon
{
    [Header("µ¯Ï»")]
    public int clipSize = 12;
    public int currentClipAmmo;

    [Header("Éä»÷")]
    public float fireRate = 0.15f;
    public float recoilAmplitude = 1f;
    public float recoilDuration = 0.2f;

    [Header("»»µ¯")]
    public TwoBoneIKConstraint leftHandIK;

    [Header("ÒôÐ§")]
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip emptySound;
    [Range(0f, 1f)]
    public float shootVolume = 1f;
    [Range(0f, 1f)]
    public float reloadVolume = 1f;

    public bool isReloading;
    protected float lastFireTime;

    public virtual void OnFire() { }
    public virtual Rig GetHoldRig() => null;
    public virtual Rig GetAimRig() => null;

    protected virtual void Awake()
    {
        currentClipAmmo = clipSize;
    }

    public virtual bool CanFire()
    {
        if (isReloading) return false;
        if (currentClipAmmo <= 0)
        {
            if (emptySound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX3D(emptySound, firePoint.position, 0.5f);
            }
            return false;
        }
        return Time.time >= lastFireTime + fireRate;
    }

    public override void Attack()
    {
        if (!CanFire()) return;
        Fire();
    }

    public virtual void Fire()
    {
        lastFireTime = Time.time;
        currentClipAmmo--;
        PlayShootSound();
        OnFire();

        UIManager.Instance.OnAmmoChanged(currentClipAmmo, GetTotalAmmoInBag());
    }

    protected virtual void PlayShootSound()
    {
        if (shootSound == null) return;
        if (AudioManager.Instance == null) return;

        AudioManager.Instance.PlaySFX3D(shootSound, firePoint.position, shootVolume);
    }

    protected virtual void PlayReloadSound()
    {
        if (reloadSound == null) return;
        if (AudioManager.Instance == null) return;

        AudioManager.Instance.PlaySFX3D(reloadSound, firePoint.position, reloadVolume);
    }

    public virtual bool CanReload()
    {
        if (isReloading || currentClipAmmo >= clipSize)
            return false;

        return GetTotalAmmoInBag() > 0;
    }

    public virtual void Reload()
    {
        isReloading = true;
        PlayReloadSound();
    }

    public virtual void OnReloadFinish()
    {
        int needed = clipSize - currentClipAmmo;
        int available = GetTotalAmmoInBag();
        int take = Mathf.Min(needed, available);

        if (take > 0)
        {
            ConsumeAmmoFromBag(take);
            currentClipAmmo += take;
        }

        isReloading = false;
        UIManager.Instance.OnAmmoChanged(currentClipAmmo, GetTotalAmmoInBag());
    }

    public int GetTotalAmmoInBag()
    {
        ItemData ammoType = weaponData.ammoType;
        if (ammoType == null) return 0;
        if (Inventory.instance == null || Inventory.instance.slots == null) return 0;

        int total = 0;
        foreach (SlotUI slot in Inventory.instance.slots)
        {
            if (slot.currentItem == ammoType)
                total += slot.count;
        }
        return total;
    }

    private void ConsumeAmmoFromBag(int amount)
    {
        ItemData ammoType = weaponData.ammoType;
        if (ammoType == null || amount <= 0) return;

        if (Inventory.instance == null || Inventory.instance.slots == null) return;

        int remaining = amount;
        foreach (SlotUI slot in Inventory.instance.slots)
        {
            if (slot.currentItem == ammoType && slot.count > 0 && remaining > 0)
            {
                int take = Mathf.Min(remaining, slot.count);
                slot.count -= take;
                remaining -= take;

                if (slot.count <= 0)
                    slot.ClearSlot();
                else
                    slot.SetItem(slot.currentItem, slot.count);
            }
        }
    }
}