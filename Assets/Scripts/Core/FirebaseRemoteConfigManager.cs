using System;
using System.Collections;
using UnityEngine;
using Firebase;
using Firebase.RemoteConfig;
using System.Threading.Tasks;

namespace Ads
{
    /// <summary>
    /// Gerenciador do Firebase Remote Config para controle dinâmico do anunciante
    /// Permite trocar o anunciante (Admob/MAX) remotamente sem atualizar o app
    /// </summary>
    public class FirebaseRemoteConfigManager : MonoBehaviour
    {
        private static FirebaseRemoteConfigManager _instance;
        
        [Header("Firebase Remote Config Settings")]
        [Tooltip("Chave no Remote Config que define qual anunciante usar (ex: 'admob' ou 'max')")]
        [SerializeField] private string remoteConfigKey = "active_ads_provider";
        
        [Tooltip("Anunciante padrão caso o Firebase falhe ou não esteja configurado")]
        [SerializeField] private string defaultAdsProvider = "Admob";
        
        [Tooltip("Tempo de cache do Remote Config em segundos (0 = sempre buscar do servidor)")]
        [SerializeField] private long cacheExpirationSeconds = 3600; // 1 hora
        
        [Tooltip("Se verdadeiro, força buscar do servidor ignorando cache")]
        [SerializeField] private bool forceFetch = false;
        
        [Tooltip("Se verdadeiro, exibe logs detalhados")]
        [SerializeField] private bool enableDebug = true;
        
        private bool isFirebaseInitialized = false;
        private bool isRemoteConfigReady = false;
        private string currentAdsProvider = null;
        
        // Eventos
        public event Action<string> OnAdsProviderChanged;
        public event Action<bool> OnRemoteConfigReady;
        
        /// <summary>
        /// Instância singleton do FirebaseRemoteConfigManager
        /// </summary>
        public static FirebaseRemoteConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<FirebaseRemoteConfigManager>();
                    
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("FirebaseRemoteConfigManager");
                        _instance = go.AddComponent<FirebaseRemoteConfigManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void Start()
        {
            InitializeFirebase();
        }
        
        /// <summary>
        /// Inicializa o Firebase e Remote Config
        /// </summary>
        public void InitializeFirebase()
        {
            if (isFirebaseInitialized)
            {
                if (enableDebug)
                    Debug.Log("[FirebaseRemoteConfig] Firebase já está inicializado.");
                return;
            }
            
            StartCoroutine(InitializeFirebaseCoroutine());
        }
        
        private IEnumerator InitializeFirebaseCoroutine()
        {
            if (enableDebug)
                Debug.Log("[FirebaseRemoteConfig] 🔥 Inicializando Firebase...");
            
            // Verificar dependências do Firebase
            var dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
            yield return new WaitUntil(() => dependencyTask.IsCompleted);
            
            if (dependencyTask.Exception != null)
            {
                Debug.LogError($"[FirebaseRemoteConfig] ❌ Erro ao verificar dependências do Firebase: {dependencyTask.Exception}");
                OnFirebaseInitialized(false);
                yield break;
            }
            
            var dependencyStatus = dependencyTask.Result;
            if (dependencyStatus != DependencyStatus.Available)
            {
                Debug.LogError($"[FirebaseRemoteConfig] ❌ Firebase não disponível: {dependencyStatus}");
                OnFirebaseInitialized(false);
                yield break;
            }
            
            // Configurar valores padrão do Remote Config
            SetDefaultRemoteConfigValues();
            
            // Marcar Firebase como inicializado (mas Remote Config ainda não está pronto)
            isFirebaseInitialized = true;
            OnFirebaseInitialized(true);
            
            // Buscar configurações remotas (isso vai atualizar isRemoteConfigReady quando completar)
            StartCoroutine(FetchRemoteConfigCoroutine());
        }
        
        /// <summary>
        /// Define valores padrão para o Remote Config
        /// </summary>
        private void SetDefaultRemoteConfigValues()
        {
            var defaults = new System.Collections.Generic.Dictionary<string, object>();
            defaults.Add(remoteConfigKey, defaultAdsProvider);
            
            FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults);
            
            if (enableDebug)
                Debug.Log($"[FirebaseRemoteConfig] ✅ Valores padrão configurados: {remoteConfigKey} = {defaultAdsProvider}");
        }
        
        /// <summary>
        /// Busca configurações do Remote Config do servidor
        /// </summary>
        public void FetchRemoteConfig()
        {
            if (!isFirebaseInitialized)
            {
                Debug.LogWarning("[FirebaseRemoteConfig] ⚠️ Firebase não está inicializado. Inicializando primeiro...");
                InitializeFirebase();
                return;
            }
            
            StartCoroutine(FetchRemoteConfigCoroutine());
        }
        
        private IEnumerator FetchRemoteConfigCoroutine()
        {
            if (!isFirebaseInitialized)
            {
                Debug.LogWarning("[FirebaseRemoteConfig] ⚠️ Firebase não está inicializado. Inicializando...");
                InitializeFirebase();
                yield return new WaitForSeconds(1f);
            }
            
            if (enableDebug)
                Debug.Log("[FirebaseRemoteConfig] 📡 Buscando configurações do Remote Config...");
            
            Task fetchTask;
            
            if (forceFetch)
            {
                // Força buscar do servidor ignorando cache
                fetchTask = FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.Zero);
            }
            else
            {
                // Usa cache se disponível, senão busca do servidor
                fetchTask = FirebaseRemoteConfig.DefaultInstance.FetchAsync(TimeSpan.FromSeconds(cacheExpirationSeconds));
            }
            
