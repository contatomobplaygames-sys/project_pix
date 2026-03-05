using UnityEngine;
using Ads;
using System;
using System.Collections;

namespace Ads
{
    /// <summary>
    /// Componente responsável por inicializar automaticamente os anúncios e exibir o banner
    /// ao iniciar o aplicativo. Verifica qual rede está ativa no AdsSettings antes de inicializar.
    /// Implementa padrão Singleton para garantir inicialização única e persistência entre cenas.
    /// </summary>
    public class AdsInitializer : MonoBehaviour
    {
        private static AdsInitializer _instance;
        
        [Header("Settings")]
        [Tooltip("Se verdadeiro, o banner será exibido automaticamente após a inicialização")]
        [SerializeField] private bool showBannerOnStart = true;
        
        [Tooltip("Se verdadeiro, exibe logs de debug")]
        [SerializeField] private bool enableDebug = true;
        
        [Tooltip("Se verdadeiro, o objeto não será destruído ao carregar novas cenas")]
        [SerializeField] private bool dontDestroyOnLoad = true;
        
        [Header("Firebase Remote Config")]
        [Tooltip("Se verdadeiro, usa Firebase Remote Config para determinar o anunciante")]
        [SerializeField] private bool useFirebaseRemoteConfig = true;
        
        [Tooltip("Timeout em segundos para aguardar Firebase Remote Config (0 = sem timeout)")]
        [SerializeField] private float firebaseTimeoutSeconds = 5f;

        private bool isInitialized = false;
        private string activeNetworkKey = null;
        private string activeNetworkType = null;
        private bool isWaitingForFirebase = false;

        /// <summary>
        /// Instância singleton do AdsInitializer
        /// </summary>
        public static AdsInitializer Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Tentar encontrar instância existente
                    _instance = FindObjectOfType<AdsInitializer>();
                    
