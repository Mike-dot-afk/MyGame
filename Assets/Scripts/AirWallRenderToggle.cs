using UnityEngine;

public class AirWallRenderToggle : MonoBehaviour
{
    [Tooltip("烘焙前勾选打开全部子物体MeshRenderer，烘焙完成取消勾选")]
    public bool enableChildRenderers;

    private MeshRenderer[] childRenderers;

    void OnValidate()
    {
        // 在Inspector修改布尔值就立刻生效，不需要运行游戏
        childRenderers = GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        foreach (var r in childRenderers)
        {
            r.enabled = enableChildRenderers;
        }
    }
}
