using UnityEngine;
using System.Collections.Generic;
public class PickupNotifyManager : MonoBehaviour
{
    public static PickupNotifyManager Instance;
    public Transform notifyRoot;
    public PickupNotifyItem notifyItemPrefab;
    public int maxShowCount = 5; //最多5行
    private List<PickupNotifyItem> _itemList = new List<PickupNotifyItem>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    /// <summary>外部拾取物品时调用这个接口</summary>
    public void ShowNotify(string itemName, int count, Sprite itemIcon)
    {
        PickupNotifyItem newItem = Instantiate(notifyItemPrefab, notifyRoot);
        // 插入到容器第一个子节点，放到UI最上方
        newItem.transform.SetSiblingIndex(0);

        newItem.Setup(itemName, count, itemIcon);
        _itemList.Add(newItem);

        //超过最大行数，销毁最底部（列表第一个）
        while (_itemList.Count > maxShowCount)
        {
            PickupNotifyItem bottom = _itemList[0];
            _itemList.RemoveAt(0);
            Destroy(bottom.gameObject);
        }
    }

    void LateUpdate()
    {
        //清理已经被Destroy的空引用
        for (int i = _itemList.Count - 1; i >= 0; i--)
        {
            if (_itemList[i] == null)
            {
                _itemList.RemoveAt(i);
            }
        }
    }
}
