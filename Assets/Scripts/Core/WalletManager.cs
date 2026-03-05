using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class WalletManager : MonoBehaviour
{
    [Header("UI References")]
    public Text balanceText;
    public Text totalEarnedText;
    public Transform transactionsContainer;
    public GameObject transactionItemPrefab;
    public Button refreshButton;
    public Button withdrawButton;
    public InputField pixKeyInput;
    public InputField amountInput;
    
    private ApiClient apiClient;
    private int currentUserId;
    
    void Start()
    {
        apiClient = FindObjectOfType<ApiClient>();
        if(refreshButton) refreshButton.onClick.AddListener(() => LoadWallet(currentUserId));
        if(withdrawButton) withdrawButton.onClick.AddListener(() => RequestWithdrawal());
    }
    
    public void LoadWallet(int userId)
    {
        currentUserId = userId;
        if(apiClient == null) apiClient = FindObjectOfType<ApiClient>();
        
        StartCoroutine(LoadBalanceRoutine());
        StartCoroutine(LoadTransactionsRoutine());
    }
    
    IEnumerator LoadBalanceRoutine()
    {
        string url = $"get_balance.php?user_id={currentUserId}";
        yield return StartCoroutine(apiClient.GetJson(url, (response) => {
            var data = JsonUtility.FromJson<BalanceResponse>(response);
            if(data != null && data.success)
            {
                if(balanceText) balanceText.text = $"R$ {data.balance:F2}";
            }
        }, (error) => {
            Debug.LogError("Balance load failed: " + error);
        }));
    }
    
    IEnumerator LoadTransactionsRoutine()
    {
        string url = $"get_transactions.php?user_id={currentUserId}&limit=50";
        yield return StartCoroutine(apiClient.GetJson(url, (response) => {
            var data = JsonUtility.FromJson<TransactionsResponse>(response);
            if(data != null && data.success)
            {
                DisplayTransactions(data.transactions);
                
                // Calcular total ganho
                float totalEarned = 0f;
                foreach(var txn in data.transactions)
                {
                    if(txn.transaction_type == "credit")
                        totalEarned += txn.amount;
                }
                if(totalEarnedText) totalEarnedText.text = $"Total Ganho: R$ {totalEarned:F2}";
            }
        }, (error) => {
            Debug.LogError("Transactions load failed: " + error);
        }));
    }
    
    void DisplayTransactions(TransactionData[] transactions)
    {
        // Limpar container
        foreach(Transform child in transactionsContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Criar itens de transação
        foreach(var txn in transactions)
        {
            GameObject item = Instantiate(transactionItemPrefab, transactionsContainer);
            TransactionItemUI ui = item.GetComponent<TransactionItemUI>();
            if(ui) ui.SetupTransaction(txn);
        }
    }
    
    public void RequestWithdrawal()
    {
        string pixKey = pixKeyInput ? pixKeyInput.text : "";
        float amount = 0f;
        
        if(float.TryParse(amountInput ? amountInput.text : "0", out amount) && amount > 0 && !string.IsNullOrEmpty(pixKey))
        {
            StartCoroutine(RequestWithdrawalRoutine(pixKey, amount));
        }
        else
        {
            Debug.LogError("Invalid withdrawal data");
        }
    }
    
    IEnumerator RequestWithdrawalRoutine(string pixKey, float amount)
    {
        var payload = JsonUtility.ToJson(new { 
            user_id = currentUserId, 
            pix_key = pixKey, 
            amount = amount 
        });
        
        yield return StartCoroutine(apiClient.PostJson("request_withdrawal.php", payload, (response) => {
            var data = JsonUtility.FromJson<WithdrawalResponse>(response);
            if(data != null && data.success)
            {
                Debug.Log("Withdrawal requested successfully!");
                LoadWallet(currentUserId);
                if(pixKeyInput) pixKeyInput.text = "";
                if(amountInput) amountInput.text = "";
            }
        }, (error) => {
            Debug.LogError("Withdrawal request failed: " + error);
        }));
    }
    
    [System.Serializable]
    public class BalanceResponse
    {
        public bool success;
        public float balance;
    }
    
    [System.Serializable]
    public class TransactionsResponse
    {
        public bool success;
        public TransactionData[] transactions;
    }
    
    [System.Serializable]
    public class TransactionData
    {
        public int id;
        public float amount;
        public string transaction_type;
        public string reason;
        public string created_at;
    }
    
    [System.Serializable]
    public class WithdrawalResponse
    {
        public bool success;
        public bool requested;
    }
}

