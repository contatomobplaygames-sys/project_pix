using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System;

/// <summary>
/// Cliente HTTP para comunicação com a API do backend
/// </summary>
public class ApiClient : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("URL base da API")]
    public string baseUrl = "https://app.mobplaygames.com.br/unity/k26pix/";
    
    [Header("Settings")]
    [Tooltip("Timeout em segundos para requisições")]
    public int timeoutSeconds = 30;
    
    [Tooltip("Se verdadeiro, exibe logs detalhados das requisições")]
    public bool enableDebugLogs = false;

    /// <summary>
    /// Envia uma requisição POST com JSON
    /// </summary>
    public IEnumerator PostJson(string path, string json, Action<string> onSuccess, Action<string> onError)
    {
        // Validações
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[ApiClient] Path cannot be null or empty.");
            onError?.Invoke("Invalid path");
            yield break;
        }
        
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[ApiClient] JSON payload is null or empty.");
        }
        
        if (string.IsNullOrEmpty(baseUrl))
        {
            Debug.LogError("[ApiClient] Base URL is not configured!");
            onError?.Invoke("Base URL not configured");
            yield break;
        }
        
        // Construir URL completa
        string url = baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        
        if (enableDebugLogs)
        {
            Debug.Log($"[ApiClient] POST {url}");
            Debug.Log($"[ApiClient] Payload: {json}");
        }
        
        UnityWebRequest www = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json ?? "{}");
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.timeout = timeoutSeconds;

        yield return www.SendWebRequest();
        
        try
        {
            if (www.result == UnityWebRequest.Result.ConnectionError || 
                www.result == UnityWebRequest.Result.ProtocolError)
            {
                string errorMsg = $"{www.error}";
                if (www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
                {
                    errorMsg += $" - {www.downloadHandler.text}";
                }
                
                if (enableDebugLogs)
                {
                    Debug.LogError($"[ApiClient] POST Error: {errorMsg}");
                }
                
                onError?.Invoke(errorMsg);
            }
            else
            {
                string response = www.downloadHandler?.text ?? "";
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[ApiClient] POST Success: {response}");
                }
                
                onSuccess?.Invoke(response);
            }
        }
        catch (Exception ex)
        {
            string errorMsg = $"Exception processing POST response: {ex.Message}";
            Debug.LogError($"[ApiClient] {errorMsg}");
            onError?.Invoke(errorMsg);
        }
        finally
        {
            www.Dispose();
        }
    }
    
    /// <summary>
    /// Envia uma requisição GET
    /// </summary>
    public IEnumerator GetJson(string path, Action<string> onSuccess, Action<string> onError)
    {
        // Validações
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[ApiClient] Path cannot be null or empty.");
            onError?.Invoke("Invalid path");
            yield break;
        }
        
        if (string.IsNullOrEmpty(baseUrl))
        {
            Debug.LogError("[ApiClient] Base URL is not configured!");
            onError?.Invoke("Base URL not configured");
            yield break;
        }
        
        // Construir URL completa
        string url = baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        
        if (enableDebugLogs)
        {
            Debug.Log($"[ApiClient] GET {url}");
        }
        
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.timeout = timeoutSeconds;

        yield return www.SendWebRequest();
        
        try
        {
            if (www.result == UnityWebRequest.Result.ConnectionError || 
                www.result == UnityWebRequest.Result.ProtocolError)
            {
                string errorMsg = $"{www.error}";
                if (www.downloadHandler != null && !string.IsNullOrEmpty(www.downloadHandler.text))
                {
                    errorMsg += $" - {www.downloadHandler.text}";
                }
                
                if (enableDebugLogs)
                {
                    Debug.LogError($"[ApiClient] GET Error: {errorMsg}");
                }
                
                onError?.Invoke(errorMsg);
            }
            else
            {
                string response = www.downloadHandler?.text ?? "";
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[ApiClient] GET Success: {response}");
                }
                
                onSuccess?.Invoke(response);
            }
        }
        catch (Exception ex)
        {
            string errorMsg = $"Exception processing GET response: {ex.Message}";
            Debug.LogError($"[ApiClient] {errorMsg}");
            onError?.Invoke(errorMsg);
        }
        finally
        {
            www.Dispose();
        }
    }
}