using UnityEngine;
using UnityEngine.UI;

public class TaskItemUI : MonoBehaviour
{
    [Header("UI References")]
    public Text titleText;
    public Text descriptionText;
    public Text rewardText;
    public Text progressText;
    public Button claimButton;
    public Image progressBar;
    
    private TasksManager.TaskData taskData;
    private TasksManager tasksManager;
    
    public void SetupTask(TasksManager.TaskData task, TasksManager manager)
    {
        taskData = task;
        tasksManager = manager;
        
        if(titleText) titleText.text = task.title;
        if(descriptionText) descriptionText.text = task.description;
        if(rewardText) rewardText.text = $"R$ {task.reward:F2}";
        
        UpdateProgress();
        
        if(claimButton)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(() => OnClaimClicked());
            claimButton.interactable = !task.completed && task.progress >= task.required_value;
        }
    }
    
    void UpdateProgress()
    {
        float progress = taskData.completed ? 1f : (float)taskData.progress / taskData.required_value;
        
        if(progressText) 
            progressText.text = $"{taskData.progress}/{taskData.required_value}";
        
        if(progressBar) 
            progressBar.fillAmount = progress;
    }
    
    void OnClaimClicked()
    {
        if(taskData.completed) return;
        
        // Auto-update progress based on task category
        // Para tarefas de "jogar", o progresso é atualizado quando o jogo termina
        // Para outras, pode ser manual ou automático
        tasksManager.UpdateTaskProgress(taskData.id, 1);
    }
}

