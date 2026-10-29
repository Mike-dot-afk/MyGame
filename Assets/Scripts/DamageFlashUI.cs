using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 受击血屏闪烁：全屏红色，中央椭圆区域透明露出游戏画面，椭圆边界渐淡，受击时闪一下淡出。
/// 受击音效独占播放：若上一个受击音还在播放则跳过本次，播完才允许下一个。
/// 用法：任意受击处调用 DamageFlashUI.Instance.Flash();
/// 挂载：场景中任意物体（建议新建空物体）。
/// 若在 Inspector 中指定 targetCanvas，则全屏图片挂入该 Canvas 并置于最顶层；
/// 未指定时自动创建独立 Canvas。
/// </summary>
public class DamageFlashUI : MonoBehaviour
{
    public static DamageFlashUI Instance { get; private set; }

    [SerializeField] private float flashDuration = 0.35f;
    [SerializeField] private Color flashColor = new Color(0.72f, 0f, 0f, 1f);
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private AudioClip[] hitSounds;
    [SerializeField] private float hitSoundVolume = 1f;

    private Image flashImage;
    private AudioSource audioSource;
    private Coroutine flashRoutine;

    private void Awake()
    {
        Instance = this;
        SetupAudioSource();
        CreateFlashImage();
    }

    private void SetupAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }

    private void CreateFlashImage()
    {
        Transform parent;
        if (targetCanvas != null)
        {
            parent = targetCanvas.transform;
        }
        else
        {
            // 未指定画布时自动创建独立的全屏 UI 画布，盖在所有界面之上
            GameObject canvasObj = new GameObject("DamageFlashCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            parent = canvasObj.transform;
        }

        GameObject imgObj = new GameObject("DamageFlashImage");
        imgObj.transform.SetParent(parent, false);
        imgObj.transform.SetAsLastSibling();
        flashImage = imgObj.AddComponent<Image>();
        flashImage.raycastTarget = false;
        flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

        RectTransform rt = flashImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        flashImage.sprite = CreateEllipseGradientSprite();
    }

    private Sprite CreateEllipseGradientSprite()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        float innerRadius = 0.32f;
        float outerRadius = 0.98f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - center) / center;
                float ny = (y - center) / center;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a;
                if (d <= innerRadius)
                {
                    a = 0f;
                }
                else if (d >= outerRadius)
                {
                    a = 1f;
                }
                else
                {
                    float t = (d - innerRadius) / (outerRadius - innerRadius);
                    a = t * t * (3f - 2f * t);
                }
                tex.SetPixel(x, y, new Color(1f, 0f, 0f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>受击时调用：闪一下然后淡出；受击音正在播则跳过本次音效</summary>
    public void Flash()
    {
        if (flashImage == null) return;
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }
        flashRoutine = StartCoroutine(FlashRoutine());
        PlayRandomHitSound();
    }

    private void PlayRandomHitSound()
    {
        if (hitSounds == null || hitSounds.Length == 0) return;
        if (audioSource != null && audioSource.isPlaying) return;
        AudioClip clip = hitSounds[Random.Range(0, hitSounds.Length)];
        if (clip == null) return;
        audioSource.PlayOneShot(clip, hitSoundVolume);
    }

    private IEnumerator FlashRoutine()
    {
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = 1f - elapsed / flashDuration;
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
            yield return null;
        }
        flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        flashRoutine = null;
    }
}