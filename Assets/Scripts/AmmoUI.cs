using UnityEngine;
using TMPro;

public class AmmoUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI ammoText;

    // 由外部调用，更新子弹显示
    public void UpdateAmmo(int currentAmmo, int remainAmmo)
    {
        ammoText.text = $"{currentAmmo} / {remainAmmo}";
    }
}