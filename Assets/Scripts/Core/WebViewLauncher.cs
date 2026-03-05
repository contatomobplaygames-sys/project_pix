using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// Gerencia o lançamento de jogos e páginas web usando UniWebView.
/// Substitui o uso obsoleto de Application.OpenURL por uma solução integrada.
/// </summary>
[RequireComponent(typeof(UniWebView))]
public class WebViewLauncher : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("URL base para páginas web do sistema")]
    public string baseUrl = "https://app.mobplaygames.com.br/unity/k26pix/";
    
    [Header("Game Configuration")]
    [Tooltip("Lista de URLs dos jogos")]
    public string[] gameUrls = {
        "https://www.crazygames.com/game/temple-run-2",
        "https://www.crazygames.com/game/subway-surfers"
    };
    
    [Tooltip("Nomes dos jogos (deve corresponder à ordem de gameUrls)")]
    public string[] gameNames = {
        "Temple Run 2",
        "Subway Surfers"
    };
    
    [Header("Settings")]
    [Tooltip("Se verdadeiro, mostra o WebView em tela cheia")]
    public bool fullScreen = true;
    
    [Tooltip("Se verdadeiro, mostra o WebView automaticamente ao carregar")]
    public bool showOnLoad = true;
    
    private UniWebView webView;
    private int currentUserId;
    private string currentGameName = "";
    private string currentGameUrl = "";
    private DateTime sessionStartTime;
    private bool isPlaying = false;
    private ApiClient apiClient;
    private bool isInitialized = false;
    
    private void Awake()
    {
        webView = GetComponent<UniWebView>();
        if (webView == null)
        {
            Debug.LogError("[WebViewLauncher] UniWebView component not found! Adding it...");
            webView = gameObject.AddComponent<UniWebView>();
        }
        
        ConfigureWebView();
    }
    
    private void Start()
    {
        InitializeApiClient();
        ValidateGameArrays();
    }
    
    private void ConfigureWebView()
    {
        if (webView == null) return;
        
        // Nota: fullScreen e showOnStart são campos SerializeField privados do UniWebView
        // Eles devem ser configurados no Inspector do Unity, não via código
        // Se necessário, podemos usar SerializedObject, mas é melhor configurar no Inspector
        
        // Configurar callbacks
        webView.OnPageFinished += OnPageFinished;
        webView.OnPageErrorReceived += OnPageError;
        webView.OnShouldClose += OnShouldClose;
        webView.OnPageStarted += OnPageStarted;
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Intercepta navegações e valida URLs para evitar abertura de arquivos locais como executáveis
    /// </summary>
    private void OnPageStarted(UniWebView webView, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning("[WebViewLauncher] ⚠️ URL vazia detectada - bloqueando navegação");
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
            Debug.LogError($"[WebViewLauncher] ❌ Tentativa de abrir arquivo local bloqueada: {url}");
            
            // Bloquear a navegação
            webView.Stop();
            
            // Se for um arquivo .txt ou executável, logar como erro crítico
            if (url.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || 
                url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"[WebViewLauncher] 🚫 BLOQUEIO DE SEGURANÇA: Tentativa de executar arquivo local detectada: {url}");
            }
            
            return;
        }
        
        // Verificar se a URL é válida (deve começar com http://, https://, ou uniwebview://)
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("uniwebview://", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[WebViewLauncher] ⚠️ URL com scheme inválido detectada: {url}");
            
            // Bloquear navegação para URLs inválidas
            webView.Stop();
            return;
        }
        
        Debug.Log($"[WebViewLauncher] ✅ Navegação permitida para: {url}");
    }
    
    private void InitializeApiClient()
    {
        if (apiClient == null)
        {
            apiClient = FindObjectOfType<ApiClient>();
            if (apiClient == null)
            {
                Debug.LogWarning("[WebViewLauncher] ApiClient not found in scene. Some features may not work.");
            }
        }
    }
    
    private void ValidateGameArrays()
    {
        if (gameUrls == null || gameNames == null)
        {
            Debug.LogError("[WebViewLauncher] Game arrays cannot be null!");
            return;
        }
        
        if (gameUrls.Length != gameNames.Length)
        {
            Debug.LogError($"[WebViewLauncher] Mismatch between gameUrls ({gameUrls.Length}) and gameNames ({gameNames.Length}) arrays! They must have the same length.");
        }
        
        if (gameUrls.Length == 0)
        {
            Debug.LogWarning("[WebViewLauncher] No games configured in gameUrls array.");
        }
    }
    
    /// <summary>
    /// Define o ID do usuário atual
    /// </summary>
    public void SetUserId(int userId)
    {
        if (userId <= 0)
        {
            Debug.LogWarning("[WebViewLauncher] Invalid userId provided. Must be greater than 0.");
            return;
        }
        
        currentUserId = userId;
        Debug.Log($"[WebViewLauncher] User ID set to: {userId}");
    }
    
    /// <summary>
    /// Abre um jogo pelo índice
    /// </summary>
    public void OpenGame(int gameIndex)
    {
        if (!isInitialized)
        {
            Debug.LogError("[WebViewLauncher] WebView not initialized!");
            return;
        }
        
        if (gameUrls == null || gameUrls.Length == 0)
        {
            Debug.LogError("[WebViewLauncher] No games configured!");
            return;
        }
        
        if (gameIndex < 0 || gameIndex >= gameUrls.Length)
        {
            Debug.LogError($"[WebViewLauncher] Invalid game index: {gameIndex}. Valid range: 0-{gameUrls.Length - 1}");
            return;
        }
        
        if (gameNames == null || gameIndex >= gameNames.Length)
        {
            Debug.LogWarning($"[WebViewLauncher] Game name not found for index {gameIndex}. Using URL as name.");
            currentGameName = gameUrls[gameIndex];
        }
        else
        {
            currentGameName = gameNames[gameIndex];
        }
        
        currentGameUrl = gameUrls[gameIndex];
        
        if (string.IsNullOrEmpty(currentGameUrl))
        {
            Debug.LogError($"[WebViewLauncher] Game URL is empty for index {gameIndex}!");
            return;
        }
        
        sessionStartTime = DateTime.Now;
        isPlaying = true;
        
        Debug.Log($"[WebViewLauncher] Opening game: {currentGameName} ({currentGameUrl})");
        
        LoadUrl(currentGameUrl);
    }
    
    /// <summary>
    /// Abre um jogo diretamente pela URL
    /// </summary>
    public void OpenGameByUrl(string url, string gameName = "")
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("[WebViewLauncher] URL cannot be null or empty!");
            return;
        }
        
        currentGameUrl = url;
        currentGameName = string.IsNullOrEmpty(gameName) ? url : gameName;
        sessionStartTime = DateTime.Now;
        isPlaying = true;
        
        Debug.Log($"[WebViewLauncher] Opening game by URL: {currentGameName} ({currentGameUrl})");
        
        LoadUrl(currentGameUrl);
    }
    
    /// <summary>
    /// Abre a carteira do usuário
    /// </summary>
    public void OpenWallet(int userId)
    {
        if (userId <= 0)
        {
            Debug.LogWarning("[WebViewLauncher] Invalid userId for wallet.");
            return;
        }
        
        string url = $"{baseUrl}web/wallet.php?user_id={userId}";
        Debug.Log($"[WebViewLauncher] Opening wallet: {url}");
        LoadUrl(url);
    }
    
    /// <summary>
    /// Abre o ranking
    /// </summary>
    public void OpenRanking()
    {
        string url = $"{baseUrl}web/ranking.php";
        Debug.Log($"[WebViewLauncher] Opening ranking: {url}");
        LoadUrl(url);
    }
    
    /// <summary>
    /// Abre as tarefas do usuário
    /// </summary>
    public void OpenTasks(int userId)
    {
        if (userId <= 0)
        {
            Debug.LogWarning("[WebViewLauncher] Invalid userId for tasks.");
            return;
        }
        
        string url = $"{baseUrl}web/tasks.php?user_id={userId}";
        Debug.Log($"[WebViewLauncher] Opening tasks: {url}");
        LoadUrl(url);
    }
    
    /// <summary>
    /// Carrega uma URL no WebView
    /// </summary>
    private void LoadUrl(string url)
    {
        if (webView == null)
        {
            Debug.LogError("[WebViewLauncher] WebView is null!");
            return;
        }
        
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("[WebViewLauncher] URL is null or empty!");
            return;
        }
        
        try
        {
            webView.Load(url);
            if (showOnLoad)
            {
                webView.Show();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WebViewLauncher] Error loading URL: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Chamado quando o jogo terminar ou quando voltar do WebView
    /// </summary>
    public void OnGameSessionEnd(int score = 0, int playtimeSeconds = 0)
    {
        if (!isPlaying)
        {
            Debug.LogWarning("[WebViewLauncher] OnGameSessionEnd called but no game session is active.");
            return;
        }
        
        isPlaying = false;
        
        // Calcular tempo jogado
        if (playtimeSeconds == 0)
        {
            playtimeSeconds = (int)(DateTime.Now - sessionStartTime).TotalSeconds;
        }
        
        // Garantir que o tempo seja válido
        if (playtimeSeconds < 0)
        {
            playtimeSeconds = 0;
        }
        
        // Calcular pontos baseado no tempo (ex: 0.01 por segundo)
        float pointsEarned = playtimeSeconds * 0.01f;
        
        Debug.Log($"[WebViewLauncher] Game session ended. Playtime: {playtimeSeconds}s, Points: {pointsEarned:F2}");
        
        // Enviar para backend
        StartCoroutine(TrackPlaytimeRoutine(currentGameName, currentGameUrl, playtimeSeconds, pointsEarned));
    }
    
    /// <summary>
    /// Força o encerramento da sessão atual
    /// </summary>
    public void ForceEndSession()
    {
        if (isPlaying)
        {
            OnGameSessionEnd();
        }
    }
    
    private IEnumerator TrackPlaytimeRoutine(string gameName, string gameUrl, int playtimeSeconds, float pointsEarned)
    {
        InitializeApiClient();
        
        if (apiClient == null)
        {
            Debug.LogWarning("[WebViewLauncher] Cannot track playtime: ApiClient not available.");
            yield break;
        }
        
        if (currentUserId <= 0)
        {
            Debug.LogWarning("[WebViewLauncher] Cannot track playtime: Invalid user ID.");
            yield break;
        }
        
        // Criar objeto serializável para JSON
        PlaytimeData data = new PlaytimeData
        {
            user_id = currentUserId,
            game_name = gameName ?? "",
            game_url = gameUrl ?? "",
            playtime_seconds = playtimeSeconds,
            points_earned = pointsEarned
        };
        
        string payload = JsonUtility.ToJson(data);
        
        yield return StartCoroutine(apiClient.PostJson("track_playtime.php", payload, 
            (response) => {
                Debug.Log($"[WebViewLauncher] Playtime tracked successfully: {response}");
            }, 
            (error) => {
                Debug.LogError($"[WebViewLauncher] Playtime tracking failed: {error}");
            }));
    }
    
    // Callbacks do UniWebView
    private void OnPageFinished(UniWebView webView, int statusCode, string url)
    {
        Debug.Log($"[WebViewLauncher] Page finished loading: {url} (Status: {statusCode})");
    }
    
    private void OnPageError(UniWebView webView, int errorCode, string errorMessage)
    {
        Debug.LogError($"[WebViewLauncher] Page error: {errorMessage} (Code: {errorCode})");
    }
    
    private bool OnShouldClose(UniWebView webView)
    {
        // Se estiver jogando, encerrar a sessão antes de fechar
        if (isPlaying)
        {
            OnGameSessionEnd();
        }
        
        return true; // Permite fechar
    }
    
    private void OnDestroy()
    {
        // Garantir que a sessão seja encerrada ao destruir o objeto
        if (isPlaying)
        {
            OnGameSessionEnd();
        }
        
        // Limpar callbacks
        if (webView != null)
        {
            webView.OnPageFinished -= OnPageFinished;
            webView.OnPageErrorReceived -= OnPageError;
            webView.OnShouldClose -= OnShouldClose;
            webView.OnPageStarted -= OnPageStarted;
        }
    }
    
    /// <summary>
    /// Classe auxiliar para serialização JSON
    /// </summary>
    [Serializable]
    private class PlaytimeData
    {
        public int user_id;
        public string game_name;
        public string game_url;
        public int playtime_seconds;
        public float points_earned;
    }
}