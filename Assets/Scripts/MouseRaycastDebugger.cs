using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MouseRaycastDebugger : MonoBehaviour
{
    void Update()
    {
        // 只在鼠标按下时检测（也可以去掉if，每帧都检测）
        if (Input.GetMouseButtonDown(0))
        {
            CheckMouseRaycast();
        }
    }

    void CheckMouseRaycast()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        Debug.Log("=== 【鼠标射线检测结果】 ===");
        if (results.Count == 0)
        {
            Debug.Log(" 射线没有命中任何 UI 物体！");
            return;
        }

        // 按渲染顺序从前往后打印（最上面的物体最先被命中）
        for (int i = 0; i < results.Count; i++)
        {
            RaycastResult result = results[i];
            GameObject hitObj = result.gameObject;
            Debug.Log($"命中 [{i + 1}]: {hitObj.name} (层级: {hitObj.layer}, 组件: {string.Join(", ", GetComponentNames(hitObj))})");
        }
        Debug.Log("===========================");
    }

    // 辅助方法：获取物体上的所有组件名称
    string GetComponentNames(GameObject obj)
    {
        Component[] components = obj.GetComponents<Component>();
        List<string> compNames = new List<string>();
        foreach (Component comp in components)
        {
            compNames.Add(comp.GetType().Name);
        }
        return string.Join(", ", compNames);
    }
}