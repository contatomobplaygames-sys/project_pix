using UnityEngine;
using System.Collections;

public class InternetConnectionManager : MonoBehaviour
{
    [Header("Painel de Sem Internet")]
    public GameObject noInternetPanel;

    [Header("Intervalo de Verificação (segundos)")]
    public float checkInterval = 2f;

    private bool isConnected = true;

    void Start()
    {
        if (noInternetPanel != null)
            noInternetPanel.SetActive(false);

        StartCoroutine(CheckInternetConnection());
    }

    IEnumerator CheckInternetConnection()
    {
        while (true)
        {
            bool hasInternet = Application.internetReachability != NetworkReachability.NotReachable;

            if (!hasInternet && isConnected)
            {
                // Perdeu a internet
                isConnected = false;
                ShowNoInternetPanel();
            }
            else if (hasInternet && !isConnected)
            {
                // Internet voltou
                isConnected = true;
                HideNoInternetPanel();
            }

            yield return new WaitForSeconds(checkInterval);
        }
    }

    void ShowNoInternetPanel()
    {
        if (noInternetPanel != null)
            noInternetPanel.SetActive(true);
    }

    void HideNoInternetPanel()
    {
        if (noInternetPanel != null)
            noInternetPanel.SetActive(false);
    }
}
