using ArionDigital;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

public class ZombieLootSave : SaveableEntity
{
    [Header("丧尸掉落配置，物品预制体与ItemData绑定")]
    public List<LootPrefabBind> lootBindList;

    [Header("掉落生成位置点（可拖骨骼/自身Transform）")]
    public Transform lootSpawnPoint;

    // 当前这只丧尸实例化出来的所有掉落物根对象
    private List<GameObject> spawnedLootRoot = new List<GameObject>();

    private ZombieHealth _zombieHealth;

    protected override void Start()
    {
        base.Start();
        _zombieHealth = GetComponent<ZombieHealth>();
    }

    /// <summary>
    /// 由 ZombieHealth.OnDeath 死亡回调调用，生成丧尸掉落
    /// </summary>
    public void SpawnZombieLoot()
    {
        DestroyOwnLoot();

        if (lootBindList == null || lootBindList.Count == 0)
            return;

        // ============这里替换成你自己的权重随机逻辑，示例仅为演示============
        int selectIndex = 0;
        LootPrefabBind selectedBind = lootBindList[selectIndex];
        // =================================================================

        GameObject lootObj = Instantiate(selectedBind.prefab,
            lootSpawnPoint.position, lootSpawnPoint.rotation);

        ItemPickup pickup = lootObj.GetComponentInChildren<ItemPickup>();
        if (pickup != null)
        {
            pickup.belongBoxSaveID = saveableID;
            pickup.itemData = selectedBind.itemData;
            pickup.itemCount = 1;
        }

        spawnedLootRoot.Add(lootObj);
    }

    /// <summary>
    /// 销毁本丧尸生成的全部掉落物；复活时调用
    /// </summary>
    public void DestroyOwnLoot()
    {
        foreach (var root in spawnedLootRoot)
        {
            if (root != null)
                Destroy(root);
        }
        spawnedLootRoot.Clear();
    }

    #region 存档读写
    public override void SaveData()
    {
        List<CrateLootDto> dtoList = new List<CrateLootDto>();

        // 仅丧尸死亡状态才保存掉落
        if (_zombieHealth != null && _zombieHealth.isDead())
        {
            foreach (var rootObj in spawnedLootRoot)
            {
                // 空保护：掉落物已经被销毁则跳过
                if (rootObj == null)
                    continue;

                ItemPickup pickup = rootObj.GetComponentInChildren<ItemPickup>();
                if (pickup == null)
                    continue;

                CrateLootDto dto = new CrateLootDto();
                dto.itemName = pickup.itemData.itemName;
                dto.posX = rootObj.transform.position.x;
                dto.posY = rootObj.transform.position.y;
                dto.posZ = rootObj.transform.position.z;
                dto.rotX = rootObj.transform.rotation.x;
                dto.rotY = rootObj.transform.rotation.y;
                dto.rotZ = rootObj.transform.rotation.z;
                dto.rotW = rootObj.transform.rotation.w;
                dto.itemCount = pickup.itemCount;
                dto.IsPickedUp = !rootObj.activeSelf;

                dtoList.Add(dto);
            }
        }

        string lootJson = JsonConvert.SerializeObject(dtoList);
        SaveSystem.SaveEntity(saveableID, "zombieLootJson", lootJson);
    }

    public override void LoadData()
    {
        string lootJson = SaveSystem.LoadEntity(saveableID, "zombieLootJson", "");
        DestroyOwnLoot();

        if (string.IsNullOrEmpty(lootJson))
            return;

        var dtoList = JsonConvert.DeserializeObject<List<CrateLootDto>>(lootJson);
        foreach (var dto in dtoList)
        {
            // 已经拾取过，跳过实例化
            if (dto.IsPickedUp)
                continue;

            GameObject targetPrefab = null;
            ItemData bindItemData = null;
            foreach (var bind in lootBindList)
            {
                if (bind.itemData != null && bind.itemData.itemName == dto.itemName)
                {
                    targetPrefab = bind.prefab;
                    bindItemData = bind.itemData;
                    break;
                }
            }
            if (targetPrefab == null)
                continue;

            Vector3 pos = new Vector3(dto.posX, dto.posY, dto.posZ);
            Quaternion rot = new Quaternion(dto.rotX, dto.rotY, dto.rotZ, dto.rotW);
            GameObject lootObj = Instantiate(targetPrefab, pos, rot);

            ItemPickup pickup = lootObj.GetComponentInChildren<ItemPickup>();
            if (pickup != null)
            {
                pickup.belongBoxSaveID = saveableID;
                pickup.itemData = bindItemData;
                pickup.itemCount = dto.itemCount;
                spawnedLootRoot.Add(lootObj);
            }
        }
    }
    #endregion
}
