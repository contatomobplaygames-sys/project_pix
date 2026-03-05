using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject gamesPanel;
    public GameObject tasksPanel;
    public GameObject rankingPanel;
    public GameObject walletPanel;
    public GameObject profilePanel;
    
    [Header("UI References")]
    public Text balanceText;
    public Text playerNameText;
    
    [Header("Managers")]
    public ApiClient apiClient;
    public GameManager gameManager;
    public TasksManager tasksManager;
    public WalletManager walletManager;
    public ProfileManager profileManager;
    public WebViewLauncher webViewLauncher;
    
    private int currentUserId;
    private float currentBalance;
    
    void Start()
    {
        // Inicializar com menu principal
        ShowMainMenu();
        
        // Só atualizar balance se houver userId válido
        if (currentUserId > 0)
        {
            RefreshBalance();
        }
    }
    
    public void SetUserData(int userId, string displayName)
    {
        if (userId <= 0)
        {
            Debug.LogWarning("[MenuManager] Invalid userId provided. Must be greater than 0.");
            return;
        }
        
        currentUserId = userId;
        
        if (playerNameText != null)
        {
            playerNameText.text = displayName ?? "";
        }
        
        if (gameManager != null)
        {
            gameManager.playerId = userId;
        }
        
        // Carregar dados do perfil
        if (profileManager != null)
        {
            profileManager.LoadProfile(userId);
        }
        
        // Atualizar balance após definir userId
        RefreshBalance();
    }
    
    public void ShowMainMenu()
    {
        HideAllPanels();
        if(mainMenuPanel) mainMenuPanel.SetActive(true);
    }
    
    public void ShowGames()
    {
        HideAllPanels();
        if(gamesPanel) gamesPanel.SetActive(true);
    }
    
    public void ShowTasks()
    {
        HideAllPanels();
        if (tasksPanel != null) tasksPanel.SetActive(true);
        
        if (currentUserId <= 0)
        {
            Debug.LogWarning("[MenuManager] Cannot show tasks: Invalid user ID.");
            return;
        }
        
        if (tasksManager != null)
        {
            tasksManager.LoadTasks(currentUserId);
        }
        else if (webViewLauncher != null)
        {
            // Fallback: abrir tasks no WebView se TasksManager não estiver disponível
            webViewLauncher.OpenTasks(currentUserId);
        }
        else
        {
            Debug.LogWarning("[MenuManager] Neither TasksManager nor WebViewLauncher available.");
        }
    }
    
    public void ShowRanking()
    {
        HideAllPanels();
        if (rankingPanel != null) rankingPanel.SetActive(true);
        
        // Abrir ranking no WebView
        if (webViewLauncher != null)
        {
            webViewLauncher.OpenRanking();
        }
        else
        {
            Debug.LogWarning("[MenuManager] WebViewLauncher not assigned. Cannot open ranking.");
        }
    }
    
    public void ShowWallet()
    {
        HideAllPanels();
        if (walletPanel != null) walletPanel.SetActive(true);
        
        if (currentUserId <= 0)
        {
            Debug.LogWarning("[MenuManager] Cannot show wallet: Invalid user ID.");
            return;
        }
        
        if (walletManager != null)
        {
            walletManager.LoadWallet(currentUserId);
        }
        else if (webViewLauncher != null)
        {
            // Fallback: abrir wallet no WebView se WalletManager não estiver disponível
            webViewLauncher.OpenWallet(currentUserId);
        }
        else
        {
            Debug.LogWarning("[MenuManager] Neither WalletManager nor WebViewLauncher available.");
        }
    }
    
    public void ShowProfile()
    {
        HideAllPanels();
        if(profilePanel) profilePanel.SetActive(true);
        if(profileManager) profileManager.LoadProfile(currentUserId);
    }
    
    private void HideAllPanels()
    {
        if(mainMenuPanel) mainMenuPanel.SetActive(false);
        if(gamesPanel) gamesPanel.SetActive(false);
        if(tasksPanel) tasksPanel.SetActive(false);
        if(rankingPanel) rankingPanel.SetActive(false);
        if(walletPanel) walletPanel.SetActive(false);
        if(profilePanel) profilePanel.SetActive(false);
    }
    
    public void RefreshBalance()
    {
        if (currentUserId <= 0)
        {
            Debug.LogWarning("[MenuManager] Cannot refresh balance: Invalid user ID.");
            return;
        }
        
        if (apiClient == null)
        {
            Debug.LogWarning("[MenuManager] Cannot refresh balance: ApiClient not assigned.");
            return;
        }
        
        StartCoroutine(RefreshBalanceRoutine());
    }
    
    IEnumerator RefreshBalanceRoutine()
    {
        string url = $"get_balance.php?user_id={currentUserId}";
        
        yield return StartCoroutine(apiClient.GetJson(url, 
            (response) => {
                try
                {
                    if (string.IsNullOrEmpty(response))
                    {
                        Debug.LogWarning("[MenuManager] Empty response from balance API.");
                        return;
                    }
                    
                    var data = JsonUtility.FromJson<BalanceResponse>(response);
                    if (data != null && data.success)
                    {
                        currentBalance = data.balance;
                        if (balanceText != null)
                        {
                            balanceText.text = $"R$ {currentBalance:F2}";
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[MenuManager] Balance API returned unsuccessful response.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[MenuManager] Error parsing balance response: {ex.Message}");
                }
            }, 
            (error) => {
                Debug.LogError($"[MenuManager] Balance refresh failed: {error}");
            }));
    }
    
    [System.Serializable]
    public class BalanceResponse
    {
        public bool success;
        public float balance;
    }
}

