using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

/// <summary>
/// Sistema limpo e profissional para envio de pontos ao servidor
/// Envia pontos quando vídeos rewarded são finalizados
/// </summary>
public class ServerPointsSender : MonoBehaviour
{
    private static ServerPointsSender _instance;
    public static ServerPointsSender Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ServerPointsSender");
                _instance = go.AddComponent<ServerPointsSender>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Server Configuration")]
    [SerializeField] private string serverBaseUrl = "https://app.mobplaygames.com.br/unity/k26pix/";
    [SerializeField] private string submitEndpoint = "php/unified_submit_score.php";
    
    [Header("Settings")]
    [SerializeField] private int requestTimeout = 30;
    [SerializeField] private bool enableDebugLogs = true;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Garantir que configurações têm valores válidos
            EnsureValidConfiguration();
            
            if (enableDebugLogs)
            {
                Debug.Log("[ServerPointsSender] ✅ Sistema inicializado");
                Debug.Log($"[ServerPointsSender] 🔗 URL: {serverBaseUrl}{submitEndpoint}");
                Debug.Log($"[ServerPointsSender] ⏱️ Timeout: {requestTimeout}s");
            }
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Garante que a configuração tem valores válidos
    /// </summary>
    private void EnsureValidConfiguration()
    {
        // Validar e corrigir serverBaseUrl
        if (string.IsNullOrEmpty(serverBaseUrl))
        {
            serverBaseUrl = "https://app.mobplaygames.com.br/unity/k26pix/";
            LogWarning("⚠️ serverBaseUrl estava vazio, usando valor padrão");
        }
        
        if (!serverBaseUrl.StartsWith("http"))
        {
            LogError($"❌ serverBaseUrl inválida: {serverBaseUrl}");
            serverBaseUrl = "https://app.mobplaygames.com.br/unity/k26pix/";
            LogWarning("⚠️ Usando valor padrão para serverBaseUrl");
        }
        
        // Validar e corrigir submitEndpoint
        if (string.IsNullOrEmpty(submitEndpoint))
        {
            submitEndpoint = "app_pix01/php/unified_submit_score.php";
            LogWarning("⚠️ submitEndpoint estava vazio, usando valor padrão");
        }
        
        // Validar requestTimeout
        if (requestTimeout <= 0)
        {
            requestTimeout = 30;
            LogWarning("⚠️ requestTimeout inválido, usando 30s");
        }
    }

    #region Public API

    /// <summary>
    /// Envia pontos ao servidor quando um vídeo rewarded é finalizado
    /// </summary>
    /// <param name="points">Quantidade de pontos a enviar (padrão: 2)</param>
    /// <param name="adNetwork">Rede de anúncios (admob, max, etc.)</param>
    /// <param name="callback">Callback opcional com resultado (success, newTotal)</param>
    public void SendRewardedVideoPoints(int points = 2, string adNetwork = "unknown", Action<bool, int> callback = null)
    {
        StartCoroutine(SendPointsCoroutine(points, "rewarded_video", adNetwork, callback));
    }

    /// <summary>
    /// Envia pontos genéricos ao servidor
    /// </summary>
    /// <param name="points">Quantidade de pontos</param>
    /// <param name="type">Tipo de pontos (rewarded_video, daily_mission, etc.)</param>
    /// <param name="adNetwork">Rede de anúncios</param>
    /// <param name="callback">Callback opcional com resultado</param>
    public void SendPoints(int points, string type, string adNetwork = "unknown", Action<bool, int> callback = null)
    {
        StartCoroutine(SendPointsCoroutine(points, type, adNetwork, callback));
    }

    #endregion

    #region Core Logic

