using UnityEngine;
using UnityEditor;
using System.Text;
using UnityEngine.Networking;
using Unity.EditorCoroutines.Editor;
using System.Collections;

public class OpenAIShaderCreator : EditorWindow
{
    private string apiKey = string.Empty;
    private string prompt = "";
    private string shaderCode = "";
    const string PROMPT_PREFS = "OpenAIShaderCreator_Prompt";

    private bool isLoading = false;
    private string lastResponse = "";

    [System.Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;
        public ChatMessage(string role, string content)
        {
            this.role = role;
            this.content = content;
        }
    }
    [System.Serializable]
    public class ChatRequest
    {
        public string model = "gpt-3.5-turbo";
        public ChatMessage[] messages;
        public float temperature = 0.2f;
    }
    [System.Serializable]
    public class ChatResponse
    {
        public ChatChoice[] choices;
    }
    [System.Serializable]
    public class ChatChoice
    {
        public int index;
        public ChatMessage message;
        public string finish_reason;
    }

    const string OPEN_AI_API_KEY_PREF = "OpenAI_API_Key";

    private void OnEnable()
    {
        this.apiKey = EditorPrefs.GetString(OPEN_AI_API_KEY_PREF, string.Empty);
        this.prompt = EditorPrefs.GetString(PROMPT_PREFS, "Please create a realistic water surface shader.");
    }

    private void OnDisable()
    {
        EditorPrefs.SetString(OPEN_AI_API_KEY_PREF, this.apiKey);
        EditorPrefs.SetString(PROMPT_PREFS, this.prompt);
    }

    [MenuItem("Tools/OpenAI/Shader Creator")]
    public static void ShowWindow()
    {
        GetWindow<OpenAIShaderCreator>("OpenAI Shader Creator");
    }

    Vector2 _scrollPosition = Vector2.zero;
    Vector2 _scrollPosition2 = Vector2.zero;

    private void OnGUI()
    {
        GUILayout.Label("OpenAI Shader Creator", EditorStyles.boldLabel);

        this.apiKey = EditorGUILayout.TextField("API Key", this.apiKey);
        if (string.IsNullOrEmpty(this.apiKey))
        {
            GUILayout.Label("Input OpenAI API KEY", EditorStyles.helpBox);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prompt");
        var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
        this.prompt = EditorGUILayout.TextArea(this.prompt, style, GUILayout.Height(60));

        EditorGUILayout.Space();
        if (GUILayout.Button("Create Shader") && !isLoading)
        {
            shaderCode = string.Empty;
            lastResponse = "";
            EditorCoroutineUtility.StartCoroutine(GenerateShaderCoroutine(prompt), this);
        }

        if (isLoading)
        {
            GUILayout.Label("Generating...", EditorStyles.helpBox);
        }

        EditorGUILayout.Space();

        GUILayout.Label("Generated Shader code", EditorStyles.boldLabel);

        // Copyボタンを追加
        if (!string.IsNullOrEmpty(shaderCode))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Shader Code"))
            {
                EditorGUIUtility.systemCopyBuffer = shaderCode;
                ShowNotification(new GUIContent("Copied to clipboard!"));
            }
            if (GUILayout.Button("Save as Shader"))
            {
                string path = EditorUtility.SaveFilePanel(
                    "Save Shader",
                    "Assets",
                    "NewShader.shader",
                    "shader"
                );
                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllText(path, shaderCode, Encoding.UTF8);
                    AssetDatabase.Refresh();
                    ShowNotification(new GUIContent("Shader saved!"));
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition,GUILayout.Height(200));
        EditorGUILayout.TextArea(shaderCode,GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        if (!string.IsNullOrEmpty(lastResponse))
        {
            EditorGUILayout.Space();
            if (EditorGUILayout.Foldout(true, "API Response"))
            {
                _scrollPosition2 = EditorGUILayout.BeginScrollView(_scrollPosition2, GUILayout.Height(100));
                EditorGUILayout.TextArea(lastResponse, GUILayout.Height(100));
                EditorGUILayout.EndScrollView();
            }
        }

        
    }

    private IEnumerator GenerateShaderCoroutine(string prompt)
    {
        isLoading = true;
        string url = "https://api.openai.com/v1/chat/completions";

        // シェーダーコードを生成するようにプロンプトを整形
        string systemPrompt = "You are an AI that generates shader code for Unity. Always return only ShaderLab code in a code block (```)";
        var messages = new ChatMessage[] {
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", prompt)
        };
        var req = new ChatRequest {
            model = "gpt-4.1",
            messages = messages,
            temperature = 0.2f
        };
        string json = JsonUtility.ToJson(req);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return request.SendWebRequest();

            isLoading = false;
            if (request.result != UnityWebRequest.Result.Success)
            {
                shaderCode = "API ERROR: " + request.error + "\n" + request.downloadHandler.text;
            }
            else
            {
                lastResponse = request.downloadHandler.text;
                // ChatResponse型でパース
                ChatResponse res = JsonUtility.FromJson<ChatResponse>(lastResponse);
                if (res != null && res.choices != null && res.choices.Length > 0)
                {
                    string content = res.choices[0].message.content;
                    shaderCode = ExtractShaderCode(content);
                }
                else
                {
                    shaderCode = "Parse Error";
                }
            }
        }
    }

    // コードブロック（```）で囲まれた部分だけ抽出
    private string ExtractShaderCode(string content)
    {
        if (string.IsNullOrEmpty(content)) return "";
        int start = content.IndexOf("```", System.StringComparison.Ordinal);
        if (start < 0) return content;
        int end = content.IndexOf("```", start + 3, System.StringComparison.Ordinal);
        if (end < 0) return content.Substring(start + 3).Trim();
        string code = content.Substring(start + 3, end - (start + 3));
        return code.Trim();
    }
}
