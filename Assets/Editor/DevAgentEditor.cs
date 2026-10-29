using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class DevAgentEditor : EditorWindow
{
    // 对话普通文本字号控制，修改数字即可放大缩小
    private static GUIStyle chatTextStyle;
    private static GUIStyle ChatTextStyle
    {
        get
        {
            if (chatTextStyle == null)
            {
                chatTextStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                chatTextStyle.fontSize = 15; // 改这里调整对话文字整体字号
                chatTextStyle.wordWrap = true;
            }
            return chatTextStyle;
        }
    }

    // 代码块内文字字号
    private static GUIStyle codeTextStyle;
    private static GUIStyle CodeTextStyle
    {
        get
        {
            if (codeTextStyle == null)
            {
                codeTextStyle = new GUIStyle(EditorStyles.miniLabel);
                codeTextStyle.fontSize = 11; // 代码块字号
                codeTextStyle.wordWrap = true;
            }
            return codeTextStyle;
        }
    }
    private const string API_URL = "http://127.0.0.1:8080/game_dev_helper";
    private const string SESSION_ID = "rpg_project_01";
    private const string SCRIPTS_FOLDER = "Assets/Scripts";
    private const string CHAT_SAVE_NAME = "agent_local_history.json";

    private string inputText = "";
    private UnityWebRequest ongoingRequest;
    private Vector2 historyScrollPos;

    [System.Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;
        public long timeStamp;
    }

    [System.Serializable]
    public class ChatStorage
    {
        public List<ChatMessage> records = new List<ChatMessage>();
    }

    private ChatStorage localChatStorage;

    // 存档路径
    private string HistorySavePath
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, "UnityDevAgent");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, CHAT_SAVE_NAME);
        }
    }

    [MenuItem("开发助手/打开AI工具")]
    static void OpenWindow()
    {
        GetWindow<DevAgentEditor>("游戏开发AI助手");
    }

    void OnEnable()
    {
        LoadLocalChatHistory();
    }

    void OnGUI()
    {
        DrawHistoryOperationBar();
        EditorGUILayout.Space();

        // 输入区
        EditorGUILayout.LabelField("输入开发需求：");
        EditorGUILayout.HelpBox(
            "💡 提示：支持多个脚本文件，用逗号或顿号分隔（如：PlayerController.cs, EnemyAI.cs）",
            MessageType.Info
        );
        inputText = EditorGUILayout.TextArea(inputText, GUILayout.Height(100));

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(ongoingRequest != null);
        if (GUILayout.Button("发送需求", GUILayout.Height(35)))
        {
            SendRequest();
        }
        EditorGUI.EndDisabledGroup();
        if (GUILayout.Button("清空输入框", GUILayout.Height(35)))
        {
            inputText = "";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        DrawFullChatHistoryPanel();
    }

    void DrawHistoryOperationBar()
    {
        EditorGUILayout.BeginHorizontal("Box");
        EditorGUILayout.LabelField($"本地对话记录总数：{localChatStorage.records.Count}", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("清空所有本地历史", GUILayout.Width(120)))
        {
            bool ok = EditorUtility.DisplayDialog("确认清空", "仅清空Unity界面本地展示记录，后端memory.db不受影响", "确认", "取消");
            if (ok)
            {
                localChatStorage.records.Clear();
                SaveLocalChatHistory();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // 消息片段：区分普通文本 / 代码块
    private struct MsgSegment
    {
        public bool isCode;
        public string text;
    }

    // 将带```的完整消息，切分成【普通文本、代码块】片段列表
    private List<MsgSegment> SplitMessageToSegment(string source)
    {
        List<MsgSegment> res = new List<MsgSegment>();
        Regex codeBlockRegex = new Regex(@"```(.*?)\n([\s\S]*?)```", RegexOptions.Singleline);
        int pos = 0;
        foreach (Match m in codeBlockRegex.Matches(source))
        {
            // 代码块前面的普通文字
            if (m.Index > pos)
            {
                string plainRaw = source.Substring(pos, m.Index - pos);
                string plainClean = FormatResponse(plainRaw);
                if (!string.IsNullOrEmpty(plainClean))
                {
                    res.Add(new MsgSegment { isCode = false, text = plainClean });
                }
            }
            // 提取代码（保留原始代码，不格式化）
            string codeBody = m.Groups[2].Value;
            res.Add(new MsgSegment { isCode = true, text = codeBody });
            pos = m.Index + m.Length;
        }
        // 末尾剩余普通文本
        if (pos < source.Length)
        {
            string tailRaw = source.Substring(pos);
            string tailClean = FormatResponse(tailRaw);
            if (!string.IsNullOrEmpty(tailClean))
            {
                res.Add(new MsgSegment { isCode = false, text = tailClean });
            }
        }
        return res;
    }

    void DrawFullChatHistoryPanel()
    {
        EditorGUILayout.LabelField("全部历史对话", EditorStyles.boldLabel);
        historyScrollPos = EditorGUILayout.BeginScrollView(historyScrollPos, GUILayout.ExpandHeight(true));

        if (localChatStorage.records.Count == 0)
        {
            EditorGUILayout.LabelField("暂无历史对话，发送第一条消息后自动保存");
        }
        else
        {
            foreach (var msg in localChatStorage.records)
            {
                if (msg.role == "user")
                {
                    EditorGUILayout.LabelField("【用户提问】", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(msg.content, ChatTextStyle);
                    EditorGUILayout.Space();
                    EditorGUILayout.Space();
                }
                else
                {
                    EditorGUILayout.LabelField("【AI回复】", EditorStyles.miniBoldLabel);
                    var segments = SplitMessageToSegment(msg.content);
                    foreach (var seg in segments)
                    {
                        if (!seg.isCode)
                        {
                            // 普通文字（已清理#、*、**等Markdown）
                            EditorGUILayout.LabelField(seg.text, ChatTextStyle);
                        }
                        else
                        {
                            // 代码块带复制按钮
                            EditorGUILayout.BeginVertical("Box");
                            EditorGUILayout.LabelField(seg.text, CodeTextStyle);
                            if (GUILayout.Button("📋 一键复制代码", GUILayout.Height(24)))
                            {
                                GUIUtility.systemCopyBuffer = seg.text;
                                EditorUtility.DisplayDialog("复制成功", "代码已经复制到剪贴板！", "确定");
                            }
                            EditorGUILayout.EndVertical();
                        }
                    }
                }
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    #region 持久化
    void LoadLocalChatHistory()
    {
        localChatStorage = new ChatStorage();
        if (File.Exists(HistorySavePath))
        {
            string json = File.ReadAllText(HistorySavePath);
            try
            {
                localChatStorage = JsonUtility.FromJson<ChatStorage>(json);
            }
            catch
            {
                localChatStorage = new ChatStorage();
                Debug.LogWarning("本地对话记录文件损坏，已重置");
            }
        }
    }

    void SaveLocalChatHistory()
    {
        string json = JsonUtility.ToJson(localChatStorage, true);
        File.WriteAllText(HistorySavePath, json);
    }

    void AppendChatRecord(string role, string content)
    {
        localChatStorage.records.Add(new ChatMessage()
        {
            role = role,
            content = content,
            timeStamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        SaveLocalChatHistory();
        historyScrollPos.y = float.MaxValue;
    }
    #endregion

    // Markdown清理函数：只作用于普通文本，不处理代码块
    // Markdown清理函数：还原旧表格处理逻辑，仅作用于普通文本，不处理代码块
    private string FormatResponse(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string result = text;
        // ===== 1. 处理表格（旧版转换逻辑） =====
        // 三列表格
        result = Regex.Replace(result, @"\|([^|]+)\|([^|]+)\|([^|]+)\|", (match) =>
        {
            string col1 = match.Groups[1].Value.Trim();
            string col2 = match.Groups[2].Value.Trim();
            string col3 = match.Groups[3].Value.Trim();
            if (Regex.IsMatch(col1, @"^[\s\-:]+$") || Regex.IsMatch(col2, @"^[\s\-:]+$"))
                return "";
            return $"\n  ▪ {col1}\n    → {col2}\n    → {col3}";
        });
        // 两列表格
        result = Regex.Replace(result, @"\|([^|]+)\|([^|]+)\|", (match) =>
        {
            string col1 = match.Groups[1].Value.Trim();
            string col2 = match.Groups[2].Value.Trim();
            if (Regex.IsMatch(col1, @"^[\s\-:]+$") || Regex.IsMatch(col2, @"^[\s\-:]+$"))
                return "";
            return $"\n  ▪ {col1}\n    → {col2}";
        });
        // 移除表格分隔线
        result = Regex.Replace(result, @"^\s*\|[\s\-:]+\|[\s\-:]+\|[\s\-:]+\|\s*$", "", RegexOptions.Multiline);
        result = Regex.Replace(result, @"^\s*\|[\s\-:]+\|[\s\-:]+\|\s*$", "", RegexOptions.Multiline);

        // ===== 2. 移除粗体 =====
        result = Regex.Replace(result, @"\*\*([^*]+)\*\*", "$1");
        result = Regex.Replace(result, @"__([^_]+)__", "$1");
        // ===== 3. 移除斜体 =====
        result = Regex.Replace(result, @"\*([^*]+)\*", "$1");
        result = Regex.Replace(result, @"_([^_]+)_", "$1");
        // ===== 4. 移除标题标记 =====
        result = Regex.Replace(result, @"^#+\s*", "", RegexOptions.Multiline);
        // ===== 5. 清理残留管道符 =====
        result = Regex.Replace(result, @"^\s*\|\s*$", "", RegexOptions.Multiline);
        result = Regex.Replace(result, @"^\|\s*", "", RegexOptions.Multiline);
        result = Regex.Replace(result, @"\s*\|\s*$", "", RegexOptions.Multiline);
        // ===== 6. 列表项转 • =====
        result = Regex.Replace(result, @"^[\s]*[-*]\s+", "• ", RegexOptions.Multiline);
        // ===== 7. 数字列表 =====
        result = Regex.Replace(result, @"^(\d+)\.\s+", "$1) ", RegexOptions.Multiline);
        // ===== 8. 清理多余空行 =====
        result = Regex.Replace(result, @"\n{3,}", "\n\n");
        // ===== 9. 清理行尾空格 =====
        result = Regex.Replace(result, @"[ \t]+$", "", RegexOptions.Multiline);
        return result.Trim();
    }

    void Update()
    {
        if (ongoingRequest != null && ongoingRequest.isDone)
        {
            if (ongoingRequest.result == UnityWebRequest.Result.Success)
            {
                string aiRaw = ParseResultFromJson(ongoingRequest.downloadHandler.text);
                // 原始完整文本存入记录，保留```用于识别代码块
                AppendChatRecord("assistant", aiRaw);
            }
            else
            {
                string errMsg = $"请求失败：{ongoingRequest.error}\n请确认后端python服务已经启动";
                AppendChatRecord("assistant", errMsg);
            }
            ongoingRequest.Dispose();
            ongoingRequest = null;
            Repaint();
        }
    }

    private void SendRequest()
    {
        if (string.IsNullOrWhiteSpace(inputText)) return;
        AppendChatRecord("user", inputText);

        string queryWithCode = inputText;
        List<string> foundScripts = FindScriptsInQuery(inputText);
        if (foundScripts.Count > 0)
        {
            queryWithCode += "\n\n【以下是我项目中的相关脚本】";
            int loadedCount = 0;
            int maxScripts = 5;
            foreach (var name in foundScripts.Take(maxScripts))
            {
                string c = ReadScriptFile(name);
                if (!string.IsNullOrEmpty(c))
                {
                    queryWithCode += $"\n\n=== {name} ===\n{c}";
                    loadedCount++;
                }
                else
                {
                    queryWithCode += $"\n\n⚠未找到脚本 {name}";
                }
            }
            if (foundScripts.Count > maxScripts)
                queryWithCode += $"\n\n...还有 {foundScripts.Count - maxScripts} 个脚本未读取，请分批提问";
            queryWithCode += $"\n\n【共加载 {loadedCount}/{foundScripts.Count}】";
        }

        const int MAX_QUERY = 12000;
        if (queryWithCode.Length > MAX_QUERY)
        {
            queryWithCode = queryWithCode.Substring(0, MAX_QUERY) + "\n\n...内容过长已截断";
        }

        ReqData req = new ReqData { session_id = SESSION_ID, query = queryWithCode };
        string jsonBody = JsonUtility.ToJson(req);
        ongoingRequest = UnityWebRequest.Post(API_URL, jsonBody, "application/json");
        ongoingRequest.SendWebRequest();
    }

    private List<string> FindScriptsInQuery(string query)
    {
        HashSet<string> set = new HashSet<string>();
        foreach (Match m in Regex.Matches(query, @"([\w\u4e00-\u9fa5]+\.cs)")) set.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(query, @"脚本[\s：:]*([\w\u4e00-\u9fa5]+)")) set.Add(m.Groups[1].Value + ".cs");
        return set.ToList();
    }

    private string ReadScriptFile(string fileName)
    {
        try
        {
            string path = GetScriptFolder();
            var files = Directory.GetFiles(path, fileName, SearchOption.AllDirectories);
            if (files.Length == 0) return null;
            string text = File.ReadAllText(files[0]);
            const int limit = 5000;
            if (text.Length > limit) text = text.Substring(0, limit) + "\n// 文件过长截断";
            return text;
        }
        catch
        {
            return null;
        }
    }

    private string GetScriptFolder()
    {
        string rel = SCRIPTS_FOLDER.Replace("Assets/", "");
        return Path.Combine(Application.dataPath, rel);
    }

    private string ParseResultFromJson(string json)
    {
        try
        {
            var obj = JsonUtility.FromJson<ResponseData>(json);
            return string.IsNullOrEmpty(obj.result) ? json : obj.result;
        }
        catch
        {
            return json;
        }
    }

    [System.Serializable]
    private class ReqData
    {
        public string session_id;
        public string query;
    }
    [System.Serializable]
    private class ResponseData
    {
        public string result;
    }
}