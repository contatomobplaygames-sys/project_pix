using UnityEngine;
using Ads;
using System.Collections;

namespace Ads
{
    /// <summary>
    /// Exemplo de uso do sistema de troca de anunciante via Firebase Remote Config
    /// Este script demonstra como verificar e atualizar o anunciante dinamicamente
    /// </summary>
    public class FirebaseAdsProviderExample : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Se verdadeiro, verifica o anunciante ao iniciar")]
        [SerializeField] private bool checkOnStart = true;
        
        [Tooltip("Intervalo em segundos para verificar atualizações do Firebase")]
        [SerializeField] private float checkInterval = 300f; // 5 minutos
        
        private void Start()
        {
            if (checkOnStart)
            {
                StartCoroutine(CheckAdsProviderOnStart());
            }
            
            // Verificar periodicamente por atualizações
            if (checkInterval > 0)
            {
                InvokeRepeating(nameof(CheckForUpdates), checkInterval, checkInterval);
            }
        }
        
        /// <summary>
        /// Verifica o anunciante após um delay para dar tempo do Firebase inicializar
        /// </summary>
        private IEnumerator CheckAdsProviderOnStart()
        {
            // Aguardar Firebase inicializar
            yield return new WaitForSeconds(3f);
            
            LogCurrentAdsProvider();
        }
        
        /// <summary>
        /// Verifica se há atualizações no Firebase e atualiza se necessário
        /// </summary>
        public void CheckForUpdates()
        {
            if (FirebaseRemoteConfigManager.Instance != null && 
                FirebaseRemoteConfigManager.Instance.IsRemoteConfigReady())
            {
                string firebaseProvider = FirebaseRemoteConfigManager.Instance.GetActiveAdsProvider();
                string currentProvider = AdsInitializer.Instance.ActiveNetworkKey;
                
                // Normalizar para comparação
                string normalizedFirebase = NormalizeKey(firebaseProvider);
                string normalizedCurrent = NormalizeKey(currentProvider);
                
                if (normalizedFirebase != normalizedCurrent)
                {
                    Debug.Log($"[FirebaseAdsProviderExample] 🔄 Anunciante mudou no Firebase! Atualizando...");
                    Debug.Log($"[FirebaseAdsProviderExample] Anterior: {currentProvider} → Novo: {firebaseProvider}");
                    
                    // Forçar atualização e reinicialização
                    AdsInitializer.Instance.UpdateFromFirebase();
                }
            }
        }
        
        /// <summary>
        /// Normaliza a chave para comparação
        /// </summary>
        private string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return "";
            
            return key.Trim().ToLower();
        }
        
        /// <summary>
        /// Loga informações sobre o anunciante atual
        /// </summary>
        public void LogCurrentAdsProvider()
        {
            if (AdsInitializer.Instance == null)
            {
                Debug.LogWarning("[FirebaseAdsProviderExample] AdsInitializer não encontrado!");
                return;
            }
            
            string currentProvider = AdsInitializer.Instance.ActiveNetworkKey;
            string networkType = AdsInitializer.Instance.ActiveNetworkType;
            bool isInitialized = AdsInitializer.Instance.IsInitialized;
            
            Debug.Log("=== 📊 Status do Sistema de Anúncios ===");
            Debug.Log($"Anunciante Ativo: {currentProvider}");
            Debug.Log($"Tipo de Rede: {networkType}");
            Debug.Log($"Sistema Inicializado: {isInitialized}");
            
            if (FirebaseRemoteConfigManager.Instance != null)
            {
                bool firebaseReady = FirebaseRemoteConfigManager.Instance.IsRemoteConfigReady();
                string firebaseProvider = FirebaseRemoteConfigManager.Instance.GetActiveAdsProvider();
                
                Debug.Log($"Firebase Inicializado: {FirebaseRemoteConfigManager.Instance.IsFirebaseInitialized}");
                Debug.Log($"Remote Config Pronto: {firebaseReady}");
                Debug.Log($"Anunciante do Firebase: {firebaseProvider}");
            }
            
            Debug.Log("========================================");
        }
        
        /// <summary>
        /// Força atualização do Firebase (pode ser chamado de um botão UI, por exemplo)
        /// </summary>
        public void ForceUpdateFromFirebase()
        {
            Debug.Log("[FirebaseAdsProviderExample] 🔄 Forçando atualização do Firebase...");
            AdsInitializer.Instance.UpdateFromFirebase();
        }
        
        // Métodos para chamar de UI (botões, etc.)
        public void OnButtonCheckProvider()
        {
            LogCurrentAdsProvider();
        }
        
        public void OnButtonForceUpdate()
        {
            ForceUpdateFromFirebase();
        }
    }
}

