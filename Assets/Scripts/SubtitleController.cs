using UnityEngine;
using TMPro;

public class SubtitleController : MonoBehaviour
{
    [Header("×ÖÄ»UIÒýÓÃ")]
    public GameObject subtitlePanel;
    public TMP_Text subtitleText;

    // ÏÔÊ¾×ÖÄ»
    public void ShowSubtitle(string content)
    {
        if (subtitlePanel != null)
        {
            subtitlePanel.SetActive(true);
        }
        if (subtitleText != null)
        {
            subtitleText.text = content;
        }
    }

    // Çå¿Õ²¢ÇÒÒþ²Ø×ÖÄ»
    public void HideSubtitle()
    {
        if (subtitlePanel != null)
        {
            subtitlePanel.SetActive(false);
        }
        if (subtitleText != null)
        {
            subtitleText.text = "";
        }
    }
}