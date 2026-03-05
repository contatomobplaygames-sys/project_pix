using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gerenciador central de autenticação e sessão do usuário
/// Singleton que persiste entre cenas
/// </summary>
public class AuthManager : MonoBehaviour
{
    private static AuthManager _instance;
    
    [Header("Settings")]
    [Tooltip("Se verdadeiro, mantém a sessão mesmo após fechar o app")]
    [SerializeField] private bool persistSession = true;
    
    [Tooltip("Se verdadeiro, exibe logs detalhados")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Tooltip("Duração da sessão em horas (0 = sem expiração)")]
    [SerializeField] private int sessionDurationHours = 24;
    
    [Header("Current Session")]
    [SerializeField] private UserSession currentSession;
    
    // Eventos
    [Header("Events")]
    public UnityEvent<UserSession> OnLoginSuccess;
    public UnityEvent OnLogoutSuccess;
    public UnityEvent<string> OnLoginError;
    public UnityEvent<UserSession> OnSessionUpdated;
    
    // Referências
    private ApiClient apiClient;
    private ProfileManager profileManager;
    private WebViewLauncher webViewLauncher;
    
    // Constantes
    private const string SESSION_KEY = "UserSession_Data";
    private const string SESSION_TIMESTAMP_KEY = "UserSession_Timestamp";
    
    #region Singleton
    
    /// <summary>
    /// Instância singleton do AuthManager
    /// </summary>
    public static AuthManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AuthManager>();
                
                if (_instance == null)
                {
                    GameObject go = new GameObject("AuthManager");
                    _instance = go.AddComponent<AuthManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeManager();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    
    #endregion
    
    #region Initialization
    
    private void InitializeManager()
    {
        if (enableDebugLogs)
            Debug.Log("[AuthManager] 🔐 Inicializando gerenciador de autenticação...");
        
        // Criar sessão vazia se não existir
        if (currentSession == null)
        {
            currentSession = new UserSession();
        }
        
        // Tentar carregar sessão salva
        if (persistSession)
        {
            LoadSession();
        }
        
        // Buscar referências
        FindReferences();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[AuthManager] ✅ Autenticação inicializada. Usuário autenticado: {IsAuthenticated()}");
            if (IsAuthenticated())
            {
                Debug.Log($"[AuthManager] 👤 Sessão ativa: {currentSession}");
            }
        }
    }
    
    private void Start()
    {
        FindReferences();
    }
    
    private void FindReferences()
    {
        if (apiClient == null)
            apiClient = FindObjectOfType<ApiClient>();
        
        if (profileManager == null)
            profileManager = FindObjectOfType<ProfileManager>();
        
        if (webViewLauncher == null)
            webViewLauncher = FindObjectOfType<WebViewLauncher>();
    }
    
    #endregion
    
    #region Public Methods - Login/Logout
    
    /// <summary>
    /// Realiza login com dados recebidos do WebView ou API
    /// </summary>
    public void Login(int userId, string username, string email, string token, string additionalData = null)
    {
        if (userId <= 0 || string.IsNullOrEmpty(username))
        {
            string error = "Dados de login inválidos";
            if (enableDebugLogs)
                Debug.LogError($"[AuthManager] ❌ {error}");
            
            OnLoginError?.Invoke(error);
            return;
        }
        
        if (enableDebugLogs)
            Debug.Log($"[AuthManager] 🔓 Fazendo login: User ID={userId}, Username={username}, Email={email}");
        
        // Criar nova sessão
        currentSession = new UserSession(userId, username, email, token);
        
        // Processar dados adicionais se fornecidos
        if (!string.IsNullOrEmpty(additionalData))
        {
            ProcessAdditionalLoginData(additionalData);
        }
        
        // Salvar sessão
        if (persistSession)
        {
            SaveSession();
        }
        
        // Atualizar componentes dependentes
        UpdateDependentComponents();
        
        if (enableDebugLogs)
            Debug.Log($"[AuthManager] ✅ Login realizado com sucesso! {currentSession}");
        
        // Disparar evento de sucesso
        OnLoginSuccess?.Invoke(currentSession);
        
        // Carregar dados completos do perfil
        LoadUserProfile();
    }
    
    /// <summary>
    /// Faz logout e limpa a sessão
    /// </summary>
    public void Logout()
    {
        if (enableDebugLogs)
            Debug.Log("[AuthManager] 🔒 Fazendo logout...");
        
        // Limpar sessão
        if (currentSession != null)
        {
            currentSession.Clear();
        }
        else
        {
            currentSession = new UserSession();
        }
        
        // Remover do armazenamento
        if (persistSession)
        {
            PlayerPrefs.DeleteKey(SESSION_KEY);
            PlayerPrefs.DeleteKey(SESSION_TIMESTAMP_KEY);
            PlayerPrefs.Save();
        }
        
        if (enableDebugLogs)
            Debug.Log("[AuthManager] ✅ Logout realizado com sucesso!");
        
        // Disparar evento
        OnLogoutSuccess?.Invoke();
    }
    
