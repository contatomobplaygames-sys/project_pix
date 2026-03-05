using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class TasksManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform tasksContainer;
    public GameObject taskItemPrefab;
    public Button refreshButton;
    
    private ApiClient apiClient;
    private int currentUserId;
    private List<TaskData> currentTasks = new List<TaskData>();
    
    void Start()
    {
        apiClient = FindObjectOfType<ApiClient>();
        if(refreshButton) refreshButton.onClick.AddListener(() => LoadTasks(currentUserId));
    }
    
    public void LoadTasks(int userId)
    {
        currentUserId = userId;
        if(apiClient == null) apiClient = FindObjectOfType<ApiClient>();
        
        StartCoroutine(LoadTasksRoutine());
    }
    
    IEnumerator LoadTasksRoutine()
    {
        string url = $"tasks.php?user_id={currentUserId}";
        yield return StartCoroutine(apiClient.GetJson(url, (response) => {
            var data = JsonUtility.FromJson<TasksResponse>(response);
            if(data != null && data.success)
            {
                currentTasks = new List<TaskData>(data.tasks);
                DisplayTasks();
            }
        }, (error) => {
            Debug.LogError("Tasks load failed: " + error);
        }));
    }
    
    void DisplayTasks()
    {
        // Limpar container
        foreach(Transform child in tasksContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Criar itens de tarefa
        foreach(var task in currentTasks)
        {
            GameObject item = Instantiate(taskItemPrefab, tasksContainer);
            TaskItemUI ui = item.GetComponent<TaskItemUI>();
            if(ui) ui.SetupTask(task, this);
        }
    }
    
    public void UpdateTaskProgress(int taskId, int progressDelta = 1)
    {
        StartCoroutine(UpdateTaskProgressRoutine(taskId, progressDelta));
    }
    
    IEnumerator UpdateTaskProgressRoutine(int taskId, int progressDelta)
    {
        var payload = JsonUtility.ToJson(new { 
            user_id = currentUserId, 
            task_id = taskId, 
            progress_delta = progressDelta 
        });
        
        yield return StartCoroutine(apiClient.PostJson("tasks.php", payload, (response) => {
            var data = JsonUtility.FromJson<TaskUpdateResponse>(response);
            if(data != null && data.success)
            {
                if(data.completed)
                {
                    Debug.Log($"Task completed! Reward: R$ {data.reward}");
                    // Atualizar lista
                    LoadTasks(currentUserId);
                }
                else
                {
                    Debug.Log($"Progress: {data.progress}/{data.required}");
                }
            }
        }, (error) => {
            Debug.LogError("Task update failed: " + error);
        }));
    }
    
    [System.Serializable]
    public class TasksResponse
    {
        public bool success;
        public TaskData[] tasks;
    }
    
    [System.Serializable]
    public class TaskData
    {
        public int id;
        public string task_type;
        public string title;
        public string description;
        public float reward;
        public int required_value;
        public string task_category;
        public int progress;
        public bool completed;
    }
    
    [System.Serializable]
    public class TaskUpdateResponse
    {
        public bool success;
        public bool completed;
        public float reward;
        public int progress;
        public int required;
        public int new_progress;
    }
}

