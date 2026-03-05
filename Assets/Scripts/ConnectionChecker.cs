using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Networking;

public class ConnectionChecker : MonoBehaviour
{
    public TMP_Text connectionStatusText;
    public GameObject noConnectionPanel;
    public int maxRetryAttempts = 3;

    public UniWebView webPrefab;

    private int consecutiveFailures = 0;
    private bool wasPreviouslyDisconnected = false;
    private bool firstCheckDone = false;

    public string urlAoReconectar;

    void Start()
    {
        if (webPrefab == null)
        {
            webPrefab = FindObjectOfType<UniWebView>();
            Debug.LogWarning(webPrefab != null ? "✅ UniWebView encontrado automaticamente." : "⚠️ Nenhum UniWebView encontrado na cena!");
        }

        // Garante que o WebView não fique ativo antes da verificação
        if (webPrefab != null)
        {
            webPrefab.gameObject.SetActive(false);
        }

        StartCoroutine(CheckInternetConnection());
    }

    IEnumerator CheckInternetConnection()
    {
        while (true)
        {
            bool isConnected = false;

            // Verifica primeiro se a conexão está completamente indisponível
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.Log("Sem conexão de internet (NotReachable)");
                HandleNoConnection();
                yield return new WaitForSeconds(5f);
                continue;
            }

            // Tenta validar conexão com um endpoint confiável
            using (UnityWebRequest www = UnityWebRequest.Get("https://clients3.google.com/generate_204"))
            {
                www.timeout = 5;
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    isConnected = true;
                    consecutiveFailures = 0;

                    // Atualiza o texto do status da conexão
                    connectionStatusText.text = (Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork)
                        ? "Conectado via Dados Móveis"
                        : "Conectado via Wi-Fi";

                    noConnectionPanel.SetActive(false);

                    if (!firstCheckDone)
                    {
                        // Primeira vez com internet detectada — carrega o WebView
                        Debug.Log("Primeira verificação com internet — iniciando WebView...");
                        if (webPrefab != null)
                        {
                            webPrefab.gameObject.SetActive(true);
                            webPrefab.Load(urlAoReconectar);
                        }
                        firstCheckDone = true;
                    }
                    else if (wasPreviouslyDisconnected)
                    {
                        // Reconectado após perda de conexão — recarrega
                        Debug.Log("Reconectado — recarregando WebView...");
                        if (webPrefab != null)
                        {
                            webPrefab.LoadNewUrl(urlAoReconectar);
                        }
                        wasPreviouslyDisconnected = false;
                    }
                }
                else
                {
                    consecutiveFailures++;
                    Debug.LogWarning("Falha na verificação de internet. Tentativa: " + consecutiveFailures);
                }
            }

            // Após múltiplas falhas, assume que está offline
            if (!isConnected && consecutiveFailures >= maxRetryAttempts)
            {
                Debug.Log("Conexão instável — número máximo de tentativas excedido.");
                HandleNoConnection();
            }

            yield return new WaitForSeconds(5f);
        }
    }

    private void HandleNoConnection()
    {
        connectionStatusText.text = "Sem conexão com a internet";
        noConnectionPanel.SetActive(true);

        if (webPrefab != null)
        {
            webPrefab.CloseWebView();
            webPrefab.gameObject.SetActive(false);
            Debug.Log("WebView fechado por ausência de internet.");
        }
        else
        {
            Debug.LogWarning("webPrefab está NULL — nenhum WebView para fechar.");
        }

        wasPreviouslyDisconnected = true;
    }
}
