using System;
using UnityEngine;

/// <summary>
/// Classe para armazenar dados da sessão do usuário
/// Pode ser serializada para JSON e armazenada em PlayerPrefs
/// </summary>
[Serializable]
public class UserSession
{
    [Header("User Data")]
    public int userId;
    public string username;
    public string email;
    public string displayName;
    public string userType; // "regular" ou "guest"
    public string guestPublicId; // ID público do guest (ex: GUEST-XXXX-XXXX)
    
    [Header("Account Info")]
    public float balance;
    public int points;
    public string referralCode;
    public int level;
    
    [Header("Session Info")]
    public string sessionToken;
    public string createdAt;
    public string lastLogin;
    public bool isAuthenticated;
    
    [Header("Statistics")]
    public int totalGamesPlayed;
    public int totalAdsWatched;
    public float totalEarned;
    public int consecutiveDays;
    
    /// <summary>
    /// Cria uma nova sessão vazia
    /// </summary>
    public UserSession()
    {
        userId = 0;
        username = "";
        email = "";
        displayName = "";
        userType = "guest";
        guestPublicId = "";
        balance = 0f;
        points = 0;
        referralCode = "";
        level = 1;
        sessionToken = "";
        createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        lastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        isAuthenticated = false;
        totalGamesPlayed = 0;
        totalAdsWatched = 0;
        totalEarned = 0f;
        consecutiveDays = 0;
    }
    
    /// <summary>
    /// Cria uma sessão a partir de dados de login
    /// </summary>
    public UserSession(int userId, string username, string email, string token)
    {
        this.userId = userId;
        this.username = username;
        this.email = email;
        this.displayName = username;
        this.sessionToken = token;
        this.userType = "regular";
        this.balance = 0f;
        this.points = 0;
        this.referralCode = "";
        this.level = 1;
        this.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        this.lastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        this.isAuthenticated = true;
        this.totalGamesPlayed = 0;
        this.totalAdsWatched = 0;
        this.totalEarned = 0f;
        this.consecutiveDays = 0;
    }
    
    /// <summary>
    /// Verifica se a sessão é válida
    /// </summary>
    public bool IsValid()
    {
        return userId > 0 && isAuthenticated && !string.IsNullOrEmpty(sessionToken);
    }
    
    /// <summary>
    /// Limpa todos os dados da sessão
    /// </summary>
    public void Clear()
    {
        userId = 0;
        username = "";
        email = "";
        displayName = "";
        userType = "guest";
        guestPublicId = "";
        balance = 0f;
        points = 0;
        referralCode = "";
        level = 1;
        sessionToken = "";
        isAuthenticated = false;
        totalGamesPlayed = 0;
        totalAdsWatched = 0;
        totalEarned = 0f;
        consecutiveDays = 0;
    }
    
    /// <summary>
    /// Converte a sessão para JSON
    /// </summary>
    public string ToJson()
    {
        return JsonUtility.ToJson(this, true);
    }
    
    /// <summary>
    /// Cria uma sessão a partir de JSON
    /// </summary>
    public static UserSession FromJson(string json)
    {
        try
        {
            return JsonUtility.FromJson<UserSession>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserSession] Erro ao parsear JSON: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Atualiza o último login para agora
    /// </summary>
    public void UpdateLastLogin()
    {
        lastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    /// <summary>
    /// Atualiza o saldo
    /// </summary>
    public void UpdateBalance(float newBalance)
    {
        balance = newBalance;
    }
    
    /// <summary>
    /// Adiciona pontos
    /// </summary>
    public void AddPoints(int amount)
    {
        points += amount;
    }
    
    /// <summary>
    /// Cria uma cópia da sessão
    /// </summary>
    public UserSession Clone()
    {
        return FromJson(ToJson());
    }
    
    public override string ToString()
    {
        return $"UserSession[{userId}] {username} ({email}) - {userType} - Balance: R${balance:F2} - Points: {points}";
    }
}