    /// <summary>
    /// Verifica se o usuário está autenticado
    /// </summary>
    public bool IsAuthenticated()
    {
        return currentSession != null && currentSession.IsValid();
    }
    
    /// <summary>
    /// Obtém a sessão atual do usuário
    /// </summary>
    public UserSession GetCurrentSession()
    {
        return currentSession;
    }
    
    /// <summary>
    /// Obtém o ID do usuário atual
    /// </summary>
    public int GetUserId()
    {
        return currentSession != null ? currentSession.userId : 0;
    }
    
    /// <summary>
    /// Obtém o nome do usuário atual
    /// </summary>
    public string GetUsername()
    {
        return currentSession != null ? currentSession.username : "";
    }
    
    /// <summary>
    /// Obtém o email do usuário atual
    /// </summary>
    public string GetUserEmail()
    {
        return currentSession != null ? currentSession.email : "";
    }
    
    /// <summary>
    /// Obtém o saldo do usuário atual
    /// </summary>
    public float GetUserBalance()
    {
        return currentSession != null ? currentSession.balance : 0f;
    }
    
    #endregion
    
    #region Session Management
    
    /// <summary>
    /// Salva a sessão no PlayerPrefs
    /// </summary>
    private void SaveSession()
    {
        if (currentSession == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[AuthManager] ⚠️ Tentando salvar sessão nula");
            return;
        }
        
        try
        {
            string json = currentSession.ToJson();
            PlayerPrefs.SetString(SESSION_KEY, json);
            PlayerPrefs.SetString(SESSION_TIMESTAMP_KEY, DateTime.Now.ToString("o"));
            PlayerPrefs.Save();
            
            if (enableDebugLogs)
                Debug.Log("[AuthManager] 💾 Sessão salva com sucesso");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuthManager] ❌ Erro ao salvar sessão: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Carrega a sessão do PlayerPrefs
    /// </summary>
    private void LoadSession()
    {
        if (!PlayerPrefs.HasKey(SESSION_KEY))
        {
            if (enableDebugLogs)
                Debug.Log("[AuthManager] 📭 Nenhuma sessão salva encontrada");
            return;
        }
        
        try
        {
            string json = PlayerPrefs.GetString(SESSION_KEY);
            currentSession = UserSession.FromJson(json);
            
            if (currentSession == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[AuthManager] ⚠️ Falha ao carregar sessão salva");
                currentSession = new UserSession();
                return;
            }
            
            // Verificar se a sessão expirou
            if (IsSessionExpired())
            {
                if (enableDebugLogs)
                    Debug.Log("[AuthManager] ⏰ Sessão expirada. Limpando...");
                
                Logout();
                return;
            }
            
            // Atualizar último login
            currentSession.UpdateLastLogin();
            SaveSession();
            
            if (enableDebugLogs)
                Debug.Log($"[AuthManager] 📂 Sessão carregada: {currentSession}");
            
            // Atualizar componentes
            UpdateDependentComponents();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuthManager] ❌ Erro ao carregar sessão: {ex.Message}");
            currentSession = new UserSession();
        }
    }
    
    /// <summary>
    /// Verifica se a sessão expirou
    /// </summary>
    private bool IsSessionExpired()
    {
        if (sessionDurationHours <= 0)
            return false; // Sem expiração
        
        if (!PlayerPrefs.HasKey(SESSION_TIMESTAMP_KEY))
            return true;
        
        try
        {
            string timestampStr = PlayerPrefs.GetString(SESSION_TIMESTAMP_KEY);
            DateTime timestamp = DateTime.Parse(timestampStr);
            DateTime expiration = timestamp.AddHours(sessionDurationHours);
            
            return DateTime.Now > expiration;
        }
        catch
        {
            return true; // Se houver erro, considerar expirada
        }
    }
    
    /// <summary>
    /// Atualiza a sessão com novos dados
    /// </summary>
    public void UpdateSession(Action<UserSession> updateAction)
    {
        if (currentSession == null)
        {
            Debug.LogWarning("[AuthManager] ⚠️ Tentando atualizar sessão nula");
            return;
        }
        
        updateAction?.Invoke(currentSession);
        
        if (persistSession)
        {
            SaveSession();
        }
        
        OnSessionUpdated?.Invoke(currentSession);
    }
    
    #endregion
    
    #region Profile & Data
    
    /// <summary>
    /// Carrega o perfil completo do usuário da API
    /// </summary>
    private void LoadUserProfile()
    {
        if (!IsAuthenticated())
        {
            if (enableDebugLogs)
                Debug.LogWarning("[AuthManager] ⚠️ Não autenticado. Não é possível carregar perfil.");
            return;
        }
        
        FindReferences();
        
        if (apiClient == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[AuthManager] ⚠️ ApiClient não encontrado. Não é possível carregar perfil.");
            return;
        }
        
        StartCoroutine(LoadUserProfileRoutine());
    }
    
