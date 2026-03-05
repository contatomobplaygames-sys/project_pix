using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// Gerencia funcionalidades do jogo como envio de scores e atualização de saldo
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("References")]
    public ApiClient api;
    public TasksManager tasksManager;
    public MenuManager menuManager;
    
    [Header("Player Data")]
    [Tooltip("ID do jogador (definido após login/registro)")]
    public int playerId = 0;
    
    [Tooltip("Se verdadeiro, o jogador é um convidado")]
    public bool isGuest = false;
    
    private void Start()
    {
        // Validar referências
        if (api == null)
        {
            api = FindObjectOfType<ApiClient>();
            if (api == null)
            {
                Debug.LogWarning("[GameManager] ApiClient not assigned and not found in scene.");
            }
        }
        
        // Carregar dados do usuário do PlayerPrefs
        LoadUserData();
        
        // Se não tem playerId, aguardar GuestInitializer criar guest
        if (playerId == 0)
        {
            StartCoroutine(WaitForGuestInitialization());
        }
    }
    
    /// <summary>
    /// Aguarda GuestInitializer criar guest e atualiza playerId
    /// </summary>
    private IEnumerator WaitForGuestInitialization()
    {
        int maxWaitTime = 10; // 10 segundos máximo
        float elapsed = 0f;
        
        while (playerId == 0 && elapsed < maxWaitTime)
        {
            var guestInitializer = GuestInitializer.Instance;
            if (guestInitializer != null && guestInitializer.IsInitialized())
            {
                int guestId = guestInitializer.GetGuestId();
                if (guestId > 0)
                {
                    SetPlayerId(guestId, isGuest: true);
                    Debug.Log($"[GameManager] ✅ PlayerId atualizado do GuestInitializer: {guestId}");
                    yield break;
                }
            }
            
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }
        
        if (playerId == 0)
        {
            Debug.LogWarning("[GameManager] ⚠️ Guest não foi inicializado após 10 segundos");
        }
    }
    
    /// <summary>
    /// Carrega dados do usuário do PlayerPrefs
    /// </summary>
    private void LoadUserData()
    {
        isGuest = PlayerPrefs.GetString("is_guest", "false") == "true";
        
        if (isGuest)
        {
            playerId = PlayerPrefs.GetInt("guest_id", 0);
            if (playerId == 0)
            {
                playerId = PlayerPrefs.GetInt("user_id", 0);
            }
        }
        else
        {
            playerId = PlayerPrefs.GetInt("user_id", 0);
        }
        
        if (playerId > 0)
        {
            Debug.Log($"[GameManager] Dados carregados: Player ID={playerId}, Is Guest={isGuest}");
        }
    }
    
    /// <summary>
    /// Define o ID do jogador (chamado após login ou recebimento de dados do WebView)
    /// </summary>
    public void SetPlayerId(int userId, bool isGuest = false)
    {
        this.playerId = userId;
        this.isGuest = isGuest;
        
        PlayerPrefs.SetInt(isGuest ? "guest_id" : "user_id", userId);
        PlayerPrefs.SetString("is_guest", isGuest ? "true" : "false");
        PlayerPrefs.Save();
        
        Debug.Log($"[GameManager] Player ID atualizado: {userId} (Guest: {isGuest})");
    }
    
    /// <summary>
    /// Envia o score do jogador para o servidor
    /// </summary>
    public void SubmitScore(int score)
    {
        if (playerId <= 0)
        {
            Debug.LogWarning("[GameManager] Cannot submit score: Invalid player ID.");
            return;
        }
        
        if (api == null)
        {
            Debug.LogError("[GameManager] Cannot submit score: ApiClient not assigned.");
            return;
        }
        
        if (score < 0)
        {
            Debug.LogWarning("[GameManager] Invalid score value. Score must be >= 0.");
            return;
        }
        
        StartCoroutine(SubmitScoreRoutine(score));
    }

    private IEnumerator SubmitScoreRoutine(int score)
    {
        ScoreData data = new ScoreData
        {
            user_id = playerId,
            score = score
        };
        
        string payload = JsonUtility.ToJson(data);
        
        yield return StartCoroutine(api.PostJson("update_score.php", payload, 
            (resp) => {
                Debug.Log($"[GameManager] Score submitted successfully: {resp}");
                
                // Atualizar progresso de tarefas relacionadas a score
                if (tasksManager != null)
                {
                    tasksManager.UpdateTaskProgress(0, 1); // task_id 0 significa qualquer tarefa de score
                }
            }, 
            (err) => {
                Debug.LogError($"[GameManager] Score submission failed: {err}");
            }));
    }

    /// <summary>
    /// Chamado quando um anúncio recompensado é completado
    /// Envia 10 pontos para o usuário logado
    /// </summary>
    public void OnRewardedAdCompleted(string adProvider = "admob")
    {
        OnRewardedAdCompleted(10, adProvider); // Sempre 10 pontos para rewarded video
    }
    
    /// <summary>
    /// Chamado quando um anúncio recompensado é completado com valor customizado
    /// </summary>
    public void OnRewardedAdCompleted(int points, string adProvider = "admob")
    {
        if (points <= 0)
        {
            Debug.LogWarning("[GameManager] Invalid reward amount. Must be > 0.");
            return;
        }
        
        // [REMOVIDO] Sistema de envio de pontos removido
        Debug.Log($"[GameManager] Vídeo rewarded completado. Pontos: {points} (provider: {adProvider})");
        
        // Atualizar menu se disponível
        if (menuManager != null)
        {
            menuManager.RefreshBalance();
        }
        
        // Atualizar progresso de tarefas
        if (tasksManager != null)
        {
            tasksManager.UpdateTaskProgress(0, 1);
        }
    }

    // [REMOVIDO] - Sistema de envio de pontos ao servidor foi completamente removido
    // Os pontos agora são apenas armazenados localmente
    
    /// <summary>
    /// Chamado quando uma tarefa for completada manualmente (ex: assistir ad)
    /// </summary>
    public void OnTaskProgress(string taskCategory, int progressDelta = 1)
    {
        if (tasksManager == null)
        {
            Debug.LogWarning("[GameManager] TasksManager not assigned. Cannot update task progress.");
            return;
        }
        
        if (string.IsNullOrEmpty(taskCategory))
        {
            Debug.LogWarning("[GameManager] Task category cannot be null or empty.");
            return;
        }
        
        if (progressDelta <= 0)
        {
            Debug.LogWarning("[GameManager] Progress delta must be greater than 0.");
            return;
        }
        
        Debug.Log($"[GameManager] Task progress: {taskCategory} +{progressDelta}");
        // tasksManager vai buscar e atualizar as tarefas da categoria
    }
    
    /// <summary>
    /// Classe auxiliar para serialização JSON de score
    /// </summary>
    [Serializable]
    private class ScoreData
    {
        public int user_id;
        public int score;
    }
    
    // [REMOVIDO] - Sistema de envio de pontos ao servidor foi completamente removido
}