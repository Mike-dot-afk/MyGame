using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractManager : MonoBehaviour
{
    [Header("交互设置")]
    public float interactRange = 2f;
    public KeyCode interactKey = KeyCode.F;
    [Header("UI 设置 - 核心控制")]
    public Vector3 uiOffset = new(1.15f, 0f, 0f);
    public float uiScale = 0.01f;
    public Vector3 uiRotation = new(0f, 0f, 0f);
    public TMP_FontAsset chineseFont;

    private IInteractable currentInteractable;
    private GameObject currentPromptUI;
    private TextMeshProUGUI promptText;
    private GameObject _uiFollowTarget; //方案A新增：UI跟随目标物体

    void Update()
    {
        CheckInteractable();
        if (Input.GetKeyDown(interactKey) && currentInteractable != null)
        {
            currentInteractable.Interact();
        }
    }

    void CheckInteractable()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, interactRange);
        foreach (var col in cols)
        {
            if (col.TryGetComponent(out IInteractable interactable))
            {
                currentInteractable = interactable;
                GameObject targetRoot = col.transform.root.gameObject;
                CreatePromptUI(targetRoot);
                return;
            }
        }
        ClearPromptUI();
        currentInteractable = null;
    }

    void CreatePromptUI(GameObject target)
    {
        ClearPromptUI();
        currentPromptUI = new GameObject("InteractPrompt");

        //【方案A改动】不再设置父物体，UI作为独立世界物体
        //currentPromptUI.transform.SetParent(target.transform, false);

        InteractUIParams? customUi = currentInteractable.GetUIParams();
        Vector3 useOffset = customUi.HasValue ? customUi.Value.offset : uiOffset;
        Vector3 useRot = customUi.HasValue ? customUi.Value.rotation : uiRotation;
        float useScale = customUi.HasValue ? customUi.Value.scale : uiScale;

        // 将目标本地offset转为世界坐标赋值给UI位置
        currentPromptUI.transform.position = target.transform.TransformPoint(useOffset);

        Canvas canvas = currentPromptUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        currentPromptUI.AddComponent<GraphicRaycaster>();

        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(currentPromptUI.transform);
        textObj.transform.localPosition = Vector3.zero;
        promptText = textObj.AddComponent<TextMeshProUGUI>();

        promptText.rectTransform.sizeDelta = new Vector2(200, 50);
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.fontSize = 24;
        promptText.color = Color.white;
        if (chineseFont != null) promptText.font = chineseFont;

        currentPromptUI.transform.localScale = Vector3.one * useScale;

        _uiFollowTarget = target; //保存需要跟随的目标
        UpdatePromptText();
    }

    void UpdatePromptText()
    {
        if (currentInteractable == null || promptText == null) return;
        string promptContent = currentInteractable.GetInteractPrompt();
        bool isNoKeyEmpty = currentInteractable is INoKeyWhenPromptEmpty;
        if (isNoKeyEmpty && string.IsNullOrEmpty(promptContent))
        {
            promptText.text = "";
            return;
        }
        promptText.text = $"{interactKey} {promptContent}";
    }

    void ClearPromptUI()
    {
        if (currentPromptUI != null)
        {
            Destroy(currentPromptUI);
            currentPromptUI = null;
            promptText = null;
            _uiFollowTarget = null; //清空跟随目标引用
        }
    }

    void LateUpdate()
    {
        if (currentPromptUI != null && Camera.main != null && currentInteractable != null && _uiFollowTarget != null)
        {
            InteractUIParams? customUi = currentInteractable.GetUIParams();
            Vector3 useOffset = customUi.HasValue ? customUi.Value.offset : uiOffset;
            Vector3 useRot = customUi.HasValue ? customUi.Value.rotation : uiRotation;

            //每帧更新UI世界位置，跟随目标，offset依旧是目标物体本地坐标
            currentPromptUI.transform.position = _uiFollowTarget.transform.TransformPoint(useOffset);

            //只水平Y轴面向相机，WorldSpace Canvas Z朝向相机防止文字镜像
            Vector3 dirToCamera = Camera.main.transform.position - currentPromptUI.transform.position;
            dirToCamera.y = 0;
            currentPromptUI.transform.rotation = Quaternion.LookRotation(-dirToCamera);

            currentPromptUI.transform.Rotate(useRot, Space.Self);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
