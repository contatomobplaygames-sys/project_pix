using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Ads;

/// <summary>
/// Handler responsável por processar mensagens de anúncios do WebView (adsExibition.html)
/// Gerencia comunicação bidirecional entre HTML e Unity para exibição de anúncios
/// </summary>
[RequireComponent(typeof(UniWebView))]
public class AdsWebViewHandler : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Se verdadeiro, exibe logs detalhados")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Tooltip("Pontos concedidos por vídeo recompensado assistido")]
    [SerializeField] private int rewardedPointsPerVideo = 1;
    
    [Tooltip("Pontos enviados ao servidor por vídeo recompensado (padrão: 10)")]
    [SerializeField] private int serverPointsPerVideo = 10;
    
    [Header("Anti-Spam Interstitial")]
    [Tooltip("Intervalo mínimo (segundos) entre interstitials vindos do WebView.")]
    [SerializeField] private float minInterstitialCooldownSeconds = 30f;
    
    private UniWebView webView;
    private AuthManager authManager;
    private ApiClient apiClient;
    private bool isInitialized = false;
    
    // Callbacks de anúncios ativos
    private Action<AdsResult> currentRewardedCallback;
    private string currentAdNetwork = "";
    private string currentAdType = "";
    
    // Timer para carregar próximo rewarded ad após 60 segundos
    private Coroutine nextRewardedLoadCoroutine;
    private const float REWARDED_LOAD_DELAY = 60f; // 60 segundos
    
    // Cooldown de interstitial (anti-spam)
    private float lastInterstitialShownAt = -9999f;
    
    #region Initialization
    
    private void Awake()
    {
        webView = GetComponent<UniWebView>();
        if (webView == null)
        {
            Debug.LogError("[AdsWebViewHandler] UniWebView component not found!");
            return;
        }
        
        InitializeHandler();
    }
    
    private void Start()
    {
        // Garantir que AuthManager está disponível
        authManager = AuthManager.Instance;
        
        // Buscar ApiClient para envio de pontos ao servidor
        if (apiClient == null)
        {
            apiClient = FindObjectOfType<ApiClient>();
            if (apiClient == null && enableDebugLogs)
            {
                Debug.LogWarning("[AdsWebViewHandler] ⚠️ ApiClient não encontrado. Envio de pontos ao servidor pode falhar.");
            }
        }
        
        if (enableDebugLogs)
            Debug.Log("[AdsWebViewHandler] ✅ Handler inicializado e pronto para receber mensagens de anúncios");
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
            Debug.Log("[AdsWebViewHandler] 🔧 Handler configurado para receber mensagens");
    }
    
    /// <summary>
    /// Intercepta navegações e valida URLs para evitar abertura de arquivos locais como executáveis
    /// </summary>
    private void OnPageStarted(UniWebView webView, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            if (enableDebugLogs)
                Debug.LogWarning("[AdsWebViewHandler] ⚠️ URL vazia detectada - bloqueando navegação");
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
                Debug.LogError($"[AdsWebViewHandler] ❌ Tentativa de abrir arquivo local bloqueada: {url}");
            
            // Bloquear a navegação
            webView.Stop();
            
            // Se for um arquivo .txt ou executável, logar como erro crítico
            if (url.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || 
                url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"[AdsWebViewHandler] 🚫 BLOQUEIO DE SEGURANÇA: Tentativa de executar arquivo local detectada: {url}");
            }
            
            return;
        }
        
        // Verificar se a URL é válida (deve começar com http://, https://, ou uniwebview://)
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("uniwebview://", StringComparison.OrdinalIgnoreCase))
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[AdsWebViewHandler] ⚠️ URL com scheme inválido detectada: {url}");
            
            // Bloquear navegação para URLs inválidas
            webView.Stop();
            return;
        }
        
        if (enableDebugLogs)
            Debug.Log($"[AdsWebViewHandler] ✅ Navegação permitida para: {url}");
    }
    
    #endregion
    
    #region WebView Message Handling
    
    /// <summary>
    /// Chamado quando uma mensagem é recebida do WebView
    /// </summary>
    private void OnWebViewMessage(UniWebView webView, UniWebViewMessage message)
    {
        if (enableDebugLogs)
            Debug.Log($"[AdsWebViewHandler] 📨 Mensagem recebida: {message.Path}");
        
        // Processar diferentes tipos de mensagens
        switch (message.Path.ToLower())
        {
            case "showad":
                HandleShowAd(message.Args);
                break;
            
            case "getcurrentpoints":
                HandleGetCurrentPoints();
                break;
            
            case "updatepoints":
                HandleUpdatePoints(message.Args);
                break;
            
            case "loadnextrewarded":
                HandleLoadNextRewarded();
                break;
            
            default:
                if (enableDebugLogs)
                    Debug.Log($"[AdsWebViewHandler] ⚠️ Mensagem não reconhecida: {message.Path}");
                break;
        }
    }
    
    #endregion
    
    #region Ad Handlers
    
    /// <summary>
    /// Processa solicitação de exibição de anúncio
    /// Espera parâmetros: type (interstitial/rewarded), network (admob/max)
    /// </summary>
    private void HandleShowAd(Dictionary<string, string> args)
    {
        if (enableDebugLogs)
            Debug.Log("[AdsWebViewHandler] 🎬 Processando solicitação de anúncio...");
        
        try
        {
            // Extrair parâmetros
            string adType = args.ContainsKey("type") ? args["type"].ToLower() : "interstitial";
            string network = args.ContainsKey("network") ? args["network"].ToLower() : "auto";
            
            string resolvedNetwork = ResolveNetwork(network);
            
            // Armazenar informações do anúncio atual
            currentAdNetwork = resolvedNetwork;
            currentAdType = adType;
            
            // Verificar se AdsAPI está inicializado
            if (!IsAdsAPIReady())
            {
                Debug.LogError("[AdsWebViewHandler] ❌ AdsAPI não está inicializado!");
                NotifyAdEvent("adFailed", adType);
                return;
            }
            
            if (enableDebugLogs)
                Debug.Log($"[AdsWebViewHandler] 📱 Exibindo {adType} via {resolvedNetwork.ToUpper()}");
            
            // Exibir anúncio baseado no tipo e rede
            switch (adType)
            {
                case "banner":
                    ShowBannerAd(resolvedNetwork);
                    break;
                case "interstitial":
                    ShowInterstitialAd(resolvedNetwork);
                    break;
                case "rewarded":
                    ShowRewardedAd(resolvedNetwork);
                    break;
                default:
                    Debug.LogWarning($"[AdsWebViewHandler] ⚠️ Tipo de anúncio desconhecido: {adType}, tentando como interstitial");
                    ShowInterstitialAd(resolvedNetwork);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao processar showAd: {ex.Message}");
            NotifyAdEvent("adFailed", "Unknown");
        }
    }

    /// <summary>
    /// Resolve a rede pedida pelo front-end para a rede ativa no AdsSettings.
    /// Aceita: auto, max, admob. Em caso inválido, usa a rede ativa.
    /// </summary>
    private string ResolveNetwork(string requestedNetwork)
    {
        if (string.IsNullOrEmpty(requestedNetwork) || requestedNetwork == "auto")
        {
            return GetCurrentConfiguredNetwork();
        }

        if (requestedNetwork == "admob" || requestedNetwork == "max")
        {
            return requestedNetwork;
        }

        if (enableDebugLogs)
            Debug.LogWarning($"[AdsWebViewHandler] ⚠️ Rede '{requestedNetwork}' inválida. Usando rede ativa do AdsSettings.");

        return GetCurrentConfiguredNetwork();
    }

    /// <summary>
    /// Obtém a rede ativa no AdsSettings e converte para admob/max.
    /// </summary>
    private string GetCurrentConfiguredNetwork()
    {
        try
        {
            string primaryKey = AdsSettings.GetPrimaryAdsKey();
            if (string.IsNullOrEmpty(primaryKey))
            {
                return "admob";
            }

            string normalized = primaryKey.Trim().ToLower();
            if (normalized.Contains("applovin") || normalized.Contains("max"))
            {
                return "max";
            }

            if (normalized.Contains("admob") || normalized.Contains("google"))
            {
                return "admob";
            }

            // Tentar descobrir pelo tipo do AdsObject configurado.
            var adsObj = AdsSettings.GetAdsObject(primaryKey);
            if (adsObj != null)
            {
                string typeName = adsObj.GetType().Name.ToLower();
                if (typeName.Contains("max"))
                    return "max";
                if (typeName.Contains("admob"))
                    return "admob";
            }
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[AdsWebViewHandler] ⚠️ Erro ao resolver rede ativa: {ex.Message}");
        }

        return "admob";
    }
    
    /// <summary>
    /// Exibe banner ad (fire-and-forget).
    /// </summary>
    private void ShowBannerAd(string network)
    {
        try
        {
            if (enableDebugLogs)
                Debug.Log($"[AdsWebViewHandler] 🪧 Exibindo Banner ({network})");
            
            AdsAPI.ShowBanner();
            NotifyAdEvent("adShown", "banner");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AdsWebViewHandler] ⚠️ Erro ao exibir banner: {ex.Message}");
            NotifyAdEvent("adFailed", "banner");
        }
    }

    /// <summary>
    /// Exibe anúncio interstitial com cooldown anti-spam.
    /// </summary>
    private void ShowInterstitialAd(string network)
    {
        // --- Cooldown anti-spam ---
        float elapsed = Time.realtimeSinceStartup - lastInterstitialShownAt;
        if (elapsed < minInterstitialCooldownSeconds)
        {
            float remaining = minInterstitialCooldownSeconds - elapsed;
            if (enableDebugLogs)
                Debug.Log($"[AdsWebViewHandler] ⏳ Interstitial bloqueado por cooldown ({remaining:F1}s restantes).");
            return;
        }

        try
        {
            if (enableDebugLogs)
                Debug.Log($"[AdsWebViewHandler] 🖼️ Exibindo Interstitial ({network})");
            
            // Notificar HTML — enviar tipo puro para matching correto no React
            NotifyAdEvent("adShown", "interstitial");
            
            // Exibir anúncio via AdsAPI
            AdsAPI.ShowInterstitial();
            
            // Marcar timestamp para cooldown
            lastInterstitialShownAt = Time.realtimeSinceStartup;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao exibir interstitial: {ex.Message}");
            NotifyAdEvent("adFailed", "interstitial");
        }
    }
    
    /// <summary>
    /// Exibe anúncio rewarded
    /// </summary>
    private void ShowRewardedAd(string network)
    {
        try
        {
            if (enableDebugLogs)
                Debug.Log($"[AdsWebViewHandler] 🎁 Exibindo Rewarded ({network})");
            
            // Configurar callback para quando o vídeo for assistido
            currentRewardedCallback = (result) =>
            {
                HandleRewardedAdResult(result, network);
            };
            
            // Exibir anúncio via AdsAPI
            AdsAPI.ShowRewardedVideo(currentRewardedCallback);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao exibir rewarded: {ex.Message}");
            NotifyAdEvent("adFailed", "rewarded");
            currentRewardedCallback = null;
        }
    }
    
    /// <summary>
    /// Processa resultado do anúncio rewarded
    /// </summary>
    private void HandleRewardedAdResult(AdsResult result, string network)
    {
        currentRewardedCallback = null;
        
        switch (result.adsStatus)
        {
            case AdsStatus.Success:
                if (enableDebugLogs)
                    Debug.Log($"[AdsWebViewHandler] ✅ Rewarded ad completado com sucesso ({network})");
                
                // Conceder pontos localmente
                int pointsToAdd = rewardedPointsPerVideo;
                AddPointsToUser(pointsToAdd);
                
                Debug.Log($"[AdsWebViewHandler] 🎬 Vídeo rewarded finalizado! Pontos adicionados localmente: {pointsToAdd}");
                
                // OTIMIZAÇÃO: Notificar React IMEDIATAMENTE com total estimado (otimista)
                // A UI atualiza instantaneamente sem esperar resposta do servidor (~6s)
                int estimatedTotal = GetCurrentPoints();
                NotifyPointsOptimisticToReact(pointsToAdd, estimatedTotal);
                
                if (enableDebugLogs)
                    Debug.Log($"[AdsWebViewHandler] ⚡ Atualização otimista enviada: +{pointsToAdd}, total estimado: {estimatedTotal}");
                
                // Enviar 2 pontos ao servidor em segundo plano
                if (enableDebugLogs)
                    Debug.Log($"[AdsWebViewHandler] 📤 Iniciando envio de 2 pontos ao servidor...");
                
                // Verificar se ServerPointsSender está disponível
                if (ServerPointsSender.Instance == null)
                {
                    Debug.LogError("[AdsWebViewHandler] ❌ ServerPointsSender.Instance é null! Não é possível enviar pontos.");
                    NotifyPointsSentToReact(pointsToAdd, GetCurrentPoints());
                    break;
                }
                
                ServerPointsSender.Instance.SendRewardedVideoPoints(2, network, (success, newTotal) =>
                {
                    if (success)
                    {
                        Debug.Log($"[AdsWebViewHandler] ✅ 2 pontos enviados ao servidor! Novo total no servidor: {newTotal}");
                        
                        // Atualizar pontos locais com o total do servidor (valor autoritativo)
                        UpdateUserPoints(newTotal);
                        
                        // Notificar React com total confirmado do servidor
                        NotifyPointsSentToReact(2, newTotal);
                    }
                    else
                    {
                        Debug.LogWarning("[AdsWebViewHandler] ⚠️ Falha ao enviar pontos ao servidor, mas pontos locais foram adicionados");
                        Debug.LogWarning("[AdsWebViewHandler] ⚠️ Verifique os logs do ServerPointsSender para mais detalhes");
                        
                        // Notificar React mesmo em caso de falha (com pontos locais)
                        int currentTotal = GetCurrentPoints();
                        NotifyPointsSentToReact(pointsToAdd, currentTotal);
                    }
                });
                
                // Notificar HTML — usar tipo puro "rewarded" para matching correto no React
                NotifyAdEvent("adRewarded", "rewarded");
                
                // Iniciar cooldown no HTML após vídeo rewarded terminar
                StartRewardedCooldown();
                
                // Iniciar timer de 60 segundos para carregar próximo rewarded ad
                StartNextRewardedAdLoadTimer();
                break;
            
            case AdsStatus.Failed:
                if (enableDebugLogs)
                    Debug.LogWarning($"[AdsWebViewHandler] ⚠️ Rewarded ad falhou ({network})");
                NotifyAdEvent("adFailed", "rewarded");
                break;
            
            case AdsStatus.Canceled:
                if (enableDebugLogs)
                    Debug.LogWarning($"[AdsWebViewHandler] ⚠️ Rewarded ad cancelado ({network})");
                NotifyAdEvent("adCanceled", "rewarded");
                break;
        }
    }
    
    /// <summary>
    /// Verifica se AdsAPI está pronto
    /// </summary>
    private bool IsAdsAPIReady()
    {
        // Verificar se AdsAPI foi inicializado
        // Como não temos acesso direto ao estado interno, vamos tentar usar
        // e deixar o AdsAPI lidar com erros
        return true; // AdsAPI vai retornar erro se não estiver inicializado
    }
    
    /// <summary>
    /// Processa solicitação para carregar próximo rewarded ad
    /// </summary>
    private void HandleLoadNextRewarded()
    {
        if (enableDebugLogs)
            Debug.Log("[AdsWebViewHandler] 🔄 Carregando próximo rewarded ad...");
        
        LoadNextRewardedAd();
    }
    
    /// <summary>
    /// Inicia um timer de 60 segundos para carregar o próximo rewarded ad
    /// </summary>
    private void StartNextRewardedAdLoadTimer()
    {
        // Cancelar timer anterior se existir
        if (nextRewardedLoadCoroutine != null)
        {
            StopCoroutine(nextRewardedLoadCoroutine);
            if (enableDebugLogs)
                Debug.Log("[AdsWebViewHandler] ⏹️ Timer anterior cancelado");
        }
        
        // Iniciar novo timer
        nextRewardedLoadCoroutine = StartCoroutine(LoadNextRewardedAdAfterDelay());
        
        if (enableDebugLogs)
            Debug.Log($"[AdsWebViewHandler] ⏰ Timer de {REWARDED_LOAD_DELAY} segundos iniciado para carregar próximo rewarded ad");
    }
    
    /// <summary>
    /// Corrotina que espera 60 segundos antes de carregar o próximo rewarded ad
    /// </summary>
    private IEnumerator LoadNextRewardedAdAfterDelay()
    {
        yield return new WaitForSeconds(REWARDED_LOAD_DELAY);
        
        if (enableDebugLogs)
            Debug.Log("[AdsWebViewHandler] ⏰ Timer de 60 segundos concluído - Carregando próximo rewarded ad");
        
        LoadNextRewardedAd();
        nextRewardedLoadCoroutine = null;
    }
    
    /// <summary>
    /// Carrega o próximo rewarded ad para deixá-lo pronto
    /// </summary>
    private void LoadNextRewardedAd()
    {
        try
        {
            AdsAPI.LoadNextRewardedAd();
            
            if (enableDebugLogs)
                Debug.Log("[AdsWebViewHandler] ✅ Próximo rewarded ad sendo carregado");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao carregar próximo rewarded ad: {ex.Message}");
        }
    }
    
    #endregion
    
    #region Points Handlers
    
    /// <summary>
    /// Processa solicitação de pontos atuais
    /// </summary>
    private void HandleGetCurrentPoints()
    {
        if (enableDebugLogs)
            Debug.Log("[AdsWebViewHandler] 💰 Solicitando pontos atuais...");
        
        try
        {
            int currentPoints = GetCurrentPoints();
            
            if (enableDebugLogs)
                Debug.Log($"[AdsWebViewHandler] 💰 Pontos atuais: {currentPoints}");
            
            // Enviar pontos para o HTML
            SendPointsToWebView(currentPoints);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao obter pontos: {ex.Message}");
            SendPointsToWebView(0);
        }
    }
    
    /// <summary>
    /// Processa atualização de pontos
    /// Espera parâmetro: points (número de pontos)
    /// </summary>
    private void HandleUpdatePoints(Dictionary<string, string> args)
    {
        if (enableDebugLogs)
            Debug.Log("[AdsWebViewHandler] 🔄 Atualizando pontos...");
        
        try
        {
            if (!args.ContainsKey("points"))
            {
                Debug.LogWarning("[AdsWebViewHandler] ⚠️ Parâmetro 'points' não encontrado!");
                return;
            }
            
            if (!int.TryParse(args["points"], out int points))
            {
                Debug.LogError($"[AdsWebViewHandler] ❌ Valor de pontos inválido: {args["points"]}");
                return;
            }
            
            // Atualizar pontos no sistema
            UpdateUserPoints(points);
            
            if (enableDebugLogs)
                Debug.Log($"[AdsWebViewHandler] ✅ Pontos atualizados para: {points}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao atualizar pontos: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Obtém pontos atuais do usuário
    /// </summary>
    private int GetCurrentPoints()
    {
        // Tentar obter do AuthManager primeiro
        if (authManager != null && authManager.IsAuthenticated())
        {
            var session = authManager.GetCurrentSession();
            if (session != null)
            {
                return session.points;
            }
        }
        
        // Fallback: PlayerPrefs
        return PlayerPrefs.GetInt("user_points", 0);
    }
    
    /// <summary>
    /// Adiciona pontos ao usuário
    /// </summary>
    private void AddPointsToUser(int pointsToAdd)
    {
        if (pointsToAdd <= 0)
            return;
        
        int currentPoints = GetCurrentPoints();
        int newPoints = currentPoints + pointsToAdd;
        
        UpdateUserPoints(newPoints);
        
        if (enableDebugLogs)
            Debug.Log($"[AdsWebViewHandler] ✅ {pointsToAdd} pontos adicionados. Novo total: {newPoints}");
    }
    
    /// <summary>
    /// Atualiza pontos do usuário
    /// </summary>
    private void UpdateUserPoints(int newPoints)
    {
        // Atualizar no AuthManager se disponível
        if (authManager != null && authManager.IsAuthenticated())
        {
            authManager.UpdateSession(session =>
            {
                session.points = newPoints;
            });
        }
        
        // Atualizar PlayerPrefs como fallback
        PlayerPrefs.SetInt("user_points", newPoints);
        PlayerPrefs.Save();
    }
    
    #endregion
    
    #region WebView Communication
    
    /// <summary>
    /// Envia pontos para o WebView
    /// </summary>
    private void SendPointsToWebView(int points)
    {
        if (webView == null)
            return;
        
        try
        {
            string script = $"if(typeof window.updatePoints === 'function') {{ window.updatePoints({points}); }}";
            webView.EvaluateJavaScript(script, (payload) =>
            {
                if (enableDebugLogs)
                    Debug.Log($"[AdsWebViewHandler] 📤 Pontos enviados para WebView: {points}");
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao enviar pontos para WebView: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Notifica evento de anúncio para o WebView
    /// </summary>
    private void NotifyAdEvent(string eventType, string adType)
    {
        if (webView == null)
            return;
        
        try
        {
            // Escapar aspas no adType
            string escapedAdType = adType.Replace("'", "\\'");
            string script = $"if(typeof window.onAdEvent === 'function') {{ window.onAdEvent('{eventType}', '{escapedAdType}'); }}";
            
            webView.EvaluateJavaScript(script, (payload) =>
            {
                if (enableDebugLogs)
                    Debug.Log($"[AdsWebViewHandler] 📤 Evento de anúncio enviado: {eventType} - {adType}");
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao notificar evento de anúncio: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Notifica React frontend quando pontos são enviados com sucesso
    /// </summary>
    private void NotifyPointsSentToReact(int pointsAdded, int newTotal)
    {
        if (webView == null)
        {
            Debug.LogWarning("[AdsWebViewHandler] ⚠️ webView é null - não é possível notificar React");
            return;
        }
        
        try
        {
            // Escapar valores para evitar problemas com JavaScript
            string safePoints = pointsAdded.ToString();
            string safeTotal = newTotal.ToString();
            
            // Chamar função global que React escuta com tratamento de erro
            string script = $@"
                if(typeof window.onPointsSentSuccessfully === 'function') {{
                    try {{
                        window.onPointsSentSuccessfully({safePoints}, {safeTotal});
                        console.log('[Unity] ✅ Pontos notificados: {safePoints}, Total: {safeTotal}');
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
                {
                    Debug.Log($"[AdsWebViewHandler] 📤 Notificação de pontos enviada para React: {pointsAdded} pontos, novo total: {newTotal}");
                    if (payload != null && !string.IsNullOrEmpty(payload.data))
                    {
                        Debug.Log($"[AdsWebViewHandler] 📥 Resposta do React: {payload.data}");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao notificar React sobre pontos: {ex.Message}");
            if (enableDebugLogs)
            {
                Debug.LogException(ex);
            }
        }
    }
    
    /// <summary>
    /// Notifica React com atualização otimista de pontos (imediata, sem aguardar servidor)
    /// Atualiza apenas os pontos na UI sem disparar recarga completa de perfil
    /// </summary>
    private void NotifyPointsOptimisticToReact(int pointsAdded, int estimatedTotal)
    {
        if (webView == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[AdsWebViewHandler] ⚠️ webView é null - não é possível enviar atualização otimista");
            return;
        }
        
        try
        {
            string safePoints = pointsAdded.ToString();
            string safeTotal = estimatedTotal.ToString();
            
            // Chamar função global leve que APENAS atualiza os pontos na UI
            // Diferente de onPointsSentSuccessfully, NÃO dispara loadGuestProfile()
            string script = $@"
                if(typeof window.onPointsOptimisticUpdate === 'function') {{
                    try {{
                        window.onPointsOptimisticUpdate({safePoints}, {safeTotal});
                        console.log('[Unity] ⚡ Atualização otimista: +{safePoints}, Total estimado: {safeTotal}');
                    }} catch(e) {{
                        console.error('[Unity] ❌ Erro em onPointsOptimisticUpdate:', e);
                    }}
                }} else {{
                    console.warn('[Unity] ⚠️ window.onPointsOptimisticUpdate não está definido');
                }}
            ";
            
            webView.EvaluateJavaScript(script, (payload) =>
            {
                if (enableDebugLogs)
                    Debug.Log($"[AdsWebViewHandler] ⚡ Atualização otimista enviada: +{pointsAdded} pontos, total estimado: {estimatedTotal}");
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao enviar atualização otimista: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Inicia o cooldown do botão rewarded após vídeo terminar
    /// </summary>
    private void StartRewardedCooldown()
    {
        if (webView == null)
            return;
        
        try
        {
            string script = "if(typeof window.startRewardedCooldown === 'function') { window.startRewardedCooldown(); }";
            
            webView.EvaluateJavaScript(script, (payload) =>
            {
                if (enableDebugLogs)
                    Debug.Log("[AdsWebViewHandler] ⏰ Cooldown iniciado no HTML após rewarded video");
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdsWebViewHandler] ❌ Erro ao iniciar cooldown: {ex.Message}");
        }
    }
    
    #endregion
    
    #region Server Points Submission
    
    // [REMOVIDO] - Sistema de envio de pontos ao servidor foi completamente removido
    // Os pontos agora são apenas armazenados localmente (PlayerPrefs e AuthManager)
    
    #endregion
    
    #region Lifecycle
    
    private void OnDestroy()
    {
        // Cancelar timer de carregamento se estiver ativo
        if (nextRewardedLoadCoroutine != null)
        {
            StopCoroutine(nextRewardedLoadCoroutine);
            nextRewardedLoadCoroutine = null;
        }
        
        // Limpar listeners
        if (webView != null)
        {
            webView.OnMessageReceived -= OnWebViewMessage;
        }
        
        // Limpar callbacks pendentes
        currentRewardedCallback = null;
    }
    
    #endregion
    
    #region Debug Methods
    
    [ContextMenu("Debug: Simular Solicitação de Pontos")]
    private void DebugSimulateGetPoints()
    {
        HandleGetCurrentPoints();
    }
    
    [ContextMenu("Debug: Simular Show Ad (Interstitial MAX)")]
    private void DebugSimulateShowAd()
    {
        var testArgs = new Dictionary<string, string>
        {
            { "type", "interstitial" },
            { "network", "max" }
        };
        HandleShowAd(testArgs);
    }
    
    [ContextMenu("Debug: Simular Show Ad (Rewarded AdMob)")]
    private void DebugSimulateShowRewarded()
    {
        var testArgs = new Dictionary<string, string>
        {
            { "type", "rewarded" },
            { "network", "admob" }
        };
        HandleShowAd(testArgs);
    }
    
    #endregion
}

