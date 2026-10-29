using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class SaveUIManager : MonoBehaviour
{
    public static SaveUIManager Instance;
    [Header("UI引用")]
    public GameObject saveUIPanel;
    public Transform content;
    public GameObject saveSlotPrefab;
    public int maxSlots = 10;
    [Header("全局UI管理器引用")]
    public UIManager globalUIManager;

    //内存：新建存档时使用，不再从磁盘元数据读取；每次RefreshSaveSlots会重新计算
    private int totalSlotCount = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        saveUIPanel.SetActive(false);
        if (globalUIManager != null)
        {
            globalUIManager.SetUIMode(false);
        }
        //不再加载Meta_SaveSlotList元数据！彻底移除这套逻辑
    }

    #region 存档面板控制
    public void OpenUI()
    {
        saveUIPanel.SetActive(true);
        globalUIManager.isSaveUIOpen = true;
        if (globalUIManager != null)
        {
            globalUIManager.SetUIMode(true);
        }
        //打开面板就扫描磁盘真实key重建UI
        RefreshSaveSlots();
    }

    public void CloseUI()
    {
        saveUIPanel.SetActive(false);
        globalUIManager.isSaveUIOpen = false;
        if (globalUIManager != null)
        {
            globalUIManager.SetUIMode(false);
        }
    }

    public void ToggleUI()
    {
        if (saveUIPanel.activeSelf)
            CloseUI();
        else
            OpenUI();
    }
    #endregion

    #region 存档槽位管理
    /// <summary>
    /// 【真相源：扫描磁盘全部key】
    /// 提取所有Slot_N，过滤内部槽TempSave/CheckpointSave；
    /// 同时更新内存totalSlotCount = 最大编号 +1，用于下一次新建存档
    /// </summary>
    public void RefreshSaveSlots()
    {
        ClearAllSlots();

        string[] allKeys = SaveFileManager.Instance.GetAllStoredDataIDs();
        HashSet<int> existSlotNumbers = new HashSet<int>();
        Regex regex = new Regex(@"^Slot_(\d+)_");

        foreach (string key in allKeys)
        {
            //过滤内部临时存档槽
            if (key.StartsWith("TempSave_") || key.StartsWith("CheckpointSave_"))
                continue;

            var match = regex.Match(key);
            if (match.Success)
            {
                int num = int.Parse(match.Groups[1].Value);
                existSlotNumbers.Add(num);
            }
        }

        //根据已存在编号，算出下一次新建存档的编号
        if (existSlotNumbers.Count > 0)
        {
            totalSlotCount = 0;
            foreach (var n in existSlotNumbers)
            {
                if (n > totalSlotCount) totalSlotCount = n;
            }
            totalSlotCount += 1;
        }
        else
        {
            //一个玩家存档都不存在，新建从1开始
            totalSlotCount = 1;
        }

        //排序生成UI按钮
        List<int> slotNumList = new List<int>(existSlotNumbers);
        slotNumList.Sort();
        foreach (int slotNum in slotNumList)
        {
            SpawnExistingSlotUI(slotNum);
        }
    }

    /// <summary>从扫描得到的编号生成UI槽物体（读取旧存档）</summary>
    private void SpawnExistingSlotUI(int slotIndex)
    {
        if (saveSlotPrefab == null || content == null) return;
        GameObject newSlot = Instantiate(saveSlotPrefab, content);
        SaveSlot slotComp = newSlot.GetComponent<SaveSlot>();
        if (slotComp != null)
        {
            slotComp.slotIndex = slotIndex;
            slotComp.SetSlotTitle();
        }
    }

    /// <summary>用户点击新建存档按钮，生成新UI物体</summary>
    public void CreateSaveSlot()
    {
        if (saveSlotPrefab == null || content == null) return;
        GameObject newSlot = Instantiate(saveSlotPrefab, content);
        newSlot.transform.SetAsFirstSibling();

        SaveSlot slotComp = newSlot.GetComponent<SaveSlot>();
        if (slotComp != null)
        {
            slotComp.slotIndex = totalSlotCount;
            slotComp.SetSlotTitle();
        }

        if (content.childCount > maxSlots)
        {
            Destroy(content.GetChild(content.childCount - 1).gameObject);
        }
    }

    public void ClearAllSlots()
    {
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
    }
    #endregion

    #region UI按钮事件
    public void OnSaveButtonClicked()
    {
        CreateSaveSlot();
        GameObject lastSlot = content.GetChild(0).gameObject;
        SaveSlot slotComp = lastSlot.GetComponent<SaveSlot>();
        if (slotComp != null)
        {
            string saveID = "Slot_" + slotComp.slotIndex;
            SaveSystem.SaveGame(saveID);
            SaveSystem.SaveGame(SaveSystem.CheckpointSaveSlot);

            //保存完成立刻扫描磁盘刷新UI列表
            RefreshSaveSlots();
        }
    }
    #endregion
}