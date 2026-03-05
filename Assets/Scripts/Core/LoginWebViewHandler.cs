using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Handler responsável por receber dados de login do WebView
/// e processar a autenticação na Unity
/// </summary>
[RequireComponent(typeof(UniWebView))]
public class LoginWebViewHandler : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Se verdadeiro, exibe logs detalhados")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Tooltip("Fechar WebView automaticamente após login bem-sucedido")]
    [SerializeField] private bool closeOnSuccessfulLogin = true;
    
    [Tooltip("Delay em segundos antes de fechar após login")]
    [SerializeField] private float closeDelay = 1.5f;
    
    private UniWebView webView;
    private AuthManager authManager;
    private bool isInitialized = false;
    
    #region Initialization
    
    private void Awake()
    {
        webView = GetComponent<UniWebView>();
        if (webView == null)
        {
            Debug.LogError("[LoginWebViewHandler] UniWebView component not found!");
            return;
        }
        
        InitializeHandler();
    }
    
    private void Start()
    {
        // Garantir que AuthManager está inicializado
        authManager = AuthManager.Instance;
        
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] ✅ Handler inicializado e pronto para receber login");
    }
    
    private void InitializeHandler()
    {
        if (isInitialized)
            return;
        
        // Registrar listener para mensagens do WebView
        webView.OnMessageReceived += OnWebViewMessage;
        
        // Registrar handler para interceptar navegações e validar URLs
        webView.OnPageStarted += OnPageStarted;
        
        // Adicionar scheme para comunicação (uniwebview://)
        webView.AddUrlScheme("uniwebview");
        
        isInitialized = true;
        
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] 🔧 Handler configurado para receber mensagens");
    }
    
    /// <summary>
    /// Intercepta navegações e valida URLs para evitar abertura de arquivos locais como executáveis
    /// </summary>
    private void OnPageStarted(UniWebView webView, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            if (enableDebugLogs)
                Debug.LogWarning("[LoginWebViewHandler] ⚠️ URL vazia detectada - bloqueando navegação");
            webView.Stop();
            return;
        }
        
        // Verificar se a URL é um arquivo local (file:// ou caminho absoluto do Windows)
        if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) || 
            url.StartsWith("C:\\", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("D:\\", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("E:\\", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("\\Temp\\") ||
            url.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            if (enableDebugLogs)
                Debug.LogError($"[LoginWebViewHandler] ❌ Tentativa de abrir arquivo local bloqueada: {url}");
            
            // Bloquear a navegação
            webView.Stop();
            
            // Se for um arquivo .txt ou executável, logar como erro crítico
            if (url.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || 
                url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"[LoginWebViewHandler] 🚫 BLOQUEIO DE SEGURANÇA: Tentativa de executar arquivo local detectada: {url}");
            }
            
            return;
        }
        
        // Verificar se a URL é válida (deve começar com http://, https://, ou uniwebview://)
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("uniwebview://", StringComparison.OrdinalIgnoreCase))
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[LoginWebViewHandler] ⚠️ URL com scheme inválido detectada: {url}");
            
            // Bloquear navegação para URLs inválidas
            webView.Stop();
            return;
        }
        
        if (enableDebugLogs)
            Debug.Log($"[LoginWebViewHandler] ✅ Navegação permitida para: {url}");
    }
    
    #endregion
    
    #region WebView Message Handling
    
    /// <summary>
    /// Chamado quando uma mensagem é recebida do WebView
    /// </summary>
    private void OnWebViewMessage(UniWebView webView, UniWebViewMessage message)
    {
        if (enableDebugLogs)
            Debug.Log($"[LoginWebViewHandler] 📨 Mensagem recebida: {message.Path}");
        
        // Processar diferentes tipos de mensagens
        switch (message.Path.ToLower())
        {
            case "login":
                HandleLogin(message.Args);
                break;
            
            case "loginSuccess":
            case "login_success":
                HandleLoginSuccess(message.Args);
                break;
            
            case "logout":
                HandleLogout(message.Args);
                break;
            
            case "updateUserData":
            case "update_user_data":
                HandleUpdateUserData(message.Args);
                break;
            
            case "sessionData":
            case "session_data":
                HandleSessionData(message.Args);
                break;
            
            case "setuserdata":
            case "set_user_data":
                HandleSetUserData(message.Args);
                break;
            
            case "reloadprofile":
            case "reload_profile":
            case "profileupdated":
            case "profile_updated":
                HandleProfileUpdated(message.Args);
                break;
            
            default:
                if (enableDebugLogs)
                    Debug.Log($"[LoginWebViewHandler] ⚠️ Mensagem não reconhecida: {message.Path}");
                break;
        }
    }
    
    #endregion
    
    #region Login Handlers
    
    /// <summary>
    /// Processa login básico
    /// Espera parâmetros: userId, username, email, token
    /// </summary>
    private void HandleLogin(Dictionary<string, string> args)
    {
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] 🔓 Processando login...");
        
        try
        {
            // Extrair dados do dicionário
            if (!args.ContainsKey("userId") || !args.ContainsKey("username"))
            {
                Debug.LogError("[LoginWebViewHandler] ❌ Dados de login incompletos! userId e username são obrigatórios.");
                LogAvailableArgs(args);
                return;
            }
            
            int userId = int.Parse(args["userId"]);
            string username = args["username"];
            string email = args.ContainsKey("email") ? args["email"] : "";
            string token = args.ContainsKey("token") ? args["token"] : GenerateToken();
            
            // Criar JSON com dados adicionais se disponíveis
            string additionalData = CreateAdditionalDataJson(args);
            
            // Fazer login através do AuthManager
            if (authManager == null)
                authManager = AuthManager.Instance;
            
            authManager.Login(userId, username, email, token, additionalData);
            
            if (enableDebugLogs)
                Debug.Log($"[LoginWebViewHandler] ✅ Login processado: User {userId} - {username}");
            
            // Fechar WebView se configurado
            if (closeOnSuccessfulLogin)
            {
                Invoke(nameof(CloseWebView), closeDelay);
            }
            
            // Notificar JavaScript do sucesso
            NotifyWebView("onLoginComplete", "success");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LoginWebViewHandler] ❌ Erro ao processar login: {ex.Message}");
            NotifyWebView("onLoginComplete", $"error:{ex.Message}");
        }
    }
    
    /// <summary>
    /// Processa login bem-sucedido com dados completos
    /// </summary>
    private void HandleLoginSuccess(Dictionary<string, string> args)
    {
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] ✅ Login bem-sucedido recebido do WebView");
        
        HandleLogin(args); // Processa da mesma forma
    }
    
    /// <summary>
    /// Processa logout
    /// </summary>
    private void HandleLogout(Dictionary<string, string> args)
    {
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] 🔒 Processando logout...");
        
        if (authManager == null)
            authManager = AuthManager.Instance;
        
        authManager.Logout();
        
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] ✅ Logout processado");
        
        // Notificar JavaScript
        NotifyWebView("onLogoutComplete", "success");
    }
    
    /// <summary>
    /// Atualiza dados do usuário
    /// </summary>
    private void HandleUpdateUserData(Dictionary<string, string> args)
    {
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] 🔄 Atualizando dados do usuário...");
        
        if (authManager == null)
            authManager = AuthManager.Instance;
        
        if (!authManager.IsAuthenticated())
        {
            Debug.LogWarning("[LoginWebViewHandler] ⚠️ Não autenticado. Não é possível atualizar dados.");
            return;
        }
        
        authManager.UpdateSession(session =>
        {
            // Atualizar balance
            if (args.ContainsKey("balance"))
            {
                if (float.TryParse(args["balance"], out float balance))
                    session.UpdateBalance(balance);
            }
            
            // Atualizar points
            if (args.ContainsKey("points"))
            {
                if (int.TryParse(args["points"], out int points))
                    session.points = points;
            }
            
            // Atualizar displayName
            if (args.ContainsKey("displayName"))
            {
                session.displayName = args["displayName"];
            }
            
            // Atualizar level
            if (args.ContainsKey("level"))
            {
                if (int.TryParse(args["level"], out int level))
                    session.level = level;
            }
        });
        
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] ✅ Dados atualizados");
    }
    
    /// <summary>
    /// Recebe dados do usuário do WebView (suporta usuários regulares e convidados)
    /// Chamado quando o JavaScript envia: uniwebview://setUserData?user_id=123&email=user@email.com&is_guest=false&guest_id=456
    /// </summary>
    private void HandleSetUserData(Dictionary<string, string> args)
    {
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] 🔐 Recebendo dados do usuário do WebView...");
        
        try
        {
            // Verificar se é convidado
            bool isGuest = args.ContainsKey("is_guest") && 
                          (args["is_guest"].ToLower() == "true" || args["is_guest"] == "1");
            
            if (isGuest)
            {
                // Processar usuário convidado
                HandleGuestUser(args);
            }
            else
            {
                // Processar usuário regular
                HandleRegularUser(args);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LoginWebViewHandler] ❌ Erro ao processar dados do usuário: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Processa dados de usuário regular
    /// </summary>
    private void HandleRegularUser(Dictionary<string, string> args)
    {
        if (!args.ContainsKey("user_id"))
        {
            Debug.LogWarning("[LoginWebViewHandler] ⚠️ user_id não fornecido para usuário regular");
            return;
        }
        
        int userId = int.Parse(args["user_id"]);
        string email = args.ContainsKey("email") ? args["email"] : "";
        string username = "";
        if (args.ContainsKey("username"))
            username = args["username"];
        else if (args.ContainsKey("user_name"))
            username = args["user_name"];
        else if (!string.IsNullOrEmpty(email))
            username = email.Split('@')[0];
        else
            username = $"User_{userId}";
        
        // Salvar dados no PlayerPrefs
        PlayerPrefs.SetInt("user_id", userId);
        PlayerPrefs.SetString("user_email", email);
        PlayerPrefs.SetString("is_guest", "false");
        PlayerPrefs.DeleteKey("guest_id"); // Limpar guest_id se existir
        PlayerPrefs.Save();
        
        // Atualizar GameManager se disponível
        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.SetPlayerId(userId);
        }
        
        // Atualizar AuthManager para garantir sincronização
        if (authManager == null)
            authManager = AuthManager.Instance;
        
        if (authManager != null)
        {
            // Criar ou atualizar sessão no AuthManager
            // username já foi definido acima, usar ele
            string token = $"webview_token_{userId}_{DateTime.Now.Ticks}";
            authManager.Login(userId, username, email, token);
            
            if (enableDebugLogs)
                Debug.Log($"[LoginWebViewHandler] ✅ AuthManager atualizado com dados do usuário");
        }
        
        if (enableDebugLogs)
            Debug.Log($"[LoginWebViewHandler] ✅ Dados de usuário regular salvos: User ID={userId}, Email={email}");
    }
    
    /// <summary>
    /// Processa dados de usuário convidado
    /// </summary>
    private void HandleGuestUser(Dictionary<string, string> args)
    {
        if (!args.ContainsKey("guest_id") && !args.ContainsKey("user_id"))
        {
            Debug.LogWarning("[LoginWebViewHandler] ⚠️ guest_id ou user_id não fornecido para convidado");
            return;
        }
        
        // guest_id pode vir como user_id para convidados
        string guestIdStr = args.ContainsKey("guest_id") ? args["guest_id"] : args["user_id"];
        
        if (!int.TryParse(guestIdStr, out int guestId))
        {
            Debug.LogWarning($"[LoginWebViewHandler] ⚠️ guest_id inválido: {guestIdStr}");
            return;
        }
        
        // Salvar dados no PlayerPrefs
        PlayerPrefs.SetInt("guest_id", guestId);
        PlayerPrefs.SetInt("user_id", guestId); // Usar guest_id como user_id também
        PlayerPrefs.SetString("is_guest", "true");
        PlayerPrefs.DeleteKey("user_email"); // Limpar email se existir

        // Salvar public_id se fornecido
        if (args.ContainsKey("guest_public_id") && !string.IsNullOrEmpty(args["guest_public_id"]))
        {
            PlayerPrefs.SetString("guest_public_id", args["guest_public_id"]);
        }
        
        // Salvar nome do guest se fornecido
        if (args.ContainsKey("display_name") && !string.IsNullOrEmpty(args["display_name"]))
        {
            PlayerPrefs.SetString("guest_name", args["display_name"]);
        }
        else if (args.ContainsKey("name") && !string.IsNullOrEmpty(args["name"]))
        {
            PlayerPrefs.SetString("guest_name", args["name"]);
        }
        else if (args.ContainsKey("username") && !string.IsNullOrEmpty(args["username"]))
        {
            PlayerPrefs.SetString("guest_name", args["username"]);
        }
        
        PlayerPrefs.Save();
        
        // Atualizar GameManager se disponível
        var gameManager = FindObjectOfType<GameManager>();
        if (gameManager != null)
        {
            gameManager.SetPlayerId(guestId, isGuest: true);
        }
        
        // Atualizar AuthManager para garantir sincronização
        if (authManager == null)
            authManager = AuthManager.Instance;
        
        if (authManager != null)
        {
            // Obter nome do guest (do PlayerPrefs ou dos argumentos)
            string guestName = PlayerPrefs.GetString("guest_name", "");
            if (string.IsNullOrEmpty(guestName))
            {
                // Tentar obter dos argumentos
                if (args.ContainsKey("display_name") && !string.IsNullOrEmpty(args["display_name"]))
                    guestName = args["display_name"];
                else if (args.ContainsKey("name") && !string.IsNullOrEmpty(args["name"]))
                    guestName = args["name"];
                else if (args.ContainsKey("username") && !string.IsNullOrEmpty(args["username"]))
                    guestName = args["username"];
            }
            
            // Se ainda não tem nome, usar padrão ou tentar carregar do servidor
            if (string.IsNullOrEmpty(guestName))
            {
                guestName = $"Guest_{guestId}";
                // Tentar carregar do GuestInitializer se disponível
                var guestInit = GuestInitializer.Instance;
                if (guestInit != null && guestInit.IsInitialized())
                {
                    string loadedName = guestInit.GetGuestName();
                    if (!string.IsNullOrEmpty(loadedName) && loadedName != "Visitante")
                    {
                        guestName = loadedName;
                    }
                }
            }
            
            string username = guestName;
            string email = args.ContainsKey("email") && !string.IsNullOrEmpty(args["email"]) 
                ? args["email"] 
                : $"convidado{guestId}@mobplaypix.com";
            string token = $"webview_guest_token_{guestId}_{DateTime.Now.Ticks}";
            authManager.Login(guestId, username, email, token);
            
            // Atualizar userType e displayName para guest
            authManager.UpdateSession(session =>
            {
                session.userType = "guest";
                session.displayName = guestName;
            });
            
            if (enableDebugLogs)
                Debug.Log($"[LoginWebViewHandler] ✅ AuthManager atualizado com dados do convidado: {guestName}");
        }
        
        if (enableDebugLogs)
            Debug.Log($"[LoginWebViewHandler] ✅ Dados de convidado salvos: Guest ID={guestId}");
    }
    
    /// <summary>
    /// Processa notificação de atualização de perfil do WebView
    /// Quando o perfil é atualizado no WebView, recarrega na Unity
    /// </summary>
    private void HandleProfileUpdated(Dictionary<string, string> args)
    {
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] 🔄 Perfil atualizado no WebView, recarregando na Unity...");
        
        // Recarregar perfil do GuestInitializer se for guest
        var guestInit = GuestInitializer.Instance;
        if (guestInit != null && guestInit.IsInitialized())
        {
            guestInit.ReloadProfile();
            if (enableDebugLogs)
                Debug.Log("[LoginWebViewHandler] ✅ Perfil do guest recarregado");
        }
        
        // Se tiver guest_id nos argumentos, também atualizar
        if (args.ContainsKey("guest_id"))
        {
            if (int.TryParse(args["guest_id"], out int guestId))
            {
                // Atualizar nome se fornecido
                if (args.ContainsKey("display_name") && !string.IsNullOrEmpty(args["display_name"]))
                {
                    PlayerPrefs.SetString("guest_name", args["display_name"]);
                    PlayerPrefs.Save();
                    
                    // Atualizar AuthManager
                    if (authManager == null)
                        authManager = AuthManager.Instance;
                    
                    if (authManager != null && authManager.IsAuthenticated())
                    {
                        authManager.UpdateSession(s => 
                        {
                            if (s.userId == guestId)
                                s.displayName = args["display_name"];
                        });
                    }
                    
                    if (enableDebugLogs)
                        Debug.Log($"[LoginWebViewHandler] ✅ Nome do guest atualizado: {args["display_name"]}");
                }
            }
        }
    }
    
    /// <summary>
    /// Recebe dados completos da sessão em formato JSON
    /// </summary>
    private void HandleSessionData(Dictionary<string, string> args)
    {
        if (enableDebugLogs)
            Debug.Log("[LoginWebViewHandler] 📦 Recebendo dados da sessão...");
        
        if (!args.ContainsKey("data"))
        {
            Debug.LogError("[LoginWebViewHandler] ❌ Parâmetro 'data' não encontrado!");
            return;
        }
        
        try
        {
            string jsonData = args["data"];
            var sessionData = UserSession.FromJson(jsonData);
            
            if (sessionData != null && sessionData.userId > 0)
            {
                // Fazer login com dados da sessão
                if (authManager == null)
                    authManager = AuthManager.Instance;
                
                authManager.Login(
                    sessionData.userId,
                    sessionData.username,
                    sessionData.email,
                    sessionData.sessionToken,
                    jsonData
                );
                
                if (enableDebugLogs)
                    Debug.Log("[LoginWebViewHandler] ✅ Sessão carregada do WebView");
            }
            else
            {
                Debug.LogError("[LoginWebViewHandler] ❌ Dados da sessão inválidos!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LoginWebViewHandler] ❌ Erro ao processar dados da sessão: {ex.Message}");
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Cria JSON com dados adicionais a partir dos argumentos
    /// </summary>
    private string CreateAdditionalDataJson(Dictionary<string, string> args)
    {
        var additionalData = new UserSession();
        
        if (args.ContainsKey("balance") && float.TryParse(args["balance"], out float balance))
            additionalData.balance = balance;
        
        if (args.ContainsKey("points") && int.TryParse(args["points"], out int points))
            additionalData.points = points;
        
        if (args.ContainsKey("displayName"))
            additionalData.displayName = args["displayName"];
        
        if (args.ContainsKey("referralCode"))
            additionalData.referralCode = args["referralCode"];
        
        if (args.ContainsKey("level") && int.TryParse(args["level"], out int level))
            additionalData.level = level;
        
        if (args.ContainsKey("userType"))
            additionalData.userType = args["userType"];
        
        return additionalData.ToJson();
    }
    
    /// <summary>
    /// Gera um token simples se não fornecido
    /// </summary>
    private string GenerateToken()
    {
        return $"token_{Guid.NewGuid().ToString("N")}";
    }
    
    /// <summary>
    /// Fecha o WebView
    /// </summary>
    private void CloseWebView()
    {
        if (webView != null)
        {
            webView.Hide();
            if (enableDebugLogs)
                Debug.Log("[LoginWebViewHandler] 👋 WebView fechado");
        }
    }
    
    /// <summary>
    /// Envia notificação para o JavaScript no WebView
    /// </summary>
    private void NotifyWebView(string functionName, string parameter)
    {
        if (webView == null)
            return;
        
        try
        {
            string script = $"if(typeof {functionName} === 'function') {{ {functionName}('{parameter}'); }}";
            webView.EvaluateJavaScript(script, (payload) =>
            {
                if (enableDebugLogs)
                    Debug.Log($"[LoginWebViewHandler] 📤 Notificação enviada: {functionName}");
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LoginWebViewHandler] ❌ Erro ao notificar WebView: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Loga argumentos disponíveis para debug
    /// </summary>
    private void LogAvailableArgs(Dictionary<string, string> args)
    {
        if (!enableDebugLogs)
            return;
        
        Debug.Log("[LoginWebViewHandler] 📋 Argumentos disponíveis:");
        foreach (var kvp in args)
        {
            Debug.Log($"  - {kvp.Key}: {kvp.Value}");
        }
    }
    
    #endregion
    
    #region Lifecycle
    
    private void OnDestroy()
    {
        // Limpar listeners
        if (webView != null)
        {
            webView.OnMessageReceived -= OnWebViewMessage;
            webView.OnPageStarted -= OnPageStarted;
        }
    }
    
    #endregion
    
    #region Debug Methods
    
    [ContextMenu("Debug: Simular Login do WebView")]
    private void DebugSimulateWebViewLogin()
    {
        var testArgs = new Dictionary<string, string>
        {
            { "userId", "456" },
            { "username", "webview_user" },
            { "email", "webview@example.com" },
            { "token", "test_token_123" },
            { "balance", "25.50" },
            { "points", "1200" }
        };
        
        HandleLogin(testArgs);
        Debug.Log("[LoginWebViewHandler] 🎭 Login simulado do WebView!");
    }
    
    #endregion
}

