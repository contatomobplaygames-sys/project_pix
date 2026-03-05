using System;
using System.Reflection;
using UnityEngine;

namespace Ads
{
    [Serializable]
    public class AdsRect
    {
        public float width;
        public float height;
        public float x;
        public float y;

        public AdsRect(float x, float y, float width, float height)
        {
            this.width = width;
            this.height = height;
            this.x = x;
            this.y = y;
        }
    }

    [Serializable]
    public class Banner
    {
        public AdsRect rect;
    }

    public enum TypeAds
    {
        Banner,
        Interstitial,
        RewardedVideo
    }

    public struct AdsRevenue
    {
        public TypeAds typeAds;
        public long revenue;

        public AdsRevenue(TypeAds typeAds, long revenue)
        {
            this.typeAds = typeAds;
            this.revenue = revenue;
        }
    }

    public static class AdsAPI
    {
        private static IAds ads;
        private static AdsProbability probability;
        private static IAds previousAds = null; // Para controlar o anunciante anterior

        public static bool IsLoadRewarded { get => ads.IsLoadRewarded; }

        public delegate void AdsEvent<T>(T eventArgs);

        public static event AdsEvent<Banner> BannerShowEvent
        {
            add
            {
                ads.BannerShowEvent += value;
            }
            remove
            {
                ads.BannerShowEvent -= value;
            }
        }

        public static event AdsEvent<Banner> BannerCloseEvent
        {
            add
            {
                ads.BannerCloseEvent += value;
            }
            remove
            {
                ads.BannerCloseEvent -= value;
            }
        }

        public static event AdsEvent<AdsRevenue> OnPaidImpression
        {
            add => ads.OnPaidImpression += value;
            remove => ads.OnPaidImpression -= value;
        }

        public static void InitializeAds(Action<bool> initStatus)
        {
            Debug.Log("AdsAPI.InitializeAds() chamado");
            
            try
            {
                // Se já existe um anunciante ativo, desativar antes de inicializar novo
                if (ads != null && previousAds != ads)
                {
                    Debug.Log($"[AdsAPI] 🔄 Desativando anunciante anterior: {ads.GetType().Name}");
                    try
                    {
                        ads.HideBanner();
                        previousAds = ads;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[AdsAPI] ⚠️ Erro ao desativar anunciante anterior: {ex.Message}");
                    }
                }

                var newAds = AdsSettings.GetAdsObject();
                if (newAds == null)
                {
                    Debug.LogError("AdsSettings.GetAdsObject() retornou null! Verifique se há AdsObjects configurados no AdsSettings.");
                    initStatus?.Invoke(false);
                    return;
                }
                
                // Se é o mesmo anunciante, não precisa reinicializar
                if (ads != null && ads == newAds)
                {
                    Debug.Log($"[AdsAPI] ℹ️ Anunciante já está ativo: {ads.GetType().Name}");
                    initStatus?.Invoke(true);
                    return;
                }
                
                Debug.Log($"[AdsAPI] 🚀 Inicializando novo anunciante: {newAds.GetType().Name}");
                
                ads = newAds;
                
                probability = new AdsProbability(AdsSettings.Instance.defaultInterstitialProbability);
                Debug.Log($"AdsProbability criado com defaultInterstitialProbability: {AdsSettings.Instance.defaultInterstitialProbability}");

                ads.Initialize(initStatus);
                BannerBlockClick.InitializeBannerBlock();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Erro ao inicializar AdsAPI: {e.Message}");
                initStatus?.Invoke(false);
            }
        }


        public static void InitializeAds(string adsKey, Action<bool> initStatus)
        {
            InitializeAds(adsKey, new AdsProbability(AdsSettings.Instance.defaultInterstitialProbability), initStatus);
        }

        public static void InitializeAds(string adsKey, AdsProbability adsProbability, Action<bool> initStatus)
        {
            // Se já existe um anunciante ativo e é diferente do novo, desativar o anterior
            if (ads != null && previousAds != ads)
            {
                Debug.Log($"[AdsAPI] 🔄 Desativando anunciante anterior: {ads.GetType().Name}");
                try
                {
                    // Ocultar banner do anunciante anterior
                    ads.HideBanner();
                    previousAds = ads;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[AdsAPI] ⚠️ Erro ao desativar anunciante anterior: {ex.Message}");
                }
            }

            // Obter o novo anunciante
            var newAds = AdsSettings.GetAdsObject(adsKey);
            
            // Se é o mesmo anunciante, não precisa reinicializar
            if (ads != null && ads == newAds)
            {
                Debug.Log($"[AdsAPI] ℹ️ Anunciante já está ativo: {ads.GetType().Name}");
                initStatus?.Invoke(true);
                return;
            }

            Debug.Log($"[AdsAPI] 🚀 Inicializando novo anunciante: {newAds?.GetType().Name ?? "NULL"}");
            
            ads = newAds;
            probability = adsProbability;

            if (ads == null)
            {
                Debug.LogError($"[AdsAPI] ❌ Anunciante '{adsKey}' não encontrado no AdsSettings!");
                initStatus?.Invoke(false);
                return;
            }

            ads.Initialize(initStatus);
            BannerBlockClick.InitializeBannerBlock();
        }

