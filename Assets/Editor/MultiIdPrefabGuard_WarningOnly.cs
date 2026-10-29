using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class MultiIdPrefabGuard_ExactChange : AssetModificationProcessor
{
    // 用 EditorPrefs 或静态字典缓存上次检查的ID值
    // 注意：静态变量在编辑器重新编译后会丢失，适合用作临时缓存
    private static Dictionary<string, string> _lastIdCache = new Dictionary<string, string>();

    static string[] OnWillSaveAssets(string[] paths)
    {
        foreach (string assetPath in paths)
        {
            if (!assetPath.EndsWith(".prefab"))
                continue;

            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabAsset == null)
                continue;

            // 构建当前所有ID的快照
            var currentIds = new Dictionary<string, string>();
            var allMono = prefabAsset.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            int index = 0;
            foreach (var mb in allMono)
            {
                SerializedObject so = new SerializedObject(mb);

                string key = $"{assetPath}_{index}";

                SerializedProperty pSaveable = so.FindProperty("saveableID");
                if (pSaveable != null) currentIds[key + "_saveable"] = pSaveable.stringValue;

                SerializedProperty pEnemy = so.FindProperty("enemySaveID");
                if (pEnemy != null) currentIds[key + "_enemy"] = pEnemy.stringValue;

                SerializedProperty pPickup = so.FindProperty("pickupSaveID");
                if (pPickup != null) currentIds[key + "_pickup"] = pPickup.stringValue;

                index++;
            }

            // 对比缓存，检测是否有ID被修改（包括新增或删除）
            bool hasIdChanged = false;
            foreach (var kv in currentIds)
            {
                if (_lastIdCache.TryGetValue(kv.Key, out string oldValue))
                {
                    if (oldValue != kv.Value && !string.IsNullOrWhiteSpace(kv.Value))
                    {
                        hasIdChanged = true;
                        break;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(kv.Value))
                {
                    // 新增了ID值
                    hasIdChanged = true;
                    break;
                }
            }

            // 更新缓存
            _lastIdCache = currentIds;

            if (hasIdChanged)
            {
                EditorUtility.DisplayDialog(
                    "⚠警告！检测到存档ID被修改！",
                    $"预制体文件：{Path.GetFileName(assetPath)}\n" +
                    "检测到有存档ID的值发生了变更（新增/修改），预制体模板不允许保存这些ID！\n\n" +
                    "请立即按 Ctrl+Z 撤销修改！",
                    "知道了，我手动处理"
                );
            }
        }
        return paths;
    }
}