    private IEnumerator SendPointsCoroutine(int points, string type, string adNetwork, Action<bool, int> callback)
    {
        // 1. AGUARDAR INICIALIZAÇÃO MASTER (IMPEDIR DUPLICIDADE)
        if (GuestInitializer.Instance != null && !GuestInitializer.Instance.IsInitialized())
        {
            if (enableDebugLogs)
                Log("⏳ Aguardando GuestInitializer (Master) criar conta antes de enviar pontos...");
            
            float waitTimeout = 15f; // Esperar até 15 segundos
            while (!GuestInitializer.Instance.IsInitialized() && waitTimeout > 0)
            {
                waitTimeout -= Time.deltaTime;
                yield return null;
            }
        }

        // Validar pontos
        if (points <= 0)
        {
            LogError($"❌ Pontos inválidos: {points}");
            callback?.Invoke(false, 0);
            yield break;
        }

        // Obter dados do usuário/guest
        int? guestId = GetGuestId();
        int? userId = GetUserId();
        string deviceId = GetDeviceId();

        // Log detalhado de identificação
        if (enableDebugLogs)
        {
            Log($"🔍 Verificando identificação do usuário:");
            Log($"   - guest_id: {(guestId.HasValue ? guestId.Value.ToString() : "null")}");
            Log($"   - user_id: {(userId.HasValue ? userId.Value.ToString() : "null")}");
            Log($"   - device_id: {(string.IsNullOrEmpty(deviceId) ? "null" : deviceId.Substring(0, Math.Min(20, deviceId.Length)) + "...")}");
            Log($"   - GuestInitializer inicializado: {(GuestInitializer.Instance != null ? GuestInitializer.Instance.IsInitialized().ToString() : "null")}");
        }

        // Se ainda não temos guest_id, tentar recuperar uma última vez
        if (!guestId.HasValue && !userId.HasValue && !string.IsNullOrEmpty(deviceId))
        {
            if (enableDebugLogs)
                Log("🔄 Tentando recuperar guest_id do servidor usando device_id...");
            
            yield return StartCoroutine(RecoverGuestIdFromServer(deviceId));
            guestId = GetGuestId();
            
            if (enableDebugLogs)
                Log($"🔍 Após recuperação - guest_id: {(guestId.HasValue ? guestId.Value.ToString() : "null")}");
        }

        // Validar que temos pelo menos um identificador
        if (!guestId.HasValue && !userId.HasValue)
        {
            LogError("❌ Falha crítica: Nenhum guest_id ou user_id disponível para enviar pontos.");
            LogError("   Verifique se:");
            LogError("   1. GuestInitializer está inicializado");
            LogError("   2. Há conexão com internet");
            LogError("   3. PlayerPrefs contém guest_id ou user_id");
            LogError($"   4. device_id disponível: {!string.IsNullOrEmpty(deviceId)}");
            callback?.Invoke(false, 0);
            yield break;
        }

        // Preparar payload JSON
        string jsonPayload = CreateJsonPayload(points, type, adNetwork, guestId, userId, deviceId);

        if (enableDebugLogs)
        {
            Log($"📤 Enviando {points} pontos ao servidor (tipo: {type}, rede: {adNetwork})");
            Log($"📋 Payload: {jsonPayload}");
        }

        // Construir URL
        string url = $"{serverBaseUrl.TrimEnd('/')}/{submitEndpoint.TrimStart('/')}";

        // Validar URL
        if (string.IsNullOrEmpty(url) || !url.StartsWith("http"))
        {
            LogError($"❌ URL inválida: {url}");
            callback?.Invoke(false, 0);
            yield break;
        }

        // Criar requisição HTTP POST
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = requestTimeout;

        // Enviar requisição
        yield return request.SendWebRequest();

        // Processar resposta
        bool success = false;
        int newTotal = 0;

        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;

            if (enableDebugLogs)
            {
                Log($"✅ Requisição HTTP bem-sucedida (Status: {request.responseCode})");
                Log($"📥 Resposta do servidor: {responseText}");
            }

            // Tentar parsear resposta JSON
            try
            {
                var response = JsonUtility.FromJson<ServerResponse>(responseText);

                if (response != null && response.status == "success")
                {
                    success = true;
                    newTotal = response.new_total > 0 ? response.new_total : response.total_points;

                    Log($"✅ Pontos enviados com sucesso! Novo total: {newTotal}");
                    
                    if (enableDebugLogs)
                    {
                        Log($"   - Pontos adicionados: {response.points_added}");
                        Log($"   - Total anterior: {newTotal - response.points_added}");
                        Log($"   - Total novo: {newTotal}");
                    }

                    // Se guest foi criado, salvar guest_id
                    if (response.guest_id.HasValue && response.guest_id.Value > 0)
                    {
                        SaveGuestId(response.guest_id.Value);
                    }
                    
                    // Notificar React sobre pontos enviados
                    NotifyReactAboutPoints(points, newTotal);
                }
                else
                {
                    LogError($"❌ Erro na resposta do servidor:");
                    LogError($"   - Status: {response?.status ?? "null"}");
                    LogError($"   - Mensagem: {response?.message ?? "Resposta inválida"}");
                    LogError($"   - Resposta completa: {responseText}");
                }
            }
            catch (Exception ex)
            {
                LogError($"❌ Erro ao processar resposta JSON: {ex.Message}");
                LogError($"📄 Resposta completa do servidor: {responseText}");
                LogError($"📄 Stack trace: {ex.StackTrace}");
            }
        }
        else
        {
            string errorMsg = request.error ?? "Erro desconhecido";
            LogError($"❌ Erro na requisição HTTP:");
            LogError($"   - Tipo: {request.result}");
            LogError($"   - Erro: {errorMsg}");
            LogError($"   - Response Code: {request.responseCode}");
            LogError($"   - URL: {url}");

            if (request.downloadHandler != null && !string.IsNullOrEmpty(request.downloadHandler.text))
            {
                LogError($"📄 Resposta do servidor: {request.downloadHandler.text}");
            }
        }

