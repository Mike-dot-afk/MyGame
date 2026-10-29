using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;

public class SaveableIdChecker : EditorWindow
{
    private const string TargetBaseTypeName = "SaveableEntity";

    /// <summary>向上遍历继承链反射获取saveableID字段，兼容父类protected序列化字段</summary>
    private static FieldInfo GetSaveableIdField(MonoBehaviour comp)
    {
        Type t = comp.GetType();
        while (t != null && t != typeof(MonoBehaviour))
        {
            var f = t.GetField("saveableID",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
                return f;
            t = t.BaseType;
        }
        return null;
    }

    /// <summary>查找所有ID为空的SaveableEntity组件（按组件粒度扫描，解决同物体多组件漏填）</summary>
    [MenuItem("Tools/存档工具/查找SaveableID为空物体【修复版】")]
    public static void FindEmptySaveableId()
    {
        List<MonoBehaviour> emptyIdComponents = new List<MonoBehaviour>();
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(includeInactive: true);

        foreach (GameObject go in allObjects)
        {
            // 关键：遍历物体上全部MonoBehaviour组件实例
            MonoBehaviour[] allMono = go.GetComponents<MonoBehaviour>();
            foreach (var comp in allMono)
            {
                FieldInfo fi = GetSaveableIdField(comp);
                if (fi == null) continue; // 没有saveableID字段，跳过

                string idVal = (string)fi.GetValue(comp);
                if (string.IsNullOrWhiteSpace(idVal))
                {
                    emptyIdComponents.Add(comp);
                    Debug.Log($"空ID组件：[{comp.gameObject.name}] 组件:{comp.GetType().Name}", comp);
                }
            }
        }

        if (emptyIdComponents.Count == 0)
        {
            Debug.Log("扫描完成：所有SaveableEntity组件都已有saveableID");
            return;
        }
        Debug.Log($"===== 一共找到 {emptyIdComponents.Count} 个组件saveableID为空 =====");
    }

    /// <summary>一键填充空ID，**逐个组件处理，同物体多个SaveableEntity全部处理**</summary>
    [MenuItem("Tools/存档工具/【一键填充空SaveableID】修复版(支持同物体多组件)")]
    public static void AutoFillEmptySaveableId()
    {
        int filledCount = 0;
        int skipExistCount = 0;
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(includeInactive: true);

        foreach (GameObject go in allObjects)
        {
            MonoBehaviour[] allMono = go.GetComponents<MonoBehaviour>();
            foreach (var comp in allMono)
            {
                FieldInfo fi = GetSaveableIdField(comp);
                if (fi == null) continue;

                string currentId = (string)fi.GetValue(comp);
                if (string.IsNullOrWhiteSpace(currentId))
                {
                    string newGuid = Guid.NewGuid().ToString();
                    fi.SetValue(comp, newGuid);
                    EditorUtility.SetDirty(comp); //标记该组件实例脏，不是标记GameObject
                    filledCount++;
                    Debug.Log($"分配ID：物体[{go.name}] 组件:{comp.GetType().Name} → {newGuid}", comp);
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

    /// <summary>检测重复saveableID，按组件粒度</summary>
    [MenuItem("Tools/存档工具/检测重复SaveableID【修复版】")]
    public static void CheckDuplicateId()
    {
        Dictionary<string, List<MonoBehaviour>> idDict = new Dictionary<string, List<MonoBehaviour>>();
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(includeInactive: true);

        foreach (GameObject go in allObjects)
        {
            MonoBehaviour[] allMono = go.GetComponents<MonoBehaviour>();
            foreach (var comp in allMono)
            {
                FieldInfo fi = GetSaveableIdField(comp);
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
                Debug.LogError($"重复ID [{kv.Key}]，组件列表：{names}", kv.Value[0]);
            }
        }
        if (dupCount == 0)
        {
            Debug.Log("未检测出重复SaveableID");
        }
        else
        {
            Debug.LogError($"一共检测到 {dupCount} 组重复ID！复制物体产生的，选中组件重新生成ID");
        }
    }
}
