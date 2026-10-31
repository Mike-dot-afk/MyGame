using UnityEngine;
using UnityEngine.UI;

public class RestartNewGameButton : MonoBehaviour
{
    [Header("新游戏按钮")]
    public Button newGameBtn;
    [Header("确认弹窗")]
    public GameObject confirmPanel;
    [Header("确认与取消按钮")]
    public Button confirmBtn;
    public Button cancelBtn;
    [Header("开始流程控制(可留空，自动查找)")]
    public StartGameFlow startGameFlow;

    void Awake()
    {
        if (newGameBtn != null)
        {
            newGameBtn.onClick.AddListener(OnNewGameClicked);
        }
        if (confirmBtn != null)
        {
            confirmBtn.onClick.AddListener(OnConfirmClicked);
        }
        if (cancelBtn != null)
        {
            cancelBtn.onClick.AddListener(OnCancelClicked);
        }
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
        StartGameFlow.OnGameStarted += HideNewGameButton;
    }

    void OnDestroy()
    {
        StartGameFlow.OnGameStarted -= HideNewGameButton;
    }

    void Start()
    {
        if (newGameBtn == null) return;
        string[] allKeys = SaveFileManager.Instance.GetAllStoredDataIDs();
        bool hasSave = allKeys != null && allKeys.Length > 0;
        newGameBtn.gameObject.SetActive(hasSave);
    }

    // 游戏开始后隐藏新游戏按钮
    private void HideNewGameButton()
    {
        if (newGameBtn != null)
        {
            newGameBtn.gameObject.SetActive(false);
        }
    }

    // 点击新游戏按钮：弹出确认弹窗
    private void OnNewGameClicked()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }
    }

    // 点击取消：关闭弹窗，不做任何事
    private void OnCancelClicked()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    // 点击确认：隐藏弹窗，清空存档并重置开场标志，直接进入开场CG
    private void OnConfirmClicked()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
        if (SaveFileManager.Instance != null)
        {
            SaveFileManager.Instance.ClearAllData();
        }
        if (startGameFlow == null)
        {
            startGameFlow = FindObjectOfType<StartGameFlow>();
        }
        if (startGameFlow != null)
        {
            startGameFlow.StartNewGame();
        }
    }
}
