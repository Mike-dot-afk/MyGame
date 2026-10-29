using UnityEngine;
// 物品类型
public enum ItemType
{
    Gun,      // 枪械
    Knife,    // 刀具
    Consume,  // 消耗品
    Bag,      // 背包扩容
    KeyItem,  // 关键道具
    Material  // 材料
}
// 右键可直接创建物品
[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/ItemData")]
public class ItemData : ScriptableObject
{
    [Header("基础信息")]
    public string itemName;
    public Sprite icon;
    public ItemType itemType;

    [Header("钥匙专用：只有KeyItem生效")]
    public string keyId; // 和Door.requireKeyId配对

    [Header("规则")]
    public bool canDiscard = true;  // 是否可丢弃
    public bool canQuickUse = true; // 是否可放入快捷栏
    public bool canStack = true;    // 是否可堆叠
    [Header("枪械配置")]
    public ItemData ammoType;  // 这把枪使用的子弹（仅枪械填充）
}
