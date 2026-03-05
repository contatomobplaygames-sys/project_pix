using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System;

/// <summary>
/// Inicializador de Guest
/// NOTA: Criação automática de guest DESABILITADA
/// Este componente apenas gerencia guests existentes
/// Para criar guest, use CreateGuestManually() quando necessário
/// </summary>
public class GuestInitializer : MonoBehaviour
{
    private static GuestInitializer _instance;
    public static GuestInitializer Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("GuestInitializer");
                _instance = go.AddComponent<GuestInitializer>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Configuration")]
    [SerializeField] private string serverBaseUrl = "https://app.mobplaygames.com.br/unity/k26pix/";
    [SerializeField] private string createGuestEndpoint = "php/create_guest.php";
    [SerializeField] private string getProfileEndpoint = "php/get_user_profile.php";
    
    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private int requestTimeout = 30;
    
    // Lock estático global para garantir apenas uma inicialização em toda a aplicação
    private static bool _globalInitializationLock = false;
    private static object _lockObject = new object();
    
    private bool isInitialized = false;
    private bool isInitializing = false; // Flag para evitar múltiplas inicializações simultâneas
    private int currentGuestId = 0;
    private string currentGuestPublicId = "";
    private string currentDeviceId = "";
    private int currentPoints = 0;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            
            if (enableDebugLogs)
                Debug.Log("[GuestInitializer] 🔧 Instância criada e configurada");
        }
        else if (_instance != this)
        {
            if (enableDebugLogs)
                Debug.Log("[GuestInitializer] ⚠️ Instância duplicada detectada, destruindo...");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Lock global para garantir apenas uma inicialização em toda a aplicação
        lock (_lockObject)
        {
            if (_globalInitializationLock)
            {
                if (enableDebugLogs)
                    Debug.Log("[GuestInitializer] ⏭️ Inicialização global já em andamento, pulando Start()");
                return;
            }

            if (isInitialized || isInitializing)
            {
                if (enableDebugLogs)
                    Debug.Log("[GuestInitializer] ⏭️ Esta instância já inicializada ou em processo, pulando Start()");
                return;
            }

            // Marcar lock global
            _globalInitializationLock = true;

            if (enableDebugLogs)
                Debug.Log("[GuestInitializer] 🚀 Iniciando criação única de guest (Master Instance)...");

            InitializeGuest();
        }
    }
    
    /// <summary>
    /// Garante que GuestInitializer existe na cena
    /// Chamado automaticamente quando alguém acessa Instance
    /// REMOVIDO: RuntimeInitializeOnLoadMethod para evitar duplicação
    /// Agora apenas Start() inicializa para ter controle total
    /// </summary>

    /// <summary>
    /// Inicializa guest (criação automática DESABILITADA)
    /// Este método ainda funciona, mas não é chamado automaticamente no Start()
    /// Use CreateGuestManually() se precisar criar guest manualmente
    /// </summary>
    public void InitializeGuest()
    {
        // Proteção contra múltiplas inicializações simultâneas
        lock (_lockObject)
        {
            if (isInitialized)
            {
                if (enableDebugLogs)
                    Debug.Log("[GuestInitializer] ✅ Guest já inicializado");
                return;
            }
            
            if (isInitializing)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[GuestInitializer] ⚠️ Inicialização já em andamento, ignorando chamada duplicada");
                return;
            }
            
            isInitializing = true;
        }

        // Verificar se já tem guest_id salvo
        int savedGuestId = PlayerPrefs.GetInt("guest_id", 0);
        string savedGuestPublicId = PlayerPrefs.GetString("guest_public_id", "");
        string savedDeviceId = PlayerPrefs.GetString("device_id", "");

        if (savedGuestId > 0 && !string.IsNullOrEmpty(savedDeviceId))
        {
            // Guest já existe localmente
            currentGuestId = savedGuestId;
            currentGuestPublicId = savedGuestPublicId;
            currentDeviceId = savedDeviceId;
                    lock (_lockObject)
                    {
                        isInitialized = true;
                        isInitializing = false;
                        _globalInitializationLock = false; // Liberar lock global
                    }
                    
                    // Sincronizar identidade com React imediatamente
                    SyncIdentityWithReact();

                    if (enableDebugLogs)
                        Debug.Log($"[GuestInitializer] ✅ Guest local encontrado: guest_id={currentGuestId}, device_id={currentDeviceId}");

                    // Verificar no servidor e carregar pontos
                    StartCoroutine(VerifyGuestOnServer());
                    return;
        }

        // Obter ou criar device_id
        string deviceId = GetOrCreateDeviceId();

        if (string.IsNullOrEmpty(deviceId))
        {
            Debug.LogError("[GuestInitializer] ❌ Não foi possível obter device_id");
            return;
        }

        // Criar guest no servidor
        StartCoroutine(CreateGuestOnServer(deviceId));
    }

    /// <summary>
    /// Obtém ou cria device_id único
    /// </summary>
    public string GetOrCreateDeviceId()
    {
        string deviceId = PlayerPrefs.GetString("device_id", "");

        if (string.IsNullOrEmpty(deviceId))
        {
            // PRIORIDADE 1: Tentar obter ID único do dispositivo (persiste entre instalações)
            deviceId = SystemInfo.deviceUniqueIdentifier;

            // PRIORIDADE 2: Se não disponível, usar combinação estável de informações do dispositivo
            if (string.IsNullOrEmpty(deviceId) || deviceId == "unsupported")
            {
                // Usar informações do dispositivo que não mudam entre instalações
                string deviceModel = SystemInfo.deviceModel ?? "Unknown";
                string deviceName = SystemInfo.deviceName ?? "Unknown";
                string operatingSystem = SystemInfo.operatingSystem ?? "Unknown";
                
                // Criar hash estável baseado em informações do dispositivo (SEM timestamp)
                // Usamos string para concatenar e então obter hash para ser determinístico
                string stableInfo = $"{deviceModel}_{deviceName}_{operatingSystem}";
                
                // Usar um algoritmo simples de hash manual para garantir que seja o mesmo em todas as execuções
                long hash = 0;
                foreach (char c in stableInfo) {
                    hash = (hash * 31) + c;
                }
                
                deviceId = $"unity_{Math.Abs(hash):X12}";
                
                if (enableDebugLogs)
                    Debug.LogWarning($"[GuestInitializer] ⚠️ SystemInfo.deviceUniqueIdentifier não disponível, usando hash estável: {deviceId}");
            }
            else
            {
                if (enableDebugLogs)
                    Debug.Log($"[GuestInitializer] ✅ Usando SystemInfo.deviceUniqueIdentifier: {deviceId}");
            }

            // Salvar localmente
            PlayerPrefs.SetString("device_id", deviceId);
            PlayerPrefs.Save();
            
            // Garantir que currentDeviceId seja atualizado
            currentDeviceId = deviceId;

            if (enableDebugLogs)
                Debug.Log($"[GuestInitializer] 📱 Device ID salvo: {deviceId}");
        }
        else
        {
            currentDeviceId = deviceId;
            if (enableDebugLogs)
                Debug.Log($"[GuestInitializer] ✅ Device ID já existe: {deviceId}");
        }

        return deviceId;
    }

    /// <summary>
    /// Cria guest no servidor
    /// </summary>
    private IEnumerator CreateGuestOnServer(string deviceId)
    {
        // Construir URL corretamente
        string baseUrl = serverBaseUrl.TrimEnd('/');
        string endpoint = createGuestEndpoint.TrimStart('/');
        string fullUrl = $"{baseUrl}/{endpoint}";
        string requestUrl = $"{fullUrl}?device_id={UnityWebRequest.EscapeURL(deviceId)}";

        if (enableDebugLogs)
        {
            Debug.Log($"[GuestInitializer] 📤 Criando guest no servidor");
            Debug.Log($"[GuestInitializer] 🔗 URL completa: {requestUrl}");
            Debug.Log($"[GuestInitializer] 📱 Device ID: {deviceId}");
        }

        UnityWebRequest request = UnityWebRequest.Get(requestUrl);
        request.timeout = requestTimeout;

        yield return request.SendWebRequest();

        if (enableDebugLogs)
        {
            Debug.Log($"[GuestInitializer] 📡 Status da requisição: {request.result}");
            Debug.Log($"[GuestInitializer] 📊 Response Code: {request.responseCode}");
        }

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                string responseText = request.downloadHandler.text;
                
                if (enableDebugLogs)
                    Debug.Log($"[GuestInitializer] 📥 Resposta do servidor: {responseText}");

                var response = JsonUtility.FromJson<GuestResponse>(responseText);

                if (response != null && response.status == "success")
                {
                    // Salvar guest_id localmente
                    lock (_lockObject)
                    {
                        currentGuestId = response.guest_id;
                        currentGuestPublicId = response.guest_public_id;
                        currentDeviceId = deviceId;
                        isInitialized = true;
                        isInitializing = false;
                        _globalInitializationLock = false; // Liberar lock global
                    }

                    PlayerPrefs.SetInt("guest_id", currentGuestId);
                    PlayerPrefs.SetString("guest_public_id", currentGuestPublicId);
                    PlayerPrefs.SetString("device_id", currentDeviceId);
                    PlayerPrefs.SetString("is_guest", "true");
                    PlayerPrefs.Save();

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[GuestInitializer] ✅ Guest criado/inicializado com sucesso!");
                        Debug.Log($"[GuestInitializer] 📊 guest_id={currentGuestId}, device_id={currentDeviceId}");
                        if (response.was_created)
                            Debug.Log($"[GuestInitializer] 🆕 Novo guest criado no servidor");
                        else
                            Debug.Log($"[GuestInitializer] ♻️ Guest existente encontrado");
                    }

                    // Atualizar GameManager se existir
                    UpdateGameManager();
                    // Sincronizar identidade com React
                    SyncIdentityWithReact();
                    
                    // Carregar pontos do banco de dados
                    StartCoroutine(LoadPointsFromServer());
                }
                else
                {
                    lock (_lockObject)
                    {
                        isInitializing = false;
                        _globalInitializationLock = false; // Liberar lock global em caso de erro
                    }
                    Debug.LogError($"[GuestInitializer] ❌ Erro na resposta: {response?.message ?? "Unknown"}");
                }
            }
            catch (System.Exception ex)
            {
                lock (_lockObject)
                {
                    isInitializing = false;
                    _globalInitializationLock = false; // Liberar lock global em caso de erro
                }
                Debug.LogError($"[GuestInitializer] ❌ Erro ao processar resposta: {ex.Message}");
            }
        }
        else
        {
            string errorDetails = $"Erro: {request.error}";
            if (request.responseCode > 0)
            {
                errorDetails += $", Response Code: {request.responseCode}";
            }
            if (!string.IsNullOrEmpty(request.downloadHandler?.text))
            {
                errorDetails += $", Response: {request.downloadHandler.text}";
            }
            
            Debug.LogError($"[GuestInitializer] ❌ Erro na requisição: {errorDetails}");
            Debug.LogError($"[GuestInitializer] 🔗 URL que falhou: {requestUrl}");
            
            // Tentar novamente após 3 segundos se for erro de rede ou timeout
            bool isNetworkError = request.result == UnityWebRequest.Result.ConnectionError || 
                                  request.result == UnityWebRequest.Result.ProtocolError;
            bool isTimeout = request.error != null && (
                request.error.Contains("timeout") || 
                request.error.Contains("Timeout") ||
                request.responseCode == 0
            );
            
            if (isNetworkError || isTimeout)
            {
                if (enableDebugLogs)
                    Debug.Log("[GuestInitializer] 🔄 Tentando novamente em 3 segundos...");
                
                yield return new WaitForSeconds(3f);
                // Não liberar flag aqui - deixar para quando a requisição retry for bem-sucedida
                StartCoroutine(CreateGuestOnServer(deviceId));
            }
            else
            {
                lock (_lockObject)
                {
                    isInitializing = false;
                    _globalInitializationLock = false; // Liberar lock global apenas se não for tentar novamente
                }
            }
        }

        request.Dispose();
    }

    /// <summary>
    /// Verifica guest no servidor e carrega pontos (se já existe localmente)
    /// </summary>
    private IEnumerator VerifyGuestOnServer()
    {
        string url = $"{serverBaseUrl.TrimEnd('/')}/{createGuestEndpoint}";
        string requestUrl = $"{url}?device_id={UnityWebRequest.EscapeURL(currentDeviceId)}";

        if (enableDebugLogs)
            Debug.Log($"[GuestInitializer] 🔍 Verificando guest no servidor: {requestUrl}");

        UnityWebRequest request = UnityWebRequest.Get(requestUrl);
        request.timeout = requestTimeout;

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                string responseText = request.downloadHandler.text;
                
                if (enableDebugLogs)
                    Debug.Log($"[GuestInitializer] 📥 Resposta do servidor: {responseText}");
                
                var response = JsonUtility.FromJson<GuestResponse>(responseText);
                if (response != null && response.status == "success")
                {
                    // Atualizar guest_id se necessário
                    if (response.guest_id != currentGuestId)
                    {
                        currentGuestId = response.guest_id;
                        PlayerPrefs.SetInt("guest_id", currentGuestId);
                        PlayerPrefs.Save();
                        
                        if (enableDebugLogs)
                            Debug.Log($"[GuestInitializer] 🔄 Guest ID atualizado: {currentGuestId}");
                    }

                    // Atualizar guest_public_id se necessário
                    if (!string.IsNullOrEmpty(response.guest_public_id) && response.guest_public_id != currentGuestPublicId)
                    {
                        currentGuestPublicId = response.guest_public_id;
                        PlayerPrefs.SetString("guest_public_id", currentGuestPublicId);
                        PlayerPrefs.Save();
                        
                        if (enableDebugLogs)
                            Debug.Log($"[GuestInitializer] 🔄 Guest Public ID atualizado: {currentGuestPublicId}");
                    }
                    
                // Sincronizar identidade com React
                SyncIdentityWithReact();
                    
                    // IMPORTANTE: Carregar pontos do servidor
                    StartCoroutine(LoadPointsFromServer());
                }
                else
                {
                    // Guest não encontrado no servidor - mas já temos device_id
                    // Isso pode acontecer se o banco foi resetado, mas não devemos criar novo guest
                    // Apenas logar o aviso
                    if (enableDebugLogs)
                        Debug.LogWarning($"[GuestInitializer] ⚠️ Guest não encontrado no servidor para device_id: {currentDeviceId}");
                    
                    // NÃO criar novo guest aqui - o device_id já existe e pode ser usado para criar guest quando necessário
                    // Apenas carregar pontos (que serão 0 se guest não existe)
                    StartCoroutine(LoadPointsFromServer());
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GuestInitializer] ❌ Erro ao processar resposta: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[GuestInitializer] ⚠️ Erro ao verificar guest: {request.error}");
        }

        request.Dispose();
    }

    /// <summary>
    /// Atualiza GameManager com guest_id
    /// </summary>
    private void UpdateGameManager()
    {
        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.SetPlayerId(currentGuestId, isGuest: true);
            if (enableDebugLogs)
                Debug.Log($"[GuestInitializer] ✅ GameManager atualizado: playerId={currentGuestId}, isGuest=true");
        }
    }

    /// <summary>
    /// Obtém guest_id atual
    /// </summary>
    public int GetGuestId()
    {
        return currentGuestId;
    }

    /// <summary>
    /// Obtém guest_public_id atual
    /// </summary>
    public string GetGuestPublicId()
    {
        return currentGuestPublicId;
    }

    /// <summary>
    /// Obtém device_id atual
    /// </summary>
    public string GetDeviceId()
    {
        return currentDeviceId;
    }

    /// <summary>
    /// Verifica se guest foi inicializado
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized && currentGuestId > 0;
    }
    
    /// <summary>
    /// Obtém pontos atuais do guest
    /// </summary>
    public int GetPoints()
    {
        return currentPoints;
    }
    
    /// <summary>
    /// Obtém nome do guest (carregado do servidor ou PlayerPrefs)
    /// </summary>
    public string GetGuestName()
    {
        // Tentar obter do PlayerPrefs primeiro
        string savedName = PlayerPrefs.GetString("guest_name", "");
        if (!string.IsNullOrEmpty(savedName))
        {
            return savedName;
        }
        
        // Se não encontrado, retornar padrão
        return "Visitante";
    }
    
    /// <summary>
    /// Força o recarregamento do perfil completo do guest do servidor
    /// Útil quando o perfil é atualizado no WebView e precisa sincronizar com Unity
    /// </summary>
    public void ReloadProfile()
    {
        if (currentGuestId <= 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[GuestInitializer] ⚠️ Não é possível recarregar perfil: guest_id inválido");
            return;
        }
        
        if (enableDebugLogs)
            Debug.Log($"[GuestInitializer] 🔄 Recarregando perfil completo do guest {currentGuestId}...");
        
        StartCoroutine(LoadPointsFromServer());
    }
    
    /// <summary>
    /// Exibe informações de debug do guest atual
    /// Útil para verificar se os dados foram carregados corretamente
    /// </summary>
    [ContextMenu("Debug: Exibir Dados do Guest")]
    public void DebugShowGuestData()
    {
        Debug.Log("=== [GuestInitializer] Dados do Guest ===");
        Debug.Log($"Guest ID: {currentGuestId}");
        Debug.Log($"Device ID: {currentDeviceId}");
        Debug.Log($"Pontos: {currentPoints}");
        Debug.Log($"Nome (PlayerPrefs): {PlayerPrefs.GetString("guest_name", "não encontrado")}");
        Debug.Log($"Nome (método): {GetGuestName()}");
        Debug.Log($"Email (PlayerPrefs): {PlayerPrefs.GetString("guest_email", "não encontrado")}");
        Debug.Log($"Inicializado: {isInitialized}");
        Debug.Log("=========================================");
    }
    
    /// <summary>
    /// Carrega perfil completo (pontos, nome, etc.) do servidor após inicialização
    /// </summary>
    private IEnumerator LoadPointsFromServer()
    {
        if (currentGuestId <= 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[GuestInitializer] ⚠️ Não é possível carregar pontos: guest_id inválido");
            yield break;
        }
        
        string url = $"{serverBaseUrl.TrimEnd('/')}/{getProfileEndpoint.TrimStart('/')}?guest_id={currentGuestId}";
        
        if (enableDebugLogs)
            Debug.Log($"[GuestInitializer] 📥 Carregando pontos do servidor: {url}");
        
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = requestTimeout;
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                string responseText = request.downloadHandler.text;
                
                if (enableDebugLogs)
                    Debug.Log($"[GuestInitializer] 📥 Resposta do servidor: {responseText}");
                
                // A API retorna no formato: { success: true, message: "...", data: { user: {...} } }
                ProfileResponse response = null;
                
                // Tentar primeiro o formato com "data" (formato padrão da API)
                try
                {
                    var dataResponse = JsonUtility.FromJson<ProfileDataResponse>(responseText);
                    if (dataResponse != null && dataResponse.success && dataResponse.data != null && dataResponse.data.user != null)
                    {
                        response = new ProfileResponse
                        {
                            success = dataResponse.success,
                            user = dataResponse.data.user
                        };
                        
                        if (enableDebugLogs)
                            Debug.Log($"[GuestInitializer] ✅ Resposta parseada no formato 'data.user'");
                    }
                }
                catch (Exception ex1)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[GuestInitializer] ⚠️ Tentativa de parse 'data.user' falhou: {ex1.Message}");
                    
                    // Se falhar, tentar parsear como ProfileResponse direto (formato alternativo)
                    try
                    {
                        response = JsonUtility.FromJson<ProfileResponse>(responseText);
                        if (response != null && response.success && response.user != null)
                        {
                            if (enableDebugLogs)
                                Debug.Log($"[GuestInitializer] ✅ Resposta parseada no formato direto 'user'");
                        }
                        else
                        {
                            response = null;
                        }
                    }
                    catch (Exception ex2)
                    {
                        if (enableDebugLogs)
                            Debug.LogError($"[GuestInitializer] ❌ Erro ao parsear resposta em ambos os formatos: {ex2.Message}");
                    }
                }
                
                if (response != null && response.success && response.user != null)
                {
                    currentPoints = response.user.points;
                    
                    // Salvar pontos localmente
                    PlayerPrefs.SetInt("user_points", currentPoints);
                    
                    // Salvar nome do guest se disponível
                    string guestName = "";
                    if (!string.IsNullOrEmpty(response.user.display_name))
                    {
                        guestName = response.user.display_name;
                        PlayerPrefs.SetString("guest_name", guestName);
                        if (enableDebugLogs)
                            Debug.Log($"[GuestInitializer] ✅ Nome do guest salvo: {guestName}");
                    }
                    else if (!string.IsNullOrEmpty(response.user.name))
                    {
                        guestName = response.user.name;
                        PlayerPrefs.SetString("guest_name", guestName);
                        if (enableDebugLogs)
                            Debug.Log($"[GuestInitializer] ✅ Nome do guest salvo: {guestName}");
                    }
                    
                    // Salvar email se disponível
                    if (!string.IsNullOrEmpty(response.user.email))
                    {
                        PlayerPrefs.SetString("guest_email", response.user.email);
                    }

                    // Salvar guest_public_id se disponível
                    if (!string.IsNullOrEmpty(response.user.guest_public_id))
                    {
                        currentGuestPublicId = response.user.guest_public_id;
                        PlayerPrefs.SetString("guest_public_id", currentGuestPublicId);
                    }
                    
                    PlayerPrefs.Save();
                    
                    // Atualizar AuthManager se o usuário estiver logado como guest
                    if (!string.IsNullOrEmpty(guestName))
                    {
                        var authManager = AuthManager.Instance;
                        if (authManager != null && authManager.IsAuthenticated())
                        {
                            var session = authManager.GetCurrentSession();
                            if (session != null && session.userId == currentGuestId)
                            {
                                authManager.UpdateSession(s => 
                                {
                                    s.displayName = guestName;
                                    if (!string.IsNullOrEmpty(response.user.email))
                                        s.email = response.user.email;
                                });
                                if (enableDebugLogs)
                                    Debug.Log($"[GuestInitializer] ✅ AuthManager atualizado: Nome={guestName}, Email={response.user.email}");
                            }
                        }
                    }
                    
                    // Notificar React sobre pontos carregados
                    NotifyReactAboutPoints(currentPoints);
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[GuestInitializer] ✅ Perfil completo carregado do servidor:");
                        Debug.Log($"[GuestInitializer]    - Pontos: {currentPoints}");
                        Debug.Log($"[GuestInitializer]    - Nome: {guestName}");
                        Debug.Log($"[GuestInitializer]    - Email: {response.user.email ?? "não informado"}");
                        Debug.Log($"[GuestInitializer]    - Level: {response.user.level}");
                        Debug.Log($"[GuestInitializer]    - Lifetime Points: {response.user.lifetime_points}");
                    }
                }
                else
                {
                    if (enableDebugLogs)
                        Debug.LogWarning("[GuestInitializer] ⚠️ Resposta do servidor inválida ou sem dados de pontos");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GuestInitializer] ❌ Erro ao processar resposta: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[GuestInitializer] ⚠️ Erro ao carregar pontos: {request.error}");
        }
        
        request.Dispose();
    }
    
    /// <summary>
    /// Notifica React sobre pontos carregados
    /// </summary>
    private void NotifyReactAboutPoints(int points)
    {
        // Antes de notificar pontos, garantir que identidade está sincronizada
        SyncIdentityWithReact();
        
        // Buscar UniWebView na cena para notificar React
        var webView = UnityEngine.Object.FindObjectOfType<UniWebView>();
        if (webView == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[GuestInitializer] ⚠️ UniWebView não encontrado - não é possível notificar React");
            return;
        }
        
        try
        {
            // Adicionado: window.setGuestData para sincronizar identidade e evitar duplicidade
            string script = $@"
                try {{
                    // 1. Sincronizar dados de identidade (IMPEDIR DUPLICIDADE)
                    if(typeof localStorage !== 'undefined') {{
                        localStorage.setItem('guest_id', '{currentGuestId}');
                        localStorage.setItem('guest_public_id', '{currentGuestPublicId}');
                        localStorage.setItem('device_id', '{currentDeviceId}');
                        localStorage.setItem('is_guest', 'true');
                        console.log('[Unity] 🆔 Identidade sincronizada: {currentGuestId} ({currentGuestPublicId})');
                    }}

                    // 2. Notificar pontos
                    if(typeof window.onPointsLoadedFromServer === 'function') {{
                        window.onPointsLoadedFromServer({points});
                        console.log('[Unity] ✅ Pontos carregados: {points}');
                    }} else if(typeof window.onPointsSentSuccessfully === 'function') {{
                        window.onPointsSentSuccessfully(0, {points});
                    }}
                }} catch(e) {{
                    console.error('[Unity] ❌ Erro ao sincronizar dados com React:', e);
                }}
            ";
            
            webView.EvaluateJavaScript(script, (payload) =>
            {
                if (enableDebugLogs)
                    Debug.Log($"[GuestInitializer] 📤 Identidade e pontos ({points}) sincronizados com React");
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GuestInitializer] ❌ Erro ao notificar React: {ex.Message}");
        }
    }

    /// <summary>
    /// Sincroniza identidade (guest_id/device_id) com o React/WebView
    /// Tenta múltiplas vezes até o WebView estar pronto
    /// </summary>
    private void SyncIdentityWithReact()
    {
        if (currentGuestId <= 0 || string.IsNullOrEmpty(currentDeviceId))
        {
            if (enableDebugLogs)
                Debug.LogWarning("[GuestInitializer] ⚠️ Não foi possível sincronizar: IDs ausentes");
            return;
        }

        // Tentar sincronizar imediatamente
        StartCoroutine(SyncIdentityWithReactCoroutine(0));
    }

    /// <summary>
    /// Corrotina para sincronizar identidade com retry
    /// </summary>
    private IEnumerator SyncIdentityWithReactCoroutine(int attempt)
    {
        const int maxAttempts = 10;
        const float delayBetweenAttempts = 0.5f;

        if (attempt >= maxAttempts)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[GuestInitializer] ⚠️ Máximo de tentativas de sincronização atingido");
            yield break;
        }

        var webView = UnityEngine.Object.FindObjectOfType<UniWebView>();
        if (webView == null)
        {
            if (enableDebugLogs && attempt == 0)
                Debug.Log("[GuestInitializer] ⏳ WebView ainda não está pronto, aguardando...");
            
            yield return new WaitForSeconds(delayBetweenAttempts);
            StartCoroutine(SyncIdentityWithReactCoroutine(attempt + 1));
            yield break;
        }

        bool shouldRetry = false;
        
        try
        {
            // Escapar caracteres especiais no device_id para JavaScript
            string safeDeviceId = currentDeviceId.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
            
            string script = $@"
                try {{
                    if(typeof localStorage !== 'undefined') {{
                        // 1. LIMPAR IDENTIDADES ANTIGAS (EVITAR DUPLICIDADE)
                        const oldId = localStorage.getItem('guest_id');
                        const oldDevice = localStorage.getItem('device_id');
                        
                        if(oldDevice && oldDevice.startsWith('web_')) {{
                            console.warn('[Unity] 🗑️ Removendo identidade WebView duplicada:', oldDevice);
                            localStorage.removeItem('guest_id');
                            localStorage.removeItem('device_id');
                            localStorage.removeItem('pix_points');
                        }}

                        // 2. Sincronizar localStorage com identidade Unity (Master)
                        localStorage.setItem('guest_id', '{currentGuestId}');
                        localStorage.setItem('guest_public_id', '{currentGuestPublicId}');
                        localStorage.setItem('device_id', '{safeDeviceId}');
                        localStorage.setItem('is_guest', 'true');
                        console.log('[Unity] 🆔 Identidade Master sincronizada: guest_id={currentGuestId}, public_id={currentGuestPublicId}');
                    }}

                    // 3. Chamar função global se existir
                    if(typeof window.setGuestData === 'function') {{
                        window.setGuestData({{
                            guest_id: {currentGuestId},
                            guest_public_id: '{currentGuestPublicId}',
                            device_id: '{safeDeviceId}'
                        }});
                    }}
                }} catch(e) {{
                    console.error('[Unity] ❌ Erro ao sincronizar identidade:', e);
                }}
            ";

            webView.EvaluateJavaScript(script, (payload) =>
            {
                if (enableDebugLogs)
                    Debug.Log($"[GuestInitializer] ✅ Identidade sincronizada com React (tentativa {attempt + 1}): guest_id={currentGuestId}");
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GuestInitializer] ❌ Erro ao sincronizar identidade com React: {ex.Message}");
            
            // Marcar para tentar novamente se não foi a última tentativa
            if (attempt < maxAttempts - 1)
            {
                shouldRetry = true;
            }
        }
        
        // Tentar novamente fora do bloco catch (yield não pode estar dentro de catch)
        if (shouldRetry)
        {
            yield return new WaitForSeconds(delayBetweenAttempts);
            StartCoroutine(SyncIdentityWithReactCoroutine(attempt + 1));
        }
    }

    [System.Serializable]
    private class ProfileResponse
    {
        public bool success;
        public UserData user;
    }
    
    [System.Serializable]
    private class ProfileDataResponse
    {
        public bool success;
        public string message;
        public ProfileDataWrapper data;
    }
    
    [System.Serializable]
    private class ProfileDataWrapper
    {
        public UserData user;
    }
    
    [System.Serializable]
    private class UserData
    {
        public int points;
        public int level;
        public int lifetime_points;
        public string display_name;
        public string name;
        public string email;
        public string guest_public_id;
    }

    [System.Serializable]
    private class GuestResponse
    {
        public string status;
        public string message;
        public int guest_id;
        public string guest_public_id;
        public string device_id;
        public int points;
        public int level;
        public int lifetime_points;
        public bool was_created;
    }
}

