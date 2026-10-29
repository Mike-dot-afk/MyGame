using StarterAssets;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private Animator animator;
    [SerializeField] private bool isEditMode;
    public float maxHp;
    public float currentHp;
    public float greenFlowerHealingAmount = 30f;
    public UIManager uiManager;

    [Header("死亡失败UI")]
    public GameObject revivePanel;
    public Image blackFadeImage;
    public Button restartCheckpointBtn;
    public float fadeToBlackDuration = 1.5f;

    private BattleStageManager _battleStageManager;
    private Vector3 lastHitPoint;
    private Coroutine deathFadeCoroutine;
    public bool alreadyDead = false;
    public static PlayerHealth Instance;

    void Awake()
    {
        Instance = this;
        _battleStageManager = FindObjectOfType<BattleStageManager>();
        if (restartCheckpointBtn != null)
        {
            restartCheckpointBtn.onClick.AddListener(RestartToCheckpoint);
        }
    }

    void Start()
    {
        currentHp = maxHp;
        if (uiManager != null)
        {
            uiManager.OnPlayerHPChanged(Mathf.RoundToInt(currentHp), Mathf.RoundToInt(maxHp));
        }

        if (revivePanel != null)
            revivePanel.SetActive(false);
        if (blackFadeImage != null)
            blackFadeImage.color = new Color(0, 0, 0, 0);
    }

    public void TakeDamage(float damage, Vector3 hitPoint)
    {
        if (alreadyDead) return;

        lastHitPoint = hitPoint;
        currentHp = Mathf.Max(0, currentHp - damage);
        if (DamageFlashUI.Instance != null)
        {
            DamageFlashUI.Instance.Flash();
        }
        if (uiManager != null)
        {
            uiManager.OnPlayerHPChanged(Mathf.RoundToInt(currentHp), Mathf.RoundToInt(maxHp));
        }
        if (currentHp <= 0)
        {
            Die();
        }
    }

    public float GetHpNormalized()
    {
        return currentHp / maxHp;
    }

    void Die()
    {
        if (isEditMode)
        {
            return;
        }
        //已经执行过死亡，直接返回，不再重复跑逻辑
        if (alreadyDead)
        {
            return;
        }
        alreadyDead = true;
        animator.SetTrigger("isDead");
        GetComponent<Collider>().enabled = false;
        ThirdPersonController controller = GetComponent<ThirdPersonController>();
        ThirdPersonShooterController shooterController = GetComponent<ThirdPersonShooterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
        if (shooterController != null)
        {
            shooterController.enabled = false;
        }

        StarterAssetsInputs inputs = GetComponent<StarterAssetsInputs>();
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (inputs != null)
        {
            inputs.enabled = false; // 禁用键盘鼠标游戏输入
        }
        if (playerInput != null)
        {
            playerInput.actions.FindActionMap("Player")?.Disable();
        }
        Cursor.lockState = CursorLockMode.None; // 解锁鼠标，可以移动光标
        Cursor.visible = true;

        StartGameFlow.PauseFight();

        if (revivePanel != null)
        {
            revivePanel.SetActive(true);
        }

        if (deathFadeCoroutine != null)
            StopCoroutine(deathFadeCoroutine);
        deathFadeCoroutine = StartCoroutine(FadeToBlackCoroutine());

        if (_battleStageManager != null)
        {
            _battleStageManager.StopBattleBGM();
        }
    }

    IEnumerator FadeToBlackCoroutine()
    {
        float timer = 0;
        Color col = blackFadeImage.color;
        while (timer < fadeToBlackDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(timer / fadeToBlackDuration);
            blackFadeImage.color = new Color(col.r, col.g, col.b, alpha);
            yield return null;
        }
    }

    void RestartToCheckpoint()
    {
        if (deathFadeCoroutine != null)
        {
            StopCoroutine(deathFadeCoroutine);
            deathFadeCoroutine = null;
        }

        StartGameFlow.ResumeFight();

        SaveSystem.currentSlotID = SaveSystem.CheckpointSaveSlot;
        SaveSystem.LoadGame(SaveSystem.CheckpointSaveSlot);
        alreadyDead = false;
        StartGameFlow.ForceClearFightPauseCounter();

        if (revivePanel != null)
            revivePanel.SetActive(false);
        if (blackFadeImage != null)
            blackFadeImage.color = new Color(0, 0, 0, 0);

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        ThirdPersonController controller = GetComponent<ThirdPersonController>();
        ThirdPersonShooterController shooterController = GetComponent<ThirdPersonShooterController>();
        if (controller != null) controller.enabled = true;
        if (shooterController != null) shooterController.enabled = true;

        StarterAssetsInputs inputs = GetComponent<StarterAssetsInputs>();
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (inputs != null)
        {
            inputs.cursorLocked = true;
            inputs.enabled = true;
        }
        if (playerInput != null)
        {
            playerInput.actions.FindActionMap("Player")?.Enable();
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        animator.ResetTrigger("isDead");
        animator.Play("Idle Walk Run Blend");
    }
}