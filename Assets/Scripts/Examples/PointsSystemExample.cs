using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// Exemplo prático de implementação do sistema de pontos
/// 
/// INSTRUÇÕES:
/// 1. Adicione este script a um GameObject na cena
/// 2. Configure as referências no Inspector (ApiClient, etc)
/// 3. Chame SendPoints() quando quiser enviar pontos
/// 
/// Este é um exemplo completo e funcional que pode ser usado como base.
/// </summary>
public class PointsSystemExample : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Cliente API para comunicação com o servidor")]
    public ApiClient apiClient;
    
    [Tooltip("ID do jogador (obtido após login)")]
    public int playerId = 0;
    
    [Tooltip("Ativar logs detalhados para debug")]
    public bool enableDebugLogs = true;
    
    [Header("Configurações de Pontos")]
    [Tooltip("Pontos padrão para rewarded video")]
    public int defaultRewardedPoints = 10;
    
    [Tooltip("Tempo mínimo entre requisições (segundos)")]
    public float cooldownTime = 20f;
    
    // Estado interno
    private float lastPointsSentTime = 0f;
    private bool isSendingPoints = false;
    
    #region Unity Lifecycle
    
    private void Start()
    {
        // Buscar ApiClient se não foi atribuído
        if (apiClient == null)
        {
            apiClient = FindObjectOfType<ApiClient>();
            if (apiClient == null)
            {
                LogError("ApiClient não encontrado na cena!");
            }
        }
        
        // Carregar playerId do PlayerPrefs
        if (playerId == 0)
        {
            playerId = PlayerPrefs.GetInt("user_id", 0);
            if (playerId > 0)
            {
                LogInfo($"Player ID carregado: {playerId}");
            }
        }
    }
    
    #endregion
    
    #region Métodos Públicos
    
    /// <summary>
    /// Envia pontos após anúncio recompensado
    /// </summary>
    public void SendRewardedVideoPoints(string adProvider = "admob")
    {
        SendPoints(defaultRewardedPoints, "rewarded_video", $"{adProvider}_unity");
    }
    
    /// <summary>
    /// Envia pontos customizados
    /// </summary>
    public void SendPoints(int points, string type = "rewarded_video", string source = "admob_unity", string description = null)
    {
        if (!CanSendPoints())
        {
            return;
        }
        
        StartCoroutine(SendPointsRoutine(points, type, source, description));
    }
    
    /// <summary>
    /// Verifica se pode enviar pontos agora
    /// </summary>
    public bool CanSendPoints()
    {
        // Verificar autenticação
        if (playerId <= 0)
        {
            LogWarning("Usuário não autenticado. Não é possível enviar pontos.");
            return false;
        }
        
        // Verificar ApiClient
        if (apiClient == null)
        {
            LogError("ApiClient não configurado!");
            return false;
        }
        
        // Verificar cooldown
        if (!IsCooldownReady())
        {
            float remainingTime = cooldownTime - (Time.time - lastPointsSentTime);
            LogWarning($"Aguarde {remainingTime:F1} segundos antes de enviar mais pontos.");
            return false;
        }
        
        // Verificar se já está enviando
        if (isSendingPoints)
        {
            LogWarning("Já existe uma requisição de pontos em andamento.");
            return false;
        }
        
        // Verificar conexão (opcional)
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            LogWarning("Sem conexão com internet.");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Atualiza o playerId (chamado após login)
    /// </summary>
    public void SetPlayerId(int userId)
    {
        playerId = userId;
        PlayerPrefs.SetInt("user_id", userId);
        PlayerPrefs.Save();
        LogInfo($"Player ID atualizado: {playerId}");
    }
    
    #endregion
    
    #region Métodos Privados
    
    /// <summary>
    /// Rotina principal de envio de pontos
    /// </summary>
    private IEnumerator SendPointsRoutine(int points, string type, string source, string description)
    {
        isSendingPoints = true;
        lastPointsSentTime = Time.time;
        
        // Validar pontos
        if (points < 1 || points > 100)
        {
            LogError($"Quantidade de pontos inválida: {points}. Deve ser entre 1 e 100.");
            isSendingPoints = false;
            yield break;
        }
        
        // Obter email do usuário
        string userEmail = PlayerPrefs.GetString("user_email", "");
        
        // Preparar dados
        PointsRequestData requestData = new PointsRequestData
        {
            user_id = playerId,
            email = userEmail,
            points = points,
            type = type,
            source = source,
            description = description ?? GetDefaultDescription(type)
        };
        
        string payload = JsonUtility.ToJson(requestData);
        
        LogInfo($"Enviando {points} pontos (tipo: {type}, fonte: {source})");
        
        // Enviar requisição
        yield return StartCoroutine(apiClient.PostJson(
            "server/php/unified_submit_score.php",
            payload,
            (response) => OnPointsSentSuccess(response, points),
            (error) => OnPointsSentError(error, points)
        ));
        
        isSendingPoints = false;
    }
    
    /// <summary>
    /// Callback de sucesso
    /// </summary>
    private void OnPointsSentSuccess(string response, int pointsSent)
    {
        LogInfo($"✅ Resposta recebida: {response}");
        
        try
        {
            // Tentar parsear resposta
            PointsResponse responseData = JsonUtility.FromJson<PointsResponse>(response);
            
            if (responseData != null && responseData.status == "success")
            {
                int newTotal = responseData.new_total;
                int pointsAdded = responseData.points_added;
                
                LogInfo($"✅ {pointsAdded} pontos adicionados! Novo total: {newTotal}");
                
                // Atualizar pontos locais
                UpdateLocalPoints(newTotal);
                
                // Atualizar UI
                OnPointsUpdated(pointsAdded, newTotal);
                
                // Feedback visual
                ShowPointsGainedFeedback(pointsAdded, newTotal);
            }
            else
            {
                LogWarning($"Resposta não indica sucesso: {response}");
            }
        }
        catch (Exception ex)
        {
            LogError($"Erro ao parsear resposta: {ex.Message}");
            LogError($"Resposta bruta: {response}");
        }
    }
    
    /// <summary>
    /// Callback de erro
    /// </summary>
    private void OnPointsSentError(string error, int pointsSent)
    {
        LogError($"❌ Erro ao enviar {pointsSent} pontos: {error}");
        
        // Armazenar pontos localmente para envio posterior
        SavePendingPoints(pointsSent);
        
        // Notificar usuário
        OnPointsError(error);
        
        // Tentar novamente após delay (opcional)
        // StartCoroutine(RetrySendPointsAfterDelay(pointsSent, 5f));
    }
    
    /// <summary>
    /// Verifica se o cooldown está pronto
    /// </summary>
    private bool IsCooldownReady()
    {
        float timeSinceLastRequest = Time.time - lastPointsSentTime;
        return timeSinceLastRequest >= cooldownTime;
    }
    
    /// <summary>
    /// Obtém descrição padrão baseada no tipo
    /// </summary>
    private string GetDefaultDescription(string type)
    {
        switch (type)
        {
            case "rewarded_video":
                return "Recompensa por assistir vídeo";
            case "reward":
                return "Recompensa recebida";
            case "bonus":
                return "Bônus especial";
            case "game":
                return "Pontos do jogo";
            default:
                return "Pontos ganhos";
        }
    }
    
    /// <summary>
    /// Atualiza pontos locais
    /// </summary>
    private void UpdateLocalPoints(int newTotal)
    {
        PlayerPrefs.SetInt("user_points", newTotal);
        PlayerPrefs.SetString("points_last_update", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Salva pontos pendentes para envio posterior
    /// </summary>
    private void SavePendingPoints(int points)
    {
        int pendingPoints = PlayerPrefs.GetInt("pending_points", 0);
        PlayerPrefs.SetInt("pending_points", pendingPoints + points);
        PlayerPrefs.Save();
        LogInfo($"Pontos pendentes salvos: {pendingPoints + points}");
    }
    
    #endregion
    
    #region Eventos (Override para customização)
    
    /// <summary>
    /// Chamado quando os pontos são atualizados com sucesso
    /// Override este método para atualizar sua UI
    /// </summary>
    protected virtual void OnPointsUpdated(int pointsAdded, int newTotal)
    {
        // Exemplo: Atualizar texto de pontos
        // pointsText.text = newTotal.ToString();
    }
    
    /// <summary>
    /// Chamado quando há erro ao enviar pontos
    /// Override este método para mostrar mensagem ao usuário
    /// </summary>
    protected virtual void OnPointsError(string errorMessage)
    {
        // Exemplo: Mostrar popup de erro
        // errorPopup.Show("Erro ao enviar pontos. Tente novamente.");
    }
    
    /// <summary>
    /// Mostra feedback visual de pontos ganhos
    /// Override este método para customizar animação
    /// </summary>
    protected virtual void ShowPointsGainedFeedback(int pointsAdded, int newTotal)
    {
        // Exemplo: Animação de pontos flutuando
        // pointsAnimation.Show($"+{pointsAdded}", newTotal);
    }
    
    #endregion
    
    #region Logging
    
    private void LogInfo(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PointsSystem] {message}");
        }
    }
    
    private void LogWarning(string message)
    {
        Debug.LogWarning($"[PointsSystem] ⚠️ {message}");
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[PointsSystem] ❌ {message}");
    }
    
    #endregion
    
    #region Classes de Dados
    
    /// <summary>
    /// Dados da requisição de pontos
    /// </summary>
    [Serializable]
    private class PointsRequestData
    {
        public int user_id;
        public string email;
        public int points;
        public string type;
        public string source;
        public string description;
    }
    
    /// <summary>
    /// Resposta do servidor
    /// </summary>
    [Serializable]
    private class PointsResponse
    {
        public string status;
        public string message;
        public int points_added;
        public int new_total;
        public int total_points;
        public int current_rewarded;
        public int transaction_id;
        public int user_id;
        public string user_email;
        public string user_name;
        public bool is_guest;
    }
    
    #endregion
}

