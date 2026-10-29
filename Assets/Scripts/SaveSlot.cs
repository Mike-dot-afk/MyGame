using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class SaveSlot : MonoBehaviour
{
    public int slotIndex;
    private TextMeshProUGUI _slotText;

    void Awake()
    {
        Button btn = GetComponentInChildren<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnLoadClicked);
        }
        _slotText = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void SetSlotTitle()
    {
        if (_slotText != null)
        {
            _slotText.text = $"´æµµ{slotIndex}";
        }
    }

    void OnLoadClicked()
    {
        string saveID = "Slot_" + slotIndex;
        SaveUIManager.Instance.CloseUI();
        SaveSystem.LoadGame(saveID);
    }
}