        // Limpar requisição
        request.Dispose();

        // Chamar callback
        callback?.Invoke(success, newTotal);
    }

    #endregion

    #region JSON Payload

    private string CreateJsonPayload(int points, string type, string adNetwork, int? guestId, int? userId, string deviceId)
    {
        var jsonParts = new System.Collections.Generic.List<string>();

        // Adicionar identificador do usuário
        if (guestId.HasValue && guestId.Value > 0)
        {
            jsonParts.Add($"\"guest_id\":{guestId.Value}");
        }
        else if (userId.HasValue && userId.Value > 0)
        {
            jsonParts.Add($"\"user_id\":{userId.Value}");
        }
        else if (!string.IsNullOrEmpty(deviceId))
        {
            // Escapar caracteres especiais
            string escapedDeviceId = deviceId.Replace("\\", "\\\\").Replace("\"", "\\\"");
            jsonParts.Add($"\"device_id\":\"{escapedDeviceId}\"");
        }

        // Campos obrigatórios
        jsonParts.Add($"\"points\":{points}");
        jsonParts.Add($"\"type\":\"{type}\"");

        // Source: combinar adNetwork com tipo
        string source = $"{adNetwork.ToLower()}_unity";
        jsonParts.Add($"\"source\":\"{source}\"");

        // Ad network (opcional)
        if (!string.IsNullOrEmpty(adNetwork) && adNetwork != "unknown")
        {
            string escapedAdNetwork = adNetwork.Replace("\\", "\\\\").Replace("\"", "\\\"");
            jsonParts.Add($"\"ad_network\":\"{escapedAdNetwork}\"");
        }

        return "{" + string.Join(",", jsonParts) + "}";
    }

    #endregion

    #region User Data Helpers

    private int? GetGuestId()
    {
        // PRIORIDADE 1: Tentar obter do PlayerPrefs (mais confiável)
        int prefGuestId = PlayerPrefs.GetInt("guest_id", 0);
        if (prefGuestId > 0)
            return prefGuestId;

        // PRIORIDADE 2: Tentar obter do GuestInitializer se disponível
        if (GuestInitializer.Instance != null)
        {
            int guestId = GuestInitializer.Instance.GetGuestId();
            if (guestId > 0)
            {
                // Salvar no PlayerPrefs para próxima vez
                PlayerPrefs.SetInt("guest_id", guestId);
                PlayerPrefs.Save();
                return guestId;
            }
        }

        // PRIORIDADE 3: Tentar obter do user_id se for guest (fallback antigo)
        bool isGuest = PlayerPrefs.GetString("is_guest", "false") == "true";
        if (isGuest)
        {
            int userId = PlayerPrefs.GetInt("user_id", 0);
            if (userId > 0)
                return userId;
        }

        return null;
    }

    private int? GetUserId()
    {
        bool isGuest = PlayerPrefs.GetString("is_guest", "false") == "true";
        if (isGuest)
            return null; // Se for guest, não usar user_id

        int userId = PlayerPrefs.GetInt("user_id", 0);
        return userId > 0 ? userId : null;
    }

    private string GetDeviceId()
    {
        // PRIORIDADE 1: Obter do GuestInitializer se disponível
        if (GuestInitializer.Instance != null)
        {
            string deviceId = GuestInitializer.Instance.GetOrCreateDeviceId();
            if (!string.IsNullOrEmpty(deviceId))
                return deviceId;
        }

        // PRIORIDADE 2: Tentar obter do PlayerPrefs diretamente
        string prefDeviceId = PlayerPrefs.GetString("device_id", "");
        if (!string.IsNullOrEmpty(prefDeviceId))
            return prefDeviceId;

        // PRIORIDADE 3: Gerar device_id baseado no hardware (mesma lógica do GuestInitializer)
        string sysId = SystemInfo.deviceUniqueIdentifier;
        if (!string.IsNullOrEmpty(sysId) && sysId != "unsupported")
        {
            PlayerPrefs.SetString("device_id", sysId);
            PlayerPrefs.Save();
            return sysId;
        }

        // PRIORIDADE 4: Fallback - gerar ID estável baseado em informações do dispositivo
        string deviceModel = SystemInfo.deviceModel ?? "Unknown";
        string deviceName = SystemInfo.deviceName ?? "Unknown";
        string operatingSystem = SystemInfo.operatingSystem ?? "Unknown";
        string stableInfo = $"{deviceModel}_{deviceName}_{operatingSystem}";
        
        long hash = 0;
        foreach (char c in stableInfo)
        {
            hash = (hash * 31) + c;
        }
        
        string fallbackDeviceId = $"unity_{Math.Abs(hash):X12}";
        PlayerPrefs.SetString("device_id", fallbackDeviceId);
        PlayerPrefs.Save();
        
        if (enableDebugLogs)
            LogWarning($"⚠️ Gerando device_id de fallback: {fallbackDeviceId}");
        
        return fallbackDeviceId;
    }

    /// <summary>
    /// Recupera guest_id do servidor usando device_id
    /// </summary>
    private IEnumerator RecoverGuestIdFromServer(string deviceId)
    {
        string baseUrl = "https://app.mobplaygames.com.br/unity/k26pix/";
        string endpoint = "php/create_guest.php";
        string url = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}?device_id={UnityWebRequest.EscapeURL(deviceId)}";

        if (enableDebugLogs)
            Log($"🔄 Recuperando guest_id do servidor usando device_id...");

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = requestTimeout;

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                string responseText = request.downloadHandler.text;
                var response = JsonUtility.FromJson<GuestRecoveryResponse>(responseText);

                if (response != null && response.status == "success" && response.guest_id > 0)
                {
                    // Salvar guest_id localmente
                    PlayerPrefs.SetInt("guest_id", response.guest_id);
                    PlayerPrefs.SetString("device_id", deviceId);
                    PlayerPrefs.SetString("is_guest", "true");
                    PlayerPrefs.Save();

                    if (enableDebugLogs)
                        Log($"✅ Guest_id recuperado do servidor: {response.guest_id}");

                    // Atualizar GuestInitializer se disponível
                    if (GuestInitializer.Instance != null)
                    {
                        // Forçar atualização do GuestInitializer
                        var guestInit = GuestInitializer.Instance;
                        var guestIdField = typeof(GuestInitializer).GetField("currentGuestId", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var deviceIdField = typeof(GuestInitializer).GetField("currentDeviceId", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var isInitField = typeof(GuestInitializer).GetField("isInitialized", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (guestIdField != null) guestIdField.SetValue(guestInit, response.guest_id);
                        if (deviceIdField != null) deviceIdField.SetValue(guestInit, deviceId);
                        if (isInitField != null) isInitField.SetValue(guestInit, true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"❌ Erro ao processar resposta de recuperação: {ex.Message}");
            }
        }
        else
        {
            if (enableDebugLogs)
                LogWarning($"⚠️ Erro ao recuperar guest_id: {request.error}");
        }

        request.Dispose();
    }

    // REMOVIDO: GenerateDeviceId() com timestamp foi removido para evitar IDs duplicados/inconsistentes
    // A lógica de geração agora é centralizada no GuestInitializer.cs através do GetOrCreateDeviceId()

    private void SaveGuestId(int guestId)
    {
        PlayerPrefs.SetInt("guest_id", guestId);
        PlayerPrefs.SetString("is_guest", "true");
        PlayerPrefs.Save();

        if (enableDebugLogs)
            Log($"💾 Guest ID salvo: {guestId}");
    }

    #endregion

    #region Logging

    private void Log(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[ServerPointsSender] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[ServerPointsSender] {message}");
    }

    #endregion

    #region React Notification

    /// <summary>
    /// Notifica React sobre pontos enviados
    /// </summary>
    private void NotifyReactAboutPoints(int pointsAdded, int newTotal)
    {
        // Buscar UniWebView na cena
        var webView = FindObjectOfType<UniWebView>();
        if (webView == null)
        {
            if (enableDebugLogs)
                LogWarning("⚠️ UniWebView não encontrado - não é possível notificar React");
            return;
        }
        
        try
        {
            string script = $@"
                if(typeof window.onPointsSentSuccessfully === 'function') {{
                    try {{
                        window.onPointsSentSuccessfully({pointsAdded}, {newTotal});
                        console.log('[Unity] ✅ Pontos notificados: {pointsAdded}, Total: {newTotal}');
                    }} catch(e) {{
                        console.error('[Unity] ❌ Erro ao executar onPointsSentSuccessfully:', e);
                    }}
                }} else {{
                    console.warn('[Unity] ⚠️ window.onPointsSentSuccessfully não está definido');
                }}
            ";
            
            webView.EvaluateJavaScript(script, (payload) =>
            {
                if (enableDebugLogs)
                    Log($"📤 React notificado: {pointsAdded} pontos, total: {newTotal}");
            });
        }
        catch (Exception ex)
        {
            LogError($"❌ Erro ao notificar React: {ex.Message}");
        }
    }
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[ServerPointsSender] {message}");
    }

    #endregion

    #region Response Classes

    [Serializable]
    private class ServerResponse
    {
        public string status;
        public string message;
        public int? guest_id;
        public int? user_id;
        public int points_added;
        public int new_total;
        public int total_points;
    }

    [Serializable]
    private class GuestRecoveryResponse
    {
        public string status;
        public string message;
        public int guest_id;
        public string device_id;
        public bool was_created;
    }

    #endregion
    
    #region Debug Methods
    
    /// <summary>
    /// Método de teste manual para diagnóstico
    /// Use: Botão direito no componente > "Test: Enviar Pontos Manualmente"
    /// </summary>
    [ContextMenu("Test: Enviar Pontos Manualmente")]
    public void TestSendPoints()
    {
        Debug.Log("🧪 [TESTE] ========================================");
        Debug.Log("🧪 [TESTE] Iniciando teste de envio de pontos...");
        Debug.Log("🧪 [TESTE] ========================================");
        
        // Verificar identificação
        int? guestId = GetGuestId();
        int? userId = GetUserId();
        string deviceId = GetDeviceId();
        
        Debug.Log($"🧪 [TESTE] Identificação atual:");
        Debug.Log($"   - guest_id: {(guestId.HasValue ? guestId.Value.ToString() : "null")}");
        Debug.Log($"   - user_id: {(userId.HasValue ? userId.Value.ToString() : "null")}");
        Debug.Log($"   - device_id: {(string.IsNullOrEmpty(deviceId) ? "null" : deviceId)}");
        
        // Verificar PlayerPrefs
        Debug.Log($"🧪 [TESTE] PlayerPrefs:");
        Debug.Log($"   - guest_id: {PlayerPrefs.GetInt("guest_id", 0)}");
        Debug.Log($"   - user_id: {PlayerPrefs.GetInt("user_id", 0)}");
        Debug.Log($"   - device_id: {PlayerPrefs.GetString("device_id", "")}");
        Debug.Log($"   - is_guest: {PlayerPrefs.GetString("is_guest", "false")}");
        
        // Verificar GuestInitializer
        if (GuestInitializer.Instance != null)
        {
            Debug.Log($"🧪 [TESTE] GuestInitializer:");
            Debug.Log($"   - Inicializado: {GuestInitializer.Instance.IsInitialized()}");
            Debug.Log($"   - guest_id: {GuestInitializer.Instance.GetGuestId()}");
            Debug.Log($"   - device_id: {GuestInitializer.Instance.GetDeviceId()}");
        }
        else
        {
            Debug.LogWarning("🧪 [TESTE] ⚠️ GuestInitializer.Instance é null!");
        }
        
        // Verificar configuração
        Debug.Log($"🧪 [TESTE] Configuração:");
        Debug.Log($"   - serverBaseUrl: {serverBaseUrl}");
        Debug.Log($"   - submitEndpoint: {submitEndpoint}");
        Debug.Log($"   - URL completa: {serverBaseUrl.TrimEnd('/')}/{submitEndpoint.TrimStart('/')}");
        Debug.Log($"   - enableDebugLogs: {enableDebugLogs}");
        
        // Tentar enviar pontos
        Debug.Log("🧪 [TESTE] Enviando 2 pontos de teste...");
        SendRewardedVideoPoints(2, "test_manual", (success, newTotal) =>
        {
            Debug.Log("🧪 [TESTE] ========================================");
            if (success)
            {
                Debug.Log($"🧪 [TESTE] ✅ SUCESSO! Novo total: {newTotal}");
            }
            else
            {
                Debug.LogError($"🧪 [TESTE] ❌ FALHA ao enviar pontos");
            }
            Debug.Log("🧪 [TESTE] ========================================");
        });
    }
    
    /// <summary>
    /// Verifica estado atual do sistema
    /// </summary>
    [ContextMenu("Debug: Verificar Estado do Sistema")]
    public void DebugCheckSystemState()
    {
        Debug.Log("🔍 [DEBUG] Estado do Sistema de Pontos:");
        Debug.Log($"   - ServerPointsSender existe: {_instance != null}");
        Debug.Log($"   - GuestInitializer existe: {GuestInitializer.Instance != null}");
        
        if (GuestInitializer.Instance != null)
        {
            Debug.Log($"   - GuestInitializer inicializado: {GuestInitializer.Instance.IsInitialized()}");
            Debug.Log($"   - Guest ID: {GuestInitializer.Instance.GetGuestId()}");
        }
        
        Debug.Log($"   - PlayerPrefs guest_id: {PlayerPrefs.GetInt("guest_id", 0)}");
        Debug.Log($"   - PlayerPrefs user_id: {PlayerPrefs.GetInt("user_id", 0)}");
        Debug.Log($"   - PlayerPrefs device_id: {PlayerPrefs.GetString("device_id", "")}");
    }
    
    #endregion
}

