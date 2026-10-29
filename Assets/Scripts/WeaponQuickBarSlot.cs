using UnityEngine;
using UnityEngine.UI;

public class WeaponQuickBarSlot : MonoBehaviour
{
    [SerializeField] private int _slotIndex;
    private ItemData _currentItem;
    private Image _slotImage;

    [SerializeField] private Image _icon;
    [SerializeField] private GameObject _dragIconContainer;
    private RectTransform _dragIconRect;

    private CanvasGroup _canvasGroup;

    void Awake()
    {
        _slotImage = GetComponent<Image>();
        _canvasGroup = GetComponent<CanvasGroup>();

        if (_icon == null) _icon = GetComponentInChildren<Image>();
        if (_dragIconContainer == null)
        {
            Transform t = transform.Find("DragIcon");
            if (t != null) { _dragIconContainer = t.gameObject; }
        }
        if (_dragIconContainer != null && _dragIconRect == null)
        {
            _dragIconRect = _dragIconContainer.GetComponent<RectTransform>();
            if (_dragIconRect == null) { _dragIconRect = _dragIconContainer.gameObject.AddComponent<RectTransform>(); }
        }
    }

    // 完整属性：可读可写
    public ItemData currentItem
    {
        get => _currentItem;
        set
        {
            _currentItem = value;
            SetItem(value); // 数据变了自动同步UI
        }
    }
    public int slotIndex => _slotIndex;

    public void SetItem(ItemData item)
    {
        _currentItem = item;

        if (item != null)
        {
            _icon.sprite = item.icon;
            _icon.enabled = true;
        }
        else
        {
            _icon.sprite = null;
            _icon.enabled = false;
        }
    }

    public void EquipItem(ItemData item)
    {
        currentItem = item; // 用属性set自动同步
    }

    public void ClearSlot()
    {
        currentItem = null; // 用属性set自动同步
    }

    public Image icon => _icon;
    public RectTransform DragIconRect => _dragIconRect;
    public GameObject DragIconContainer => _dragIconContainer;
}