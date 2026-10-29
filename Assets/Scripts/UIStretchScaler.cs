using UnityEngine;

public class UIStretchScaler : MonoBehaviour
{
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    private int lastWidth;
    private int lastHeight;

    void Start()
    {
        Apply();
    }

    void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            Apply();
        }
    }

    void Apply()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        RectTransform rt = transform as RectTransform;
        if (rt == null) return;
        float sx = Screen.width / referenceResolution.x;
        float sy = Screen.height / referenceResolution.y;
        rt.localScale = new Vector3(sx, sy, 1f);
    }
}