            yield return new WaitUntil(() => fetchTask.IsCompleted);
            
            if (fetchTask.Exception != null)
            {
                Debug.LogWarning($"[FirebaseRemoteConfig] ⚠️ Erro ao buscar Remote Config: {fetchTask.Exception.Message}");
                Debug.LogWarning("[FirebaseRemoteConfig] Usando valores padrão ou em cache.");
                OnRemoteConfigFetched(false);
                yield break;
            }
            
            // Obter o status do fetch através do Info
            var remoteConfig = FirebaseRemoteConfig.DefaultInstance;
            var info = remoteConfig.Info;
            var fetchStatus = info.LastFetchStatus;
            
            if (fetchStatus == LastFetchStatus.Success)
            {
                // Ativar as configurações buscadas
                var activateTask = remoteConfig.ActivateAsync();
                yield return new WaitUntil(() => activateTask.IsCompleted);
                
                if (enableDebug)
                    Debug.Log("[FirebaseRemoteConfig] ✅ Configurações do Remote Config atualizadas com sucesso!");
                
                OnRemoteConfigFetched(true);
            }
            else
            {
                Debug.LogWarning($"[FirebaseRemoteConfig] ⚠️ Status do fetch: {fetchStatus}. Usando valores em cache ou padrão.");
                OnRemoteConfigFetched(false);
            }
        }
        
        /// <summary>
        /// Obtém o anunciante ativo do Remote Config
        /// </summary>
        public string GetActiveAdsProvider()
        {
            if (!isFirebaseInitialized)
            {
                if (enableDebug)
                    Debug.LogWarning("[FirebaseRemoteConfig] Firebase não inicializado. Retornando anunciante padrão.");
                return defaultAdsProvider;
            }
            
            try
            {
                var configValue = FirebaseRemoteConfig.DefaultInstance.GetValue(remoteConfigKey);
                string provider = configValue.StringValue;
                
                if (enableDebug)
                {
                    Debug.Log($"[FirebaseRemoteConfig] 🔍 Valor bruto do Remote Config: '{provider}' (chave: {remoteConfigKey})");
                }
                
                // Normalizar o valor (remover espaços, converter para formato esperado)
                provider = NormalizeAdsProviderKey(provider);
                
                if (string.IsNullOrEmpty(provider))
                {
                    if (enableDebug)
                        Debug.LogWarning($"[FirebaseRemoteConfig] Valor vazio no Remote Config. Usando padrão: {defaultAdsProvider}");
                    return NormalizeAdsProviderKey(defaultAdsProvider);
                }
                
                if (enableDebug)
                {
                    Debug.Log($"[FirebaseRemoteConfig] 📢 Anunciante obtido do Remote Config: '{provider}' (normalizado de '{configValue.StringValue}')");
                }
                
                currentAdsProvider = provider;
                return provider;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirebaseRemoteConfig] ❌ Erro ao obter anunciante do Remote Config: {ex.Message}");
                return NormalizeAdsProviderKey(defaultAdsProvider);
            }
        }
        
        /// <summary>
        /// Normaliza a chave do anunciante para o formato esperado pelo sistema
        /// </summary>
        private string NormalizeAdsProviderKey(string provider)
        {
            if (string.IsNullOrEmpty(provider))
                return defaultAdsProvider;
            
            string normalized = provider.Trim().ToLower();
            
            // Mapear variações comuns para as chaves esperadas
            if (normalized == "admob" || normalized == "ad mob" || normalized == "google" || normalized == "google ads")
                return "Admob";
            
            if (normalized == "max" || normalized == "applovin" || normalized == "applovin max")
                return "AppLovin";
            
            // Se não reconhecer, retornar capitalizado
            return char.ToUpper(normalized[0]) + normalized.Substring(1);
        }
        
        /// <summary>
        /// Verifica se o Remote Config está pronto para uso
        /// </summary>
        public bool IsRemoteConfigReady()
        {
            return isRemoteConfigReady && isFirebaseInitialized;
        }
        
        /// <summary>
        /// Força atualizar as configurações do servidor
        /// </summary>
        public void ForceUpdate()
        {
            forceFetch = true;
            StartCoroutine(FetchRemoteConfigCoroutine());
            forceFetch = false;
        }
        
        private void OnFirebaseInitialized(bool success)
        {
            if (success)
            {
                if (enableDebug)
                    Debug.Log("[FirebaseRemoteConfig] ✅ Firebase inicializado com sucesso!");
            }
            else
            {
                Debug.LogError("[FirebaseRemoteConfig] ❌ Falha ao inicializar Firebase. Usando configuração local.");
            }
        }
        
        private void OnRemoteConfigFetched(bool success)
        {
            isRemoteConfigReady = true;
            
            if (success)
            {
                string newProvider = GetActiveAdsProvider();
                OnAdsProviderChanged?.Invoke(newProvider);
            }
            
            OnRemoteConfigReady?.Invoke(success);
        }
        
        /// <summary>
        /// Obtém o anunciante atual (pode ser null se ainda não foi buscado)
        /// </summary>
        public string CurrentAdsProvider => currentAdsProvider;
        
        /// <summary>
        /// Verifica se o Firebase está inicializado
        /// </summary>
        public bool IsFirebaseInitialized => isFirebaseInitialized;
    }
}