        public static void InitializeAds(AdsProbability adsProbability, Action<bool> initStatus)
        {
            ads = AdsSettings.GetAdsObject();
            probability = adsProbability;

            ads.Initialize(initStatus);
            BannerBlockClick.InitializeBannerBlock();
        }

        public static void ShowBanner()
        {
            Debug.Log("=== AdsAPI.ShowBanner() CHAMADO ===");
            Debug.Log($"ads object: {(ads != null ? ads.GetType().Name : "NULL")}");
            
            if (ads == null)
            {
                Debug.LogError("❌ AdsAPI não foi inicializado! Chame AdsAPI.InitializeAds() primeiro.");
                return;
            }
            
            Debug.Log($"✅ Chamando ads.ShowBanner() - Tipo: {ads.GetType().Name}");
            ads.ShowBanner();
        }

        public static void HideBanner()
        {
            if (ads == null)
            {
                Debug.LogError("AdsAPI não foi inicializado! Chame AdsAPI.InitializeAds() primeiro.");
                return;
            }
            ads.HideBanner();
        }

        public static void ShowRewardedVideo(Action<AdsResult> CallbackRewarded)
        {
            if (ads == null)
            {
                Debug.LogError("AdsAPI não foi inicializado! Chame AdsAPI.InitializeAds() primeiro.");
                CallbackRewarded?.Invoke(new AdsResult(AdsStatus.Failed));
                return;
            }
            ads.ShowRewarded(CallbackRewarded);
        }

        /// <summary>
        /// Carrega o próximo rewarded ad para deixá-lo pronto
        /// </summary>
        public static void LoadNextRewardedAd()
        {
            if (ads == null)
            {
                Debug.LogError("AdsAPI não foi inicializado! Chame AdsAPI.InitializeAds() primeiro.");
                return;
            }
            
            // Usar reflexão ou casting para acessar LoadRewardedAd
            // Como MaxAds e AdmobAds têm métodos LoadRewardedAd, vamos tentar acessar
            try
            {
                if (ads is MaxAds maxAds)
                {
                    // MaxAds tem LoadRewardedAd privado, vamos usar reflexão para acessar
                    var method = typeof(MaxAds).GetMethod("LoadRewardedAd", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (method != null)
                    {
                        method.Invoke(maxAds, null);
                        Debug.Log("[AdsAPI] ✅ Próximo rewarded ad MAX sendo carregado");
                    }
                }
                else if (ads is AdmobAds admobAds)
                {
                    // AdmobAds tem LoadRewardedAd público
                    admobAds.LoadRewardedAd();
                    Debug.Log("[AdsAPI] ✅ Próximo rewarded ad AdMob sendo carregado");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AdsAPI] ❌ Erro ao carregar próximo rewarded ad: {ex.Message}");
            }
        }

        public static void ShowInterstitialOfProbability()
        {
            if (probability.IsShowAd())
                ShowInterstitial();
        }

        public static void ShowInterstitialOfProbability(string key)
        {
            if (probability.IsShowAd(key))
                ShowInterstitial();
        }

        public static void ShowInterstitial()
        {
            if (ads == null)
            {
                Debug.LogError("AdsAPI não foi inicializado! Chame AdsAPI.InitializeAds() primeiro.");
                return;
            }
            ads.ShowInterstitial();
        }

        public static void ShowRewardedInterstitial(Action<AdsResult> CallbackRewarded)
        {
            ads.ShowRewardedInterstitial(CallbackRewarded);
        }

        public static void ShowRewardedInterstitialProbability(Action<AdsResult> CallbackRewarded)
        {
            if (probability.IsShowAd())
                ads.ShowRewardedInterstitial(CallbackRewarded);
        }

        public static void ShowRewardedInterstitialProbability(Action<AdsResult> CallbackRewarded, string key)
        {
            if (probability.IsShowAd(key))
                ads.ShowRewardedInterstitial(CallbackRewarded);
        }
    }
}
