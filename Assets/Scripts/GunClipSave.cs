using UnityEngine;

public class GunClipSave : SaveableEntity
{
    private GunWeapon _gunWeapon;

    void Awake()
    {
        _gunWeapon = GetComponent<GunWeapon>();
    }

    public override void SaveData()
    {
        if (_gunWeapon == null)
            return;

        SaveSystem.SaveEntity(saveableID, "CurrentClipAmmo", _gunWeapon.currentClipAmmo);
    }

    public override void LoadData()
    {
        if (_gunWeapon == null)
            return;

        int savedAmmo = SaveSystem.LoadEntity<int>(saveableID, "CurrentClipAmmo", _gunWeapon.clipSize);
        _gunWeapon.currentClipAmmo = savedAmmo;

        // ¶ÁµµºóË¢ÐÂ×Óµ¯UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.RefreshAmmoUI();
        }
    }
}