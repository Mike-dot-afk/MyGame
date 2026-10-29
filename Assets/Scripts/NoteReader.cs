using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NoteReader : MonoBehaviour
{
    [Header("引用")]
    public GameObject noteRootPanel;
    public TMP_Text contentText;

    [Header("纸条多页文本，一页一条")]
    public List<string> pages = new List<string>();

    // 状态
    private int _currentPageIndex;
    private bool _isNoteOpen;

    /// <summary>打开纸条，传入文本列表；如果传null使用Inspector配置pages</summary>
    public void OpenNote(List<string> customPages = null)
    {
        if (_isNoteOpen) return;
        if (customPages != null)
        {
            pages = customPages;
        }
        if (pages.Count == 0)
        {
            return;
        }

        //====新增：如果背包正在打开，强制把背包关闭，防止UI重叠====
        if (UIManager.Instance.bagUI.IsBagOpen())
        {
            UIManager.Instance.ToggleBagAndQuickBar();
        }

        _isNoteOpen = true;
        _currentPageIndex = 0;
        noteRootPanel.SetActive(true);
        RefreshPageDisplay();
        // ===== 锁定玩家输入，光标显示，并且暂停游戏时间（和背包SetUIMode逻辑对齐）=====
        StartGameFlow.LockPlayerInput();
        UIManager.Instance.isNoteReading = true;

        // 新增：读纸条时暂停游戏，打开光标
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>关闭纸条（放下纸条），外部可订阅OnNoteClosed事件触发闪电演出</summary>
    public void CloseNote()
    {
        if (!_isNoteOpen) return;
        _isNoteOpen = false;
        noteRootPanel.SetActive(false);
        // 解除UI封锁
        UIManager.Instance.isNoteReading = false;
        // 恢复玩家输入
        StartGameFlow.UnlockPlayerInput();

        //====新增：关闭纸条恢复游戏时间，恢复光标锁定（和背包关闭逻辑对齐）====
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        OnNoteClosed?.Invoke();
    }

    // 关闭纸条回调：用来触发闪电、僵尸移动演出
    public delegate void NoteClosedDelegate();
    public event NoteClosedDelegate OnNoteClosed;

    void Update()
    {
        if (!_isNoteOpen) return;

        // 下一页：鼠标左键 / Enter
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return))
        {
            NextPage();
        }
        // 关闭纸条：鼠标右键 / ESC
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CloseNote();
        }
    }

    void NextPage()
    {
        // 如果不是最后一页：翻页
        if (_currentPageIndex < pages.Count - 1)
        {
            _currentPageIndex++;
            RefreshPageDisplay();
        }
        else
        {
            // 已经是最后一页，按下确认直接关闭纸条（生化4行为）
            CloseNote();
        }
    }

    void RefreshPageDisplay()
    {
        string raw = pages[_currentPageIndex];
        // 把Inspector输入的文字"\\n"（两个字符）替换成真正换行符
        string realText = raw.Replace("\\n", "\n");
        contentText.text = realText;
    }

    // 读档/异常复位用
    public void ForceCloseNote()
    {
        if (_isNoteOpen)
            CloseNote();
    }
}
