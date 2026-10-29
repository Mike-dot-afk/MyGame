using UnityEngine;
using UnityEngine.UI;

public class HPBarUI : MonoBehaviour
{
    [SerializeField] private Image hpFill;

    // 由外部调用，更新血条填充量
    public void UpdateHP(int currentHp, int maxHp)
    {
        hpFill.fillAmount = (float)currentHp / maxHp;
    }
}