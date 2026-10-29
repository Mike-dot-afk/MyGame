using ArionDigital;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct CrateLootDto
{
    public string itemName;
    public float posX;
    public float posY;
    public float posZ;

    public float rotX;
    public float rotY;
    public float rotZ;
    public float rotW;

    public int itemCount;
    public bool IsPickedUp;
}

[System.Serializable]
public struct CrateLootSaveData
{
    // 删掉原来 public int prefabIndex;
    public string itemName;      // 新增：用来匹配lootBindList
    public Vector3 spawnPos;
    public Quaternion spawnRot;
    public ItemData itemData;
    public int itemCount;
    public bool IsPickedUp;
}

// 新增掉落配置结构
[System.Serializable]
public class LootPrefabBind
{
    public GameObject prefab;
    public ItemData itemData;
    [Tooltip("该物品背包理论最大持有量，用于计算存量惩罚")]
    public int maxItemHold;
}

public class CrateLootSave : SaveableEntity
{
    [Header("掉落配置")]
    public List<LootPrefabBind> lootBindList;
    public Transform lootSpawnPoint;
    [Header("概率参数")]
    public float grenadeBaseWeight = 0.08f;
    [Range(0f, 1f)] public float inventoryPenaltyMax = 0.7f;
    // 绑定同物体的箱子核心逻辑脚本
    private CrashCrate crashCrate;
    private Inventory playerInventory;
    // 只存储每次实例化出来的顶层根物体，解决子物体挂载销毁不全问题
    private List<GameObject> spawnedLootRoot = new List<GameObject>();
    // 记录本次随机选中的预制体下标
    private int currentSelectPrefabIndex;

    protected override void Start()
    {
        base.Start();
        crashCrate = GetComponent<CrashCrate>();
        playerInventory = Inventory.instance;
    }

    // 销毁本箱子所有动态掉落
    private void DestroyOwnLoot()
    {
        foreach (var rootObj in spawnedLootRoot)
        {
            if (rootObj != null)
                Destroy(rootObj);
        }
        spawnedLootRoot.Clear();
    }

    // 外部调用：箱子破碎时生成掉落，给CrashCrate调用
    public void SpawnBoxLoot()
    {
        DestroyOwnLoot();
        int selectIndex = GetDynamicWeightIndex();
        currentSelectPrefabIndex = selectIndex;
        GameObject selectPrefab = lootBindList[selectIndex].prefab;
        GameObject lootObj = Instantiate(selectPrefab, lootSpawnPoint.position, lootSpawnPoint.rotation);
        ItemPickup pickup = lootObj.GetComponentInChildren<ItemPickup>();
        if (pickup != null)
        {
            pickup.belongBoxSaveID = saveableID;
            pickup.itemData = lootBindList[selectIndex].itemData;
        }
        spawnedLootRoot.Add(lootObj);
    }

    // 根据背包动态权重获取选中下标
    private int GetDynamicWeightIndex()
    {
        if (lootBindList.Count == 1)
            return 0;
        Dictionary<string, int> itemOwned = CountBagItemByName();
        List<(int index, float weight)> weightList = new List<(int index, float weight)>();
        for (int i = 0; i < lootBindList.Count; i++)
        {
            LootPrefabBind bind = lootBindList[i];
            ItemData data = bind.itemData;
            float weight = 1f;
            int have = itemOwned.ContainsKey(data.itemName) ? itemOwned[data.itemName] : 0;
            if (data.itemName == "手雷")
            {
                weight = grenadeBaseWeight;
                float fill = (float)have / bind.maxItemHold;
                float penalty = 1 - fill * inventoryPenaltyMax;
                weight *= Mathf.Max(penalty, 0.05f);
            }
            else
            {
                float fill = (float)have / bind.maxItemHold;
                float penalty = 1 - fill * inventoryPenaltyMax;
                weight *= Mathf.Max(penalty, 0.05f);
            }
            weightList.Add((i, weight));
        }
        float total = 0;
        foreach (var w in weightList) total += w.weight;
        float roll = Random.Range(0f, total);
        float cur = 0;
        foreach (var pair in weightList)
        {
            cur += pair.weight;
            if (roll <= cur)
                return pair.index;
        }
        return 0;
    }

    // 统计背包每种物品数量
    private Dictionary<string, int> CountBagItemByName()
    {
        Dictionary<string, int> dict = new Dictionary<string, int>();
        foreach (var slot in playerInventory.slots)
        {
            if (slot.currentItem == null) continue;
            string name = slot.currentItem.itemName;
            if (dict.ContainsKey(name))
                dict[name] += slot.count;
            else
                dict[name] = slot.count;
        }
        return dict;
    }

