using UnityEngine;

public class DistanceActivator : MonoBehaviour
{
    [Header("目标设置")]
    [SerializeField] private GameObject targetObject;        // 要激活的目标物体
    [SerializeField] private float activateDistance = 3f;    // 激活距离
    [SerializeField] private string playerTag = "Player";    // 玩家标签

    [Header("可选设置")]
    [SerializeField] private bool deactivateWhenLeave = false; // 离开范围时是否关闭
    [SerializeField] private bool checkEveryFrame = true;     // 是否每帧检测

    private GameObject player;
    private bool isActive = false;

    void Start()
    {
        // 查找玩家
        player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            Debug.LogWarning($"[DistanceActivator] 找不到标签为 '{playerTag}' 的物体");
            return;
        }

        // 确保目标物体初始状态正确
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[DistanceActivator] 目标物体未赋值");
        }
    }

    void Update()
    {
        if (checkEveryFrame && player != null && targetObject != null)
        {
            CheckDistance();
        }
    }

    void CheckDistance()
    {
        float distance = Vector3.Distance(transform.position, player.transform.position);
        bool shouldBeActive = distance <= activateDistance;

        // 只有在状态需要改变时才执行 SetActive，避免性能浪费
        if (shouldBeActive && !isActive)
        {
            targetObject.SetActive(true);
            isActive = true;
        }
        else if (!shouldBeActive && isActive && deactivateWhenLeave)
        {
            targetObject.SetActive(false);
            isActive = false;
        }
    }

    // 可视化辅助（在Scene视图中显示检测范围）
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, activateDistance);
    }
}