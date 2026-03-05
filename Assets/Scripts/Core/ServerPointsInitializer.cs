using UnityEngine;

/// <summary>
/// Inicializador que garante que ServerPointsSender existe na cena com configuração correta
/// IMPORTANTE: Este script deve estar na primeira cena do jogo
/// </summary>
public class ServerPointsInitializer : MonoBehaviour
{
    [Header("Configuração do Servidor")]
    [Tooltip("URL base do servidor")]
    [SerializeField] private string serverBaseUrl = "https://app.mobplaygames.com.br/unity/k26pix/";
    
    [Tooltip("Endpoint para envio de pontos")]
    [SerializeField] private string submitEndpoint = "php/unified_submit_score.php";
    
    [Header("Configurações")]
    [Tooltip("Timeout da requisição em segundos")]
    [SerializeField] private int requestTimeout = 30;
    
    [Tooltip("Habilitar logs detalhados")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("Inicialização")]
    [Tooltip("Criar ServerPointsSender automaticamente se não existir")]
    [SerializeField] private bool autoCreateIfMissing = true;

    private void Awake()
    {
        InitializeServerPointsSender();
    }

    private void InitializeServerPointsSender()
    {
        // Verificar se ServerPointsSender já existe
        ServerPointsSender existingSender = FindObjectOfType<ServerPointsSender>();
        
        if (existingSender != null)
        {
            if (enableDebugLogs)
                Debug.Log("[ServerPointsInitializer] ✅ ServerPointsSender já existe na cena");
            
            // Aplicar configurações via reflection (já que os campos são privados)
            ApplyConfigurationToSender(existingSender);
            return;
        }

        if (!autoCreateIfMissing)
        {
            Debug.LogWarning("[ServerPointsInitializer] ⚠️ ServerPointsSender não encontrado e autoCreateIfMissing = false");
            return;
        }

        // Criar novo ServerPointsSender
        CreateServerPointsSender();
    }

    private void CreateServerPointsSender()
    {
        if (enableDebugLogs)
            Debug.Log("[ServerPointsInitializer] 🔧 Criando ServerPointsSender...");

        // Criar GameObject para ServerPointsSender
        GameObject senderObj = new GameObject("ServerPointsSender");
        ServerPointsSender sender = senderObj.AddComponent<ServerPointsSender>();
        DontDestroyOnLoad(senderObj);

        // Aplicar configurações
        ApplyConfigurationToSender(sender);

        if (enableDebugLogs)
            Debug.Log($"[ServerPointsInitializer] ✅ ServerPointsSender criado e configurado");
    }

    private void ApplyConfigurationToSender(ServerPointsSender sender)
    {
        // Usar reflection para definir os campos privados
        var senderType = typeof(ServerPointsSender);
        
        // Definir serverBaseUrl
        var baseUrlField = senderType.GetField("serverBaseUrl", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (baseUrlField != null)
        {
            baseUrlField.SetValue(sender, serverBaseUrl);
            if (enableDebugLogs)
                Debug.Log($"[ServerPointsInitializer] 📝 serverBaseUrl = {serverBaseUrl}");
        }

        // Definir submitEndpoint
        var endpointField = senderType.GetField("submitEndpoint", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (endpointField != null)
        {
            endpointField.SetValue(sender, submitEndpoint);
            if (enableDebugLogs)
                Debug.Log($"[ServerPointsInitializer] 📝 submitEndpoint = {submitEndpoint}");
        }

        // Definir requestTimeout
        var timeoutField = senderType.GetField("requestTimeout", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (timeoutField != null)
        {
            timeoutField.SetValue(sender, requestTimeout);
            if (enableDebugLogs)
                Debug.Log($"[ServerPointsInitializer] 📝 requestTimeout = {requestTimeout}s");
        }

        // Definir enableDebugLogs
        var debugField = senderType.GetField("enableDebugLogs", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (debugField != null)
        {
            debugField.SetValue(sender, enableDebugLogs);
            if (enableDebugLogs)
                Debug.Log($"[ServerPointsInitializer] 📝 enableDebugLogs = {enableDebugLogs}");
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[ServerPointsInitializer] ✅ Configuração aplicada:");
            Debug.Log($"   URL: {serverBaseUrl}{submitEndpoint}");
        }
    }

    [ContextMenu("Debug: Verificar Configuração")]
    private void DebugCheckConfiguration()
    {
        Debug.Log("🔍 [DEBUG] Verificando configuração do ServerPointsSender:");
        Debug.Log($"   - serverBaseUrl: {serverBaseUrl}");
        Debug.Log($"   - submitEndpoint: {submitEndpoint}");
        Debug.Log($"   - URL completa: {serverBaseUrl}{submitEndpoint}");
        Debug.Log($"   - requestTimeout: {requestTimeout}s");
        Debug.Log($"   - enableDebugLogs: {enableDebugLogs}");
        Debug.Log($"   - autoCreateIfMissing: {autoCreateIfMissing}");
        
        ServerPointsSender sender = FindObjectOfType<ServerPointsSender>();
        Debug.Log($"   - ServerPointsSender existe: {sender != null}");
    }

    [ContextMenu("Force: Criar ServerPointsSender Agora")]
    private void ForceCreateServerPointsSender()
    {
        Debug.Log("🔧 [FORCE] Criando ServerPointsSender manualmente...");
        CreateServerPointsSender();
    }
}