                    // Se não existir, criar uma nova
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("AdsInitializer");
                        _instance = go.AddComponent<AdsInitializer>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            // Implementar padrão Singleton
            if (_instance == null)
            {
                _instance = this;
                if (dontDestroyOnLoad)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (_instance != this)
            {
                // Se já existe uma instância, destruir esta
                if (enableDebug)
                    Debug.LogWarning("[AdsInitializer] Instância duplicada detectada. Destruindo...");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeAdsSystem();
        }

        /// <summary>
        /// Inicializa o sistema de anúncios verificando qual rede está ativa no AdsSettings
        /// Se useFirebaseRemoteConfig estiver habilitado, busca a configuração do Firebase primeiro
        /// </summary>
        public void InitializeAdsSystem()
        {
            if (isInitialized)
            {
                if (enableDebug)
                    Debug.LogWarning("[AdsInitializer] Sistema de anúncios já foi inicializado.");
                return;
            }

            try
            {
                // Verificar se o AdsSettings existe
                if (AdsSettings.Instance == null)
                {
                    Debug.LogError("[AdsInitializer] ❌ AdsSettings não encontrado! Verifique se o asset existe em Assets/Ads/Resources/AdsSettings.asset");
                    return;
                }

                // Se Firebase Remote Config estiver habilitado, buscar configuração do Firebase
                if (useFirebaseRemoteConfig)
                {
                    if (enableDebug)
                        Debug.Log("[AdsInitializer] 🔥 Usando Firebase Remote Config para determinar anunciante...");
                    
                    StartCoroutine(InitializeWithFirebase());
                }
                else
                {
                    // Usar configuração local do AdsSettings
                    InitializeWithLocalSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AdsInitializer] ❌ Erro ao inicializar sistema de anúncios: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Inicializa usando Firebase Remote Config
        /// </summary>
        private System.Collections.IEnumerator InitializeWithFirebase()
        {
            isWaitingForFirebase = true;
            
            // Garantir que o FirebaseRemoteConfigManager existe
            FirebaseRemoteConfigManager firebaseManager = FirebaseRemoteConfigManager.Instance;
            
            // Inicializar Firebase se ainda não estiver inicializado
            if (!firebaseManager.IsFirebaseInitialized)
            {
                firebaseManager.InitializeFirebase();
                
                // Aguardar Firebase inicializar (com timeout)
                float elapsedTime = 0f;
                while (!firebaseManager.IsFirebaseInitialized && elapsedTime < firebaseTimeoutSeconds)
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsedTime += 0.1f;
                }
                
                if (!firebaseManager.IsFirebaseInitialized)
                {
                    Debug.LogWarning("[AdsInitializer] ⚠️ Timeout ao aguardar Firebase. Usando configuração local.");
                    isWaitingForFirebase = false;
                    InitializeWithLocalSettings();
                    yield break;
                }
            }
            
            // Buscar Remote Config e aguardar completar
            firebaseManager.FetchRemoteConfig();
            
            // Aguardar Remote Config estar pronto (com timeout aumentado)
            float remoteConfigElapsedTime = 0f;
            float maxWaitTime = firebaseTimeoutSeconds * 2f; // Dobrar o timeout para dar tempo do fetch
            
            if (enableDebug)
                Debug.Log($"[AdsInitializer] ⏳ Aguardando Remote Config estar pronto (timeout: {maxWaitTime}s)...");
            
            while (!firebaseManager.IsRemoteConfigReady() && remoteConfigElapsedTime < maxWaitTime)
            {
                yield return new WaitForSeconds(0.1f);
                remoteConfigElapsedTime += 0.1f;
            }
            
            if (!firebaseManager.IsRemoteConfigReady())
            {
                Debug.LogWarning($"[AdsInitializer] ⚠️ Timeout ao aguardar Remote Config ({maxWaitTime}s). Usando configuração local.");
                isWaitingForFirebase = false;
                InitializeWithLocalSettings();
                yield break;
            }
            
            // Obter anunciante do Firebase
            string firebaseAdsProvider = firebaseManager.GetActiveAdsProvider();
            
            if (enableDebug)
            {
                Debug.Log($"[AdsInitializer] 📡 Anunciante obtido do Firebase: {firebaseAdsProvider}");
                Debug.Log($"[AdsInitializer] ⏱️ Tempo de espera: {remoteConfigElapsedTime:F1}s");
            }
            
            // Atualizar AdsSettings com o anunciante do Firebase
            UpdateAdsProviderFromFirebase(firebaseAdsProvider);
            
            isWaitingForFirebase = false;
            
            // Inicializar com a configuração atualizada
            InitializeWithLocalSettings();
        }

        /// <summary>
        /// Atualiza o anunciante no AdsSettings baseado no valor do Firebase
        /// </summary>
        private void UpdateAdsProviderFromFirebase(string firebaseProvider)
        {
            try
            {
                // Normalizar a chave para corresponder ao formato do AdsSettings
                string normalizedKey = NormalizeProviderKey(firebaseProvider);
                
                // Verificar se a chave existe no AdsSettings
                if (AdsSettings.Instance != null)
                {
                    var adsObject = AdsSettings.GetAdsObject(normalizedKey);
                    if (adsObject != null)
                    {
                        // Atualizar a chave primária no AdsSettings
                        AdsSettings.Instance.SetPrimaryAdsKey(normalizedKey);
                        
                        if (enableDebug)
                            Debug.Log($"[AdsInitializer] ✅ Anunciante atualizado no AdsSettings: {normalizedKey}");
                    }
                    else
                    {
                        Debug.LogWarning($"[AdsInitializer] ⚠️ Anunciante '{normalizedKey}' do Firebase não encontrado no AdsSettings. Usando padrão.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AdsInitializer] ❌ Erro ao atualizar anunciante do Firebase: {ex.Message}");
            }
        }

        /// <summary>
        /// Garante que AdMob seja a rede primária se existir no dicionário
        /// </summary>
        private void EnsureAdmobIsPrimary()
        {
            try
            {
                if (AdsSettings.Instance == null) return;
                
                // Verificar se AdMob existe no dicionário (tentar variações comuns)
                string[] admobKeys = { "Admob", "AdmobAds", "Google AdMob" };
                string foundAdmobKey = null;
                
                foreach (string key in admobKeys)
                {
                    var adsObject = AdsSettings.GetAdsObject(key);
                    if (adsObject != null)
                    {
                        foundAdmobKey = key;
                        break;
                    }
                }
                
                // Se AdMob foi encontrado e não é a rede primária, definir como primária
                if (!string.IsNullOrEmpty(foundAdmobKey))
                {
                    string currentPrimary = AdsSettings.GetPrimaryAdsKey();
                    if (currentPrimary != foundAdmobKey)
                    {
                        if (enableDebug)
                            Debug.Log($"[AdsInitializer] 🔄 Definindo AdMob como rede primária (chave: {foundAdmobKey})");
                        
                        AdsSettings.Instance.SetPrimaryAdsKey(foundAdmobKey);
                    }
                }
            }
            catch (Exception ex)
            {
                if (enableDebug)
                    Debug.LogWarning($"[AdsInitializer] ⚠️ Erro ao garantir AdMob como primário: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Normaliza a chave do provider para corresponder ao formato do AdsSettings
        /// </summary>
        private string NormalizeProviderKey(string provider)
        {
            if (string.IsNullOrEmpty(provider))
            {
                if (enableDebug)
                    Debug.LogWarning("[AdsInitializer] ⚠️ Provider vazio. Usando padrão do AdsSettings.");
                return AdsSettings.GetPrimaryAdsKey() ?? "Admob";
            }
            
            string normalized = provider.Trim();
            
            if (enableDebug)
                Debug.Log($"[AdsInitializer] 🔄 Normalizando provider: '{provider}' -> '{normalized}'");
            
            // Mapear variações para as chaves esperadas no AdsSettings
            if (normalized.Equals("Admob", StringComparison.OrdinalIgnoreCase) || 
                normalized.Equals("Ad Mob", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Google", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Google AdMob", StringComparison.OrdinalIgnoreCase))
            {
                if (enableDebug)
                    Debug.Log($"[AdsInitializer] ✅ Provider normalizado para: Admob");
                return "Admob";
            }
            
            if (normalized.Equals("AppLovin", StringComparison.OrdinalIgnoreCase) || 
                normalized.Equals("MAX", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("AppLovin MAX", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Applovin", StringComparison.OrdinalIgnoreCase))
            {
                if (enableDebug)
                    Debug.Log($"[AdsInitializer] ✅ Provider normalizado para: AppLovin");
                return "AppLovin";
            }
            
            // Se não reconhecer, tentar usar como está (pode ser que já esteja no formato correto)
            if (enableDebug)
                Debug.LogWarning($"[AdsInitializer] ⚠️ Provider não reconhecido: '{normalized}'. Usando como está.");
            return normalized;
        }

        /// <summary>
        /// Inicializa usando configuração local do AdsSettings
        /// </summary>
        private void InitializeWithLocalSettings()
        {
            // Se já está inicializado, não reinicializar
            if (isInitialized)
            {
                if (enableDebug)
                    Debug.LogWarning("[AdsInitializer] Sistema já inicializado. Pulando reinicialização.");
                return;
            }

            try
            {
                // Garantir que AdMob seja a rede primária se existir no dicionário
                EnsureAdmobIsPrimary();
                
                // Ocultar banner anterior se houver
                try
                {
                    AdsAPI.HideBanner();
                }
                catch
                {
                    // Ignorar erro se não houver banner para ocultar
                }

                // Obter a chave da rede primária
                activeNetworkKey = AdsSettings.GetPrimaryAdsKey();

                if (string.IsNullOrEmpty(activeNetworkKey))
                {
                    Debug.LogError("[AdsInitializer] ❌ Nenhuma rede de anúncios configurada no AdsSettings!");
                    return;
                }

                // Identificar o tipo de rede
                activeNetworkType = GetNetworkType(activeNetworkKey);
                
                if (enableDebug)
                {
                    Debug.Log($"[AdsInitializer] ✅ Rede ativa detectada: {activeNetworkKey} ({activeNetworkType})");
                    Debug.Log($"[AdsInitializer] Inicializando sistema de anúncios...");
                }

                // Inicializar a rede de anúncios
                AdsAPI.InitializeAds(activeNetworkKey, OnAdsInitialized);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AdsInitializer] ❌ Erro ao inicializar com configuração local: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Callback chamado quando os anúncios são inicializados
        /// </summary>
        private void OnAdsInitialized(bool success)
        {
            if (!success)
            {
                Debug.LogError("[AdsInitializer] ❌ Falha ao inicializar sistema de anúncios!");
                return;
            }

            isInitialized = true;

            if (enableDebug)
            {
                Debug.Log($"[AdsInitializer] ✅ Sistema de anúncios inicializado com sucesso!");
                Debug.Log($"[AdsInitializer] Rede ativa: {activeNetworkKey} ({activeNetworkType})");
            }

            // Exibir banner automaticamente se configurado
            if (showBannerOnStart)
            {
                if (enableDebug)
                    Debug.Log($"[AdsInitializer] 🎯 Exibindo banner automaticamente ({activeNetworkType})...");
                
                // Pequeno delay para garantir que o SDK está completamente pronto
                Invoke(nameof(ShowBannerDelayed), 0.5f);
            }
        }

        /// <summary>
        /// Exibe o banner com um pequeno delay para garantir que o SDK está pronto
        /// </summary>
        private void ShowBannerDelayed()
        {
            try
            {
                AdsAPI.ShowBanner();
                if (enableDebug)
                    Debug.Log($"[AdsInitializer] ✅ Banner exibido com sucesso! ({activeNetworkType})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AdsInitializer] ❌ Erro ao exibir banner: {ex.Message}");
            }
        }

        /// <summary>
        /// Identifica o tipo de rede baseado na chave
        /// </summary>
        private string GetNetworkType(string networkKey)
        {
            if (string.IsNullOrEmpty(networkKey))
                return "Desconhecida";

            string keyLower = networkKey.ToLower().Trim();
            
            // Verificar se é MAX (AppLovin)
            if (keyLower == "max" || keyLower.Contains("max") || keyLower.Contains("applovin"))
                return "AppLovin MAX";
            
            // Verificar se é AdMob (Google)
            if (keyLower == "admob" || keyLower.Contains("admob") || keyLower.Contains("google"))
                return "Google AdMob";

            return "Desconhecida";
        }
        
        /// <summary>
        /// Obtém o tipo de rede ativa
        /// </summary>
        public string ActiveNetworkType => activeNetworkType;

        /// <summary>
        /// Método público para forçar a reinicialização do sistema de anúncios
        /// </summary>
        public void Reinitialize()
        {
            isInitialized = false;
            isWaitingForFirebase = false;
            InitializeAdsSystem();
        }
        
        /// <summary>
        /// Força atualizar o anunciante do Firebase e reinicializar
        /// </summary>
        public void UpdateFromFirebase()
        {
            if (useFirebaseRemoteConfig)
            {
                if (enableDebug)
                    Debug.Log("[AdsInitializer] 🔄 Forçando atualização do Firebase Remote Config...");
                
                FirebaseRemoteConfigManager.Instance.ForceUpdate();
                StartCoroutine(WaitAndReinitialize());
            }
        }
        
        private System.Collections.IEnumerator WaitAndReinitialize()
        {
            yield return new WaitForSeconds(1f);
            Reinitialize();
        }

        /// <summary>
        /// Verifica se o sistema está inicializado
        /// </summary>
        public bool IsInitialized => isInitialized;

        /// <summary>
        /// Obtém a chave da rede ativa
        /// </summary>
        public string ActiveNetworkKey => activeNetworkKey;
        
        /// <summary>
        /// Verifica se a rede ativa é MAX
        /// </summary>
        public bool IsMaxActive => activeNetworkType == "AppLovin MAX";
        
        /// <summary>
        /// Verifica se a rede ativa é AdMob
        /// </summary>
        public bool IsAdmobActive => activeNetworkType == "Google AdMob";
        
        /// <summary>
        /// Método estático para garantir que o AdsInitializer seja inicializado
        /// Pode ser chamado de qualquer lugar do código
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_instance == null)
            {
                Instance.InitializeAdsSystem();
            }
        }
    }
}

