using UnityEngine;

[CreateAssetMenu(fileName = "New Item Pack", menuName = "Inventory/Item Pack")]
public class ItemPack : ScriptableObject
{
    public ItemData[] items; // 批量添加的物品列表
}

public class TestPickup : MonoBehaviour
{
    public ItemPack itemPack; // 在Inspector中拖入物品包

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            AddItemsToInventory();
        }
    }

    private void AddItemsToInventory()
    {
        if (Inventory.instance == null || itemPack == null || itemPack.items.Length == 0)
        {
            //Debug.LogWarning("物品包为空或未分配！");
            return;
        }

        foreach (var item in itemPack.items)
        {
            Inventory.instance.AddItem(item);
        }
        //Debug.Log($"已添加 {itemPack.items.Length} 个物品到背包！");
    }
}