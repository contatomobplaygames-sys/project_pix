using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ProfileManager : MonoBehaviour
{
    [Header("UI References")]
    public Text displayNameText;
    public Text emailText;
    public Text balanceText;
    public Text totalEarnedText;
    public Text playtimeText;
    public Text completedTasksText;
    public Text bestScoreText;
    public Text rankText;
    public InputField nameInput;
    public Button saveNameButton;
    
    private ApiClient apiClient;
    private int currentUserId;
    
    void Start()
    {
        apiClient = FindObjectOfType<ApiClient>();
        if(saveNameButton) saveNameButton.onClick.AddListener(() => SaveDisplayName());
    }
    
    public void LoadProfile(int userId)
    {
        currentUserId = userId;
        if(apiClient == null) apiClient = FindObjectOfType<ApiClient>();
        
        StartCoroutine(LoadProfileRoutine());
    }
    
    IEnumerator LoadProfileRoutine()
    {
        string url = $"get_user_profile.php?user_id={currentUserId}";
        yield return StartCoroutine(apiClient.GetJson(url, (response) => {
            var data = JsonUtility.FromJson<ProfileResponse>(response);
            if(data != null && data.success)
            {
                DisplayProfile(data.user, data.stats);
            }
        }, (error) => {
            Debug.LogError("Profile load failed: " + error);
        }));
    }
    
    void DisplayProfile(UserData user, StatsData stats)
    {
        if(displayNameText) displayNameText.text = user.display_name;
        if(emailText) emailText.text = user.email;
        if(balanceText) balanceText.text = $"R$ {user.balance:F2}";
        
        if(totalEarnedText) totalEarnedText.text = $"R$ {stats.total_earned:F2}";
        
        int hours = stats.total_playtime_seconds / 3600;
        int minutes = (stats.total_playtime_seconds % 3600) / 60;
        if(playtimeText) playtimeText.text = $"{hours}h {minutes}m";
        
        if(completedTasksText) completedTasksText.text = stats.completed_tasks.ToString();
        if(bestScoreText) bestScoreText.text = stats.best_score.ToString();
        if(rankText) rankText.text = $"#{stats.leaderboard_rank}";
        
        if(nameInput) nameInput.text = user.display_name;
    }
    
    void SaveDisplayName()
    {
        string newName = nameInput ? nameInput.text : "";
        if(string.IsNullOrEmpty(newName)) return;
        
        // TODO: Criar endpoint update_profile.php se necessário
        Debug.Log($"Save display name: {newName}");
    }
    
    [System.Serializable]
    public class ProfileResponse
    {
        public bool success;
        public UserData user;
        public StatsData stats;
    }
    
    [System.Serializable]
    public class UserData
    {
        public int id;
        public string email;
        public string display_name;
        public float balance;
        public string referral_code;
        public string created_at;
        public string guest_public_id;
    }
    
    [System.Serializable]
    public class StatsData
    {
        public float total_earned;
        public int total_playtime_seconds;
        public int completed_tasks;
        public int best_score;
        public int leaderboard_rank;
    }
}

