using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class ItemPickupIdChecker : EditorWindow
{
    private const string TargetScriptTypeName = "ItemPickup";
    private const string TargetFieldName = "pickupSaveID";

    private static FieldInfo GetTargetField(MonoBehaviour comp, string fieldName)
    {
        Type t = comp.GetType();
        while (t != null && t != typeof(MonoBehaviour))
        {
            var f = t.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
                return f;
            t = t.BaseType;
        }
        return null;
    }

    [MenuItem("Tools/物品存档ID工具/查找pickupSaveID为空物体")]
    public static void FindEmptyPickupId()
    {
        List<MonoBehaviour> emptyIdComponents = new List<MonoBehaviour>();
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(includeInactive: true);
        foreach (GameObject go in allObjects)
        {
            MonoBehaviour[] comps = go.GetComponents<MonoBehaviour>();
            foreach (var comp in comps)
            {
                if (comp.GetType().Name != TargetScriptTypeName) continue;
                FieldInfo fi = GetTargetField(comp, TargetFieldName);
                if (fi == null)
                {
                    Debug.LogWarning($"[{go.name}] 找不到字段 {TargetFieldName}", go);
                    continue;
                }
                string idVal = (string)fi.GetValue(comp);
                if (string.IsNullOrWhiteSpace(idVal))
                {
                    emptyIdComponents.Add(comp);
                    Debug.Log($"空ID组件：[{go.name}] 组件:{comp.GetType().Name}", comp);
                }
            }
        }
        if (emptyIdComponents.Count == 0)
        {
            Debug.Log("扫描完成：所有ItemPickup都已有pickupSaveID");
            return;
        }
        Debug.Log($"===== 找到 {emptyIdComponents.Count} 个组件pickupSaveID为空 =====");
    }

    [MenuItem("Tools/物品存档ID工具/【一键填充空pickupSaveID】仅空ID生成GUID，不改动已有ID")]
    public static void AutoFillEmptyPickupId()
    {
        int filledCount = 0;
        int skipExistCount = 0;
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(includeInactive: true);
        foreach (GameObject go in allObjects)
        {
            MonoBehaviour[] comps = go.GetComponents<MonoBehaviour>();
            foreach (var comp in comps)
            {
                if (comp.GetType().Name != TargetScriptTypeName) continue;
                FieldInfo fi = GetTargetField(comp, TargetFieldName);
                if (fi == null)
                {
                    Debug.LogWarning($"[{go.name}] 找不到{TargetFieldName}字段", go);
                    continue;
                }
                string currentId = (string)fi.GetValue(comp);
                if (string.IsNullOrWhiteSpace(currentId))
                {
                    string newGuid = Guid.NewGuid().ToString();
                    fi.SetValue(comp, newGuid);
                    EditorUtility.SetDirty(comp);
                    filledCount++;
                    Debug.Log($"自动分配pickupSaveID：{go.name} -> {newGuid}", comp);
                }
                else
                {
                    skipExistCount++;
                }
            }
        }
        Debug.Log($"批量处理完成：\n填充空ID组件数量：{filledCount}\n跳过已有ID组件数量：{skipExistCount}");
        EditorSceneManager.MarkAllScenesDirty();
    }

    [MenuItem("Tools/物品存档ID工具/检测重复pickupSaveID")]
    public static void CheckDuplicatePickupId()
    {
        Dictionary<string, List<MonoBehaviour>> idDict = new Dictionary<string, List<MonoBehaviour>>();
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(includeInactive: true);
        foreach (GameObject go in allObjects)
        {
            MonoBehaviour[] comps = go.GetComponents<MonoBehaviour>();
            foreach (var comp in comps)
            {
                if (comp.GetType().Name != TargetScriptTypeName) continue;
                FieldInfo fi = GetTargetField(comp, TargetFieldName);
                if (fi == null) continue;
                string id = (string)fi.GetValue(comp);
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!idDict.ContainsKey(id))
                    idDict[id] = new List<MonoBehaviour>();
                idDict[id].Add(comp);
            }
        }
        int dupCount = 0;
        foreach (var kv in idDict)
        {
            if (kv.Value.Count > 1)
            {
                dupCount++;
                var names = string.Join(" , ", kv.Value.ConvertAll(c => $"{c.gameObject.name}({c.GetType().Name})"));
                Debug.LogError($"重复pickupSaveID [{kv.Key}]，组件列表：{names}", kv.Value[0]);
            }
        }
        if (dupCount == 0)
        {
            Debug.Log("未检测出重复pickupSaveID");
        }
        else
        {
            Debug.LogError($"一共检测到 {dupCount} 组重复pickupSaveID！复制物体产生的，选中组件重新生成ID");
        }
    }
}
