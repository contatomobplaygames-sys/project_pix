using System.Collections;
using Ads;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla o fluxo global de anúncios no nível Unity:
/// - garante banner ao iniciar o app (startup)
/// - exibe interstitial em trocas de cena Unity reais (SceneManager)
/// 
/// NOTA: trocas de rota dentro do WebView React são tratadas pelo
/// AdsWebViewHandler (que recebe mensagens do Layout.tsx via unityBridge).
/// </summary>
public class AdsSceneFlowController : MonoBehaviour
{
    private const float InterstitialDelaySeconds = 0.35f;
    private const float BannerWaitTimeoutSeconds = 12f;
    private const float InterstitialRequestLockSeconds = 1.0f;

    [Header("Anti-Spam Interstitial")]
    [Tooltip("Intervalo mínimo (segundos) entre interstitials para evitar spam e problemas de política.")]
    [SerializeField] private float minInterstitialIntervalSeconds = 30f;
    [Tooltip("Se verdadeiro, exibe logs detalhando quando o interstitial foi bloqueado por cooldown.")]
    [SerializeField] private bool logCooldownBlocks = true;

    private bool isSubscribed;
    private bool isInterstitialRequestInFlight;
    private float lastInterstitialShownAt = -9999f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var existing = FindObjectOfType<AdsSceneFlowController>();
        if (existing != null) return;

        var go = new GameObject("AdsSceneFlowController");
        DontDestroyOnLoad(go);
        go.AddComponent<AdsSceneFlowController>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        EnsureAdsInitializerExists();
        SubscribeSceneEvents();
    }

    private void Start()
    {
        StartCoroutine(EnsureBannerOnAppStart());
    }

    private void OnDestroy()
    {
        UnsubscribeSceneEvents();
    }

    private void SubscribeSceneEvents()
    {
        if (isSubscribed) return;

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        isSubscribed = true;
    }

    private void UnsubscribeSceneEvents()
    {
        if (!isSubscribed) return;

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        isSubscribed = false;
    }

    private void EnsureAdsInitializerExists()
    {
        AdsInitializer.EnsureInitialized();
    }

    private IEnumerator EnsureBannerOnAppStart()
    {
        float elapsed = 0f;
        while (!AdsInitializer.Instance.IsInitialized && elapsed < BannerWaitTimeoutSeconds)
        {
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (!AdsInitializer.Instance.IsInitialized)
        {
            Debug.LogWarning("[AdsSceneFlowController] Ads não inicializado a tempo para exibir banner no startup.");
            yield break;
        }

        try
        {
            AdsAPI.ShowBanner();
            Debug.Log("[AdsSceneFlowController] Banner exibido ao iniciar o aplicativo.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AdsSceneFlowController] Erro ao exibir banner no startup: {ex.Message}");
        }
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        if (!CanRequestInterstitial(next.name))
        {
            return;
        }

        StartCoroutine(ShowInterstitialAfterSceneSwitch(next.name));
    }

    private IEnumerator ShowInterstitialAfterSceneSwitch(string sceneName)
    {
        isInterstitialRequestInFlight = true;
        yield return new WaitForSeconds(InterstitialDelaySeconds);

        if (!AdsInitializer.Instance.IsInitialized)
        {
            Debug.LogWarning($"[AdsSceneFlowController] Ads ainda não inicializado. Interstitial ignorado na cena '{sceneName}'.");
            ReleaseRequestLockWithDelay();
            yield break;
        }

        try
        {
            AdsAPI.ShowInterstitial();
            lastInterstitialShownAt = Time.realtimeSinceStartup;
            Debug.Log($"[AdsSceneFlowController] Interstitial exibido na troca para cena '{sceneName}'.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AdsSceneFlowController] Erro ao exibir interstitial na cena '{sceneName}': {ex.Message}");
        }
        
        ReleaseRequestLockWithDelay();
    }

    private bool CanRequestInterstitial(string sceneName)
    {
        if (isInterstitialRequestInFlight)
        {
            if (logCooldownBlocks)
            {
                Debug.Log($"[AdsSceneFlowController] Interstitial bloqueado (request em andamento) na cena '{sceneName}'.");
            }
            return false;
        }

        float elapsed = Time.realtimeSinceStartup - lastInterstitialShownAt;
        if (elapsed < minInterstitialIntervalSeconds)
        {
            if (logCooldownBlocks)
            {
                float remaining = minInterstitialIntervalSeconds - elapsed;
                Debug.Log($"[AdsSceneFlowController] Interstitial bloqueado por cooldown ({remaining:F1}s restantes) na cena '{sceneName}'.");
            }
            return false;
        }

        return true;
    }

    private void ReleaseRequestLockWithDelay()
    {
        if (!gameObject.activeInHierarchy)
        {
            isInterstitialRequestInFlight = false;
            return;
        }

        StartCoroutine(ReleaseRequestLockRoutine());
    }

    private IEnumerator ReleaseRequestLockRoutine()
    {
        yield return new WaitForSeconds(InterstitialRequestLockSeconds);
        isInterstitialRequestInFlight = false;
    }
}
