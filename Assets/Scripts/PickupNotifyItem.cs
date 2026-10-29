using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PickupNotifyItem : MonoBehaviour
{
    public TMP_Text textComp;
    public Image iconComp;
    public float lifeTime = 3f;

    private float _timer;

    public void Setup(string itemName, int count, Sprite icon)
    {
        iconComp.sprite = icon;
        if (count <= 1)
        {
            textComp.text = itemName;
        }
        else
        {
            textComp.text = $"{itemName} ¡Á{count}";
        }
        _timer = lifeTime;
    }

    void Update()
    {
        _timer -= Time.unscaledDeltaTime;
        if (_timer <= 0)
        {
            Destroy(gameObject);
        }
    }
}