    private IEnumerator LoadUserProfileRoutine()
    {
        string url = $"get_user_profile.php?user_id={currentSession.userId}";
        
        if (enableDebugLogs)
            Debug.Log($"[AuthManager] 📡 Carregando perfil do usuário: {url}");
        
        yield return StartCoroutine(apiClient.GetJson(url,
            (response) => OnProfileLoaded(response),
            (error) => OnProfileLoadError(error)
        ));
    }
    
    private void OnProfileLoaded(string response)
    {
        try
        {
            // Tentar parsear resposta como ProfileResponse do ProfileManager
            var profileData = JsonUtility.FromJson<ProfileManager.ProfileResponse>(response);
            
            if (profileData != null && profileData.success && profileData.user != null)
            {
                // Atualizar sessão com dados do perfil
                UpdateSession(session =>
                {
                    session.displayName = profileData.user.display_name;
                    session.email = profileData.user.email;
                    session.balance = profileData.user.balance;
                    session.referralCode = profileData.user.referral_code;
                    session.guestPublicId = profileData.user.guest_public_id;
                    
                    if (profileData.stats != null)
                    {
                        session.totalEarned = profileData.stats.total_earned;
                        session.totalGamesPlayed = profileData.stats.completed_tasks;
                    }
                });
                
                if (enableDebugLogs)
                    Debug.Log($"[AuthManager] ✅ Perfil carregado: {currentSession}");
                
                OnSessionUpdated?.Invoke(currentSession);
            }
            else
            {
                Debug.LogWarning($"[AuthManager] ⚠️ Resposta de perfil inválida: {response}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AuthManager] ❌ Erro ao processar perfil: {ex.Message}");
        }
    }
    
    private void OnProfileLoadError(string error)
    {
        Debug.LogError($"[AuthManager] ❌ Erro ao carregar perfil: {error}");
    }
    
    /// <summary>
    /// Processa dados adicionais de login (JSON)
    /// </summary>
    private void ProcessAdditionalLoginData(string jsonData)
    {
        try
        {
            // Tentar parsear dados adicionais e atualizar sessão
            var additionalData = JsonUtility.FromJson<UserSession>(jsonData);
            if (additionalData != null)
            {
                if (additionalData.balance > 0)
                    currentSession.balance = additionalData.balance;
                
                if (additionalData.points > 0)
                    currentSession.points = additionalData.points;
                
                if (!string.IsNullOrEmpty(additionalData.referralCode))
                    currentSession.referralCode = additionalData.referralCode;
                
                if (additionalData.level > 0)
                    currentSession.level = additionalData.level;
            }
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[AuthManager] ⚠️ Não foi possível processar dados adicionais: {ex.Message}");
        }
    }
    
    #endregion
    
    #region Component Integration
    
    /// <summary>
    /// Atualiza componentes que dependem da autenticação
    /// </summary>
    private void UpdateDependentComponents()
    {
        FindReferences();
        
        // Atualizar WebViewLauncher com userId
        if (webViewLauncher != null && IsAuthenticated())
        {
            webViewLauncher.SetUserId(currentSession.userId);
        }
        
        // Atualizar ProfileManager
        if (profileManager != null && IsAuthenticated())
        {
            profileManager.LoadProfile(currentSession.userId);
        }
    }
    
    #endregion
    
    #region Debug Methods
    
    [ContextMenu("Debug: Mostrar Sessão")]
    private void DebugShowSession()
    {
        Debug.Log("========== [AuthManager] SESSÃO ATUAL ==========");
        if (currentSession != null)
        {
            Debug.Log($"Autenticado: {IsAuthenticated()}");
            Debug.Log($"User ID: {currentSession.userId}");
            Debug.Log($"Username: {currentSession.username}");
            Debug.Log($"Email: {currentSession.email}");
            Debug.Log($"Display Name: {currentSession.displayName}");
            Debug.Log($"Balance: R$ {currentSession.balance:F2}");
            Debug.Log($"Points: {currentSession.points}");
            Debug.Log($"Token: {currentSession.sessionToken}");
            Debug.Log($"Type: {currentSession.userType}");
        }
        else
        {
            Debug.Log("Nenhuma sessão ativa");
        }
        Debug.Log("================================================");
    }
    
    [ContextMenu("Debug: Limpar Sessão")]
    private void DebugClearSession()
    {
        Logout();
        Debug.Log("[AuthManager] 🗑️ Sessão limpa!");
    }
    
    [ContextMenu("Debug: Simular Login")]
    private void DebugSimulateLogin()
    {
        Login(
            userId: 123,
            username: "teste_user",
            email: "teste@example.com",
            token: "token_teste_" + DateTime.Now.Ticks
        );
        Debug.Log("[AuthManager] 🎭 Login simulado!");
    }
    
    #endregion
}

