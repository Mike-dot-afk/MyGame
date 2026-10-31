using Newtonsoft.Json.Bson;
using UnityEngine;
public class UIManager : MonoBehaviour
{
    [Header("UI组件引用")]
    [SerializeField] public BagUI bagUI;
    [SerializeField] private HPBarUI hpBar;
    [SerializeField] private AmmoUI ammoUI;
    [SerializeField] private WeaponQuickBarUI weaponBar;
    [SerializeField] private BoxUI boxUI;
    [SerializeField] private GameObject saveAndExitButton;
    [Header("快捷栏父物体")]
    [SerializeField] private GameObject weaponQuickBar;
    public static UIManager Instance;
    // 标记：是否打开了储物箱
    private bool isStorageBoxOpen = false;
    // 标记：是否打开了存档点
    public bool isSaveUIOpen = false;
    // 标记：是否正在阅读纸条，阅读纸条期间禁止打开背包、储物箱
    public bool isNoteReading = false;
    private void Awake()
    {
        Instance = this;
    }
    void Update()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.alreadyDead)
        {
            return;
        }
        // 全局输入：快捷切武器
        if (Input.GetKeyDown(KeyCode.Alpha1)) OnWeaponChanged(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) OnWeaponChanged(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) OnWeaponChanged(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) OnWeaponChanged(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) OnWeaponChanged(4);
        // 全局输入：开/关背包
        if (!isSaveUIOpen && !isStorageBoxOpen && !isNoteReading && !StartGameFlow.IsSystemLocked && Input.GetKeyDown(KeyCode.B))
        {
            ToggleBagAndQuickBar();
        }
    }
    public void SetUIMode(bool isOpen)
    {
        Time.timeScale = isOpen ? 0 : 1;  // 暂停/恢复
        if (isOpen)
        {
            StartGameFlow.LockPlayerInput();
            if (ThirdPersonShooterController.Instance != null)
                ThirdPersonShooterController.Instance.CancelGrenadeForUI();
        }
        else
        {
            StartGameFlow.UnlockPlayerInput();
        }
        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;
        // UI音频统一管理：打开时压低BGM并暂停音效，关闭时恢复
        if (AudioManager.Instance != null)
        {
            if (isOpen) AudioManager.Instance.PauseAllForUI();
            else AudioManager.Instance.ResumeAllForUI();
        }
    }
    // 供玩家脚本调用：血量变化时更新血条
    public void OnPlayerHPChanged(int currentHp, int maxHp)
    {
        hpBar.UpdateHP(currentHp, maxHp);
    }
    // 供武器脚本调用：子弹变化时更新显示
    public void OnAmmoChanged(int currentAmmo, int remainAmmo)
    {
        ammoUI.UpdateAmmo(currentAmmo, remainAmmo);
    }
    // 供读档、捡子弹时调用：强制刷新UI
    public void RefreshAmmoUI()
    {
        if (WeaponManager.Instance == null) return;
        // 用新方法获取子弹
        WeaponManager.Instance.GetCurrentAmmo(out int currentAmmo, out int remainAmmo);
        OnAmmoChanged(currentAmmo, remainAmmo);
    }
    // 供武器/输入调用：切换武器
    public void OnWeaponChanged(int slotIndex)
    {
        WeaponQuickBarSlot[] slots = weaponBar.GetComponentsInChildren<WeaponQuickBarSlot>(true);
        WeaponQuickBarUI.Instance.currentSelectedSlotIndex = slotIndex + 1;
        foreach (var slot in slots)
        {
            if (slot.slotIndex == slotIndex)
            {
                WeaponManager.Instance.EquipWeaponByItem(slot.currentItem);
                return;
            }
        }
    }
    // 切换背包+快捷栏显示/隐藏（正常状态）
    public void ToggleBagAndQuickBar()
    {
        if (StartGameFlow.IsSystemLocked) return;
        bool isBagActive = !bagUI.IsBagOpen();
        bagUI.SetBagActive(isBagActive);
        weaponQuickBar.SetActive(isBagActive);
        saveAndExitButton.SetActive(isBagActive);
        SetUIMode(isBagActive);
    }
    // 打开储物箱时调用
    public void OnStorageBoxOpened()
    {
        isStorageBoxOpen = true;
        boxUI.SetBoxActive(true);
        bagUI.SetBagActive(true);          // 强制开背包
        weaponQuickBar.SetActive(false);   // 强制藏快捷栏
        saveAndExitButton.SetActive(false);// 强制隐藏保存并退出按钮
        SetUIMode(true);
    }
    // 关闭储物箱时调用
    public void OnStorageBoxClosed()
    {
        isStorageBoxOpen = false;
        boxUI.SetBoxActive(false);
        bagUI.SetBagActive(false);      // 强制关背包
        weaponQuickBar.SetActive(false);
        SetUIMode(false);
    }
    //开关储物箱
    public void ToggleStorageBox()
    {
        if (isNoteReading || StartGameFlow.IsSystemLocked) return;
        if (isStorageBoxOpen)
        {
            OnStorageBoxClosed();
        }
        else
        {
            OnStorageBoxOpened();
        }
    }
}
