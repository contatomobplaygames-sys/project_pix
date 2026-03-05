using UnityEngine;
using UnityEngine.UI;

public class TransactionItemUI : MonoBehaviour
{
    [Header("UI References")]
    public Text typeText;
    public Text amountText;
    public Text reasonText;
    public Text dateText;
    
    public void SetupTransaction(WalletManager.TransactionData transaction)
    {
        if(typeText) 
            typeText.text = transaction.transaction_type == "credit" ? "+" : "-";
        
        if(amountText)
        {
            amountText.text = $"R$ {transaction.amount:F2}";
            amountText.color = transaction.transaction_type == "credit" ? Color.green : Color.red;
        }
        
        if(reasonText) 
            reasonText.text = FormatReason(transaction.reason);
        
        if(dateText) 
            dateText.text = FormatDate(transaction.created_at);
    }
    
    string FormatReason(string reason)
    {
        switch(reason)
        {
            case "ad_reward": return "Anúncio";
            case "task_completion": return "Tarefa";
            case "game_reward": return "Jogo";
            case "withdrawal": return "Saque";
            default: return reason;
        }
    }
    
    string FormatDate(string dateStr)
    {
        // Formatação simples da data
        if(string.IsNullOrEmpty(dateStr)) return "";
        return dateStr.Substring(0, Mathf.Min(10, dateStr.Length)); // YYYY-MM-DD
    }
}

