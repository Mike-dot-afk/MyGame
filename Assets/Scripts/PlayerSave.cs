using UnityEngine;
using System.Collections;

public class PlayerSave : SaveableEntity
{
    [Header("玩家专属存档数据")]
    // 不再手动赋值，运行时由 PlayerHealth 同步
    public int hp;

    private PlayerHealth _playerHealth;
    private int _pendingLoadWeaponSlot = 0;

    void Awake()
    {
        // 获取同物体上的 PlayerHealth 组件
        _playerHealth = GetComponent<PlayerHealth>();
        if (_playerHealth == null)
        {
            return;
        }
    }

    public override void SaveData()
    {
        if (_playerHealth != null)
        {
            hp = Mathf.RoundToInt(_playerHealth.currentHp);
        }

        int saveSlot = 0;

        // 手雷 = 6
        if (ThirdPersonShooterController.Instance.IsHoldingGrenade())
        {
            saveSlot = 6;
        }
        // 空手 = 0
        else if (WeaponManager.Instance.IsUnarmed())
        {
            saveSlot = 0;
        }
        // 武器 1~5
        else
        {
            saveSlot = WeaponQuickBarUI.Instance.currentSelectedSlotIndex;
        }

        SaveSystem.SaveEntity(saveableID, "Position", transform.position);
        SaveSystem.SaveEntity(saveableID, "Active", gameObject.activeSelf);
        SaveSystem.SaveEntity(saveableID, "HP", hp);
        SaveSystem.SaveEntity(saveableID, "WeaponSlot", saveSlot);
    }

    public override void LoadData()
    {
        Vector3 savedPos = SaveSystem.LoadEntity<Vector3>(saveableID, "Position", transform.position);
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = savedPos;
        if (cc != null) cc.enabled = true;

        gameObject.SetActive(SaveSystem.LoadEntity<bool>(saveableID, "Active", true));

        // 读档时：把存档值写回 PlayerHealth，并更新 UI
        hp = SaveSystem.LoadEntity<int>(saveableID, "HP", 100);
        if (_playerHealth != null)
        {
            _playerHealth.currentHp = hp;
            // 通知 UI 更新血量显示
            if (_playerHealth.uiManager != null)
            {
                _playerHealth.uiManager.OnPlayerHPChanged(
                    Mathf.RoundToInt(_playerHealth.currentHp),
                    Mathf.RoundToInt(_playerHealth.maxHp)
                );
            }
        }
        int savedSlot = SaveSystem.LoadEntity<int>(saveableID, "WeaponSlot", 0);
        _pendingLoadWeaponSlot = SaveSystem.LoadEntity<int>(saveableID, "WeaponSlot", 0);
        if (UIManager.Instance != null)
        {
            UIManager.Instance.RefreshAmmoUI();
        }
        StartCoroutine(DelayEquipWeapon());
    }

    private IEnumerator DelayEquipWeapon()
    {
        // 等待一帧，UGUI、WeaponQuickBarUI、所有子Slot全部初始化完成
        yield return null;

        // UI实例有可能还没生成，做防御判空
        if (WeaponQuickBarUI.Instance != null)
        {
            int slot = _pendingLoadWeaponSlot;
            WeaponQuickBarUI.Instance.SwitchToSlot(slot);
        }
    }

    public int GetPendingWeaponSlot() => _pendingLoadWeaponSlot;
}