    #region 存档读写
    public override void SaveData()
    {
        // 同步读取箱子当前破碎状态
        SaveSystem.SaveEntity(saveableID, "IsBroken", crashCrate.isBroken);
        SaveSystem.SaveEntity(saveableID, "FadedOut", crashCrate.crateFadedOut);

        List<CrateLootSaveData> lootDatas = new List<CrateLootSaveData>();
        if (crashCrate.isBroken)
        {
            foreach (var rootObj in spawnedLootRoot)
            {
                ItemPickup pickup = rootObj.GetComponentInChildren<ItemPickup>();
                if (pickup == null) continue;
                CrateLootSaveData data;
                data.itemName = pickup.itemData.itemName;
                data.spawnPos = rootObj.transform.position;
                data.spawnRot = rootObj.transform.rotation;
                data.itemData = null;
                data.itemCount = pickup.itemCount;
                data.IsPickedUp = !rootObj.activeSelf;
                lootDatas.Add(data);
            }
        }

        List<CrateLootDto> dtoList = new List<CrateLootDto>();
        foreach (var src in lootDatas)
        {
            CrateLootDto dto = new CrateLootDto();
            dto.itemName = src.itemName;
            dto.posX = src.spawnPos.x;
            dto.posY = src.spawnPos.y;
            dto.posZ = src.spawnPos.z;

            dto.rotX = src.spawnRot.x;
            dto.rotY = src.spawnRot.y;
            dto.rotZ = src.spawnRot.z;
            dto.rotW = src.spawnRot.w;

            dto.itemCount = src.itemCount;
            dto.IsPickedUp = src.IsPickedUp;
            dtoList.Add(dto);
        }

        string lootJson = JsonConvert.SerializeObject(dtoList);
        SaveSystem.SaveEntity(saveableID, "lootJson", lootJson);
    }

    public override void LoadData()
    {
        bool crateBroken_raw = SaveSystem.LoadEntity(saveableID, "IsBroken", false);
        bool isFaded_raw = SaveSystem.LoadEntity(saveableID, "FadedOut", false);
        string lootJson = SaveSystem.LoadEntity(saveableID, "lootJson", "");

        List<CrateLootSaveData> loadLoot_raw = new List<CrateLootSaveData>();
        if (!string.IsNullOrEmpty(lootJson))
        {
            var dtoList = JsonConvert.DeserializeObject<List<CrateLootDto>>(lootJson);
            foreach (var dto in dtoList)
            {
                CrateLootSaveData data = new CrateLootSaveData();
                data.itemName = dto.itemName;
                data.spawnPos = new Vector3(dto.posX, dto.posY, dto.posZ);
                data.spawnRot = new Quaternion(dto.rotX, dto.rotY, dto.rotZ, dto.rotW);
                data.itemCount = dto.itemCount;
                data.IsPickedUp = dto.IsPickedUp;
                data.itemData = null;
                loadLoot_raw.Add(data);
            }
        }

        DestroyOwnLoot();
        bool crateBroken = crateBroken_raw;
        bool isFaded = isFaded_raw;
        crashCrate.isBroken = crateBroken;
        crashCrate.crateFadedOut = isFaded;

        if (crateBroken)
        {
            crashCrate.wholeCrate.enabled = false;
            crashCrate.boxCollider.enabled = false;
            crashCrate.fracturedCrate.SetActive(!isFaded);
        }
        else
        {
            crashCrate.wholeCrate.enabled = true;
            crashCrate.boxCollider.enabled = true;
            crashCrate.fracturedCrate.SetActive(false);
            crashCrate.ResetCrateState();
        }

        // 重建掉落，原有业务逻辑完全不变
        if (loadLoot_raw.Count > 0)
        {
            foreach (var data in loadLoot_raw)
            {
                if (data.IsPickedUp)
                    continue;
                GameObject targetPrefab = null;
                ItemData bindItemData = null;
                foreach (var bind in lootBindList)
                {
                    if (bind.itemData != null && bind.itemData.itemName == data.itemName)
                    {
                        targetPrefab = bind.prefab;
                        bindItemData = bind.itemData;
                        break;
                    }
                }
                if (targetPrefab == null)
                {
                    continue;
                }
                GameObject lootObj = Instantiate(targetPrefab, data.spawnPos, data.spawnRot);
                ItemPickup pickup = lootObj.GetComponentInChildren<ItemPickup>();
                if (pickup != null)
                {
                    pickup.belongBoxSaveID = saveableID;
                    pickup.itemData = bindItemData;
                    pickup.itemCount = data.itemCount;
                    spawnedLootRoot.Add(lootObj);
                }
            }
        }
    }
    #endregion
}