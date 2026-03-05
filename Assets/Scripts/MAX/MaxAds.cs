using Ads;
using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ads
{
    [CreateAssetMenu(fileName = "MaxAds", menuName = "Ads/MAX Ads", order = 1)]
    public class MaxAds : AdsObject
    {
        [Header("Settings")]
        [Header("SDK Key")]
        [Tooltip("SDK Key do AppLovin MAX. Deve corresponder ao SDK Key configurado no AppLovinSettings.asset")]
        public string sdkKey = "305HTZSFID4gKwOH25p3GxdpW_ebL5_hqerdTSs06EhR00P0eMc1KkghSu4wvEUr04vO2xPa4q9RBA95-xcgUi";

        [Space(10)]
        public string interstitialPlacementId = "19e41a9b276c1114";
        public string bannerPlacementId = "6d79176f2326baf5";
        public string rewardedPlacementId = "7d926c81202224bf";

        [Header("Banner Block")]
        public bool isManualBannerRect;
        public AdsRect bannerRect;

        public override bool IsLoadRewarded 
        { 
            get => MaxSdk.IsRewardedAdReady(rewardedPlacementId); 
            set { } // Propriedade somente leitura baseada no estado do SDK
        }

        public override event AdsAPI.AdsEvent<Ads.Banner> BannerShowEvent;
        public override event AdsAPI.AdsEvent<Ads.Banner> BannerCloseEvent;
        public override event AdsAPI.AdsEvent<AdsRevenue> OnPaidImpression;

        private int retryAttempt;
        private Action<bool> pendingInitStatus;



        public override void Initialize(Action<bool> initStatus)
        {
            try
            {
                // Armazenar o callback para chamar quando o SDK estiver inicializado
                pendingInitStatus = initStatus;

                // Registrar callback de inicialização ANTES de inicializar o SDK
                MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;

                // Inicializar o SDK
                MaxSdk.SetSdkKey(sdkKey);
                MaxSdk.InitializeSdk();
                
                Debug.Log("MAX SDK InitializeSdk() chamado - aguardando callback de inicialização...");

                //Initialize Interstitial
                MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
                MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailedEvent;
                MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayedEvent;
                MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClickedEvent;
                MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHiddenEvent;
                MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialAdFailedToDisplayEvent;
                MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialAdRevenuePaidEvent;
                // LoadInterstitial() será chamado após SDK estar inicializado

                //Initialize Banner
                // Banners are automatically sized to 320�50 on phones and 728�90 on tablets
                // You may call the utility method MaxSdkUtils.isTablet() to help with view sizing adjustments
                // Banner será criado após SDK estar inicializado (no OnSdkInitialized)

                MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerAdLoadedEvent;
                MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerAdLoadFailedEvent;
                MaxSdkCallbacks.Banner.OnAdClickedEvent += OnBannerAdClickedEvent;
                MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaidEvent;
                MaxSdkCallbacks.Banner.OnAdExpandedEvent += OnBannerAdExpandedEvent;
                MaxSdkCallbacks.Banner.OnAdCollapsedEvent += OnBannerAdCollapsedEvent;

                //Initialize Rewarded
                MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoadedEvent;
                MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailedEvent;
                MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayedEvent;
                MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClickedEvent;
                MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaidEvent;
                MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHiddenEvent;
                MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplayEvent;
                MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;

            }
            catch (Exception ex)
            {
                Debug.LogError($"Erro ao inicializar MAX SDK: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
                initStatus?.Invoke(false);
            }
        }

        private void OnSdkInitialized(MaxSdkBase.SdkConfiguration sdkConfiguration)
        {
            Debug.Log("=== ✅ MAX SDK INICIALIZADO COM SUCESSO! ===");
            Debug.Log($"Test Mode: {sdkConfiguration.IsTestModeEnabled}");
            Debug.Log($"País: {sdkConfiguration.CountryCode}");
            Debug.Log($"Consent Flow Geography: {sdkConfiguration.ConsentFlowUserGeography}");
            Debug.Log($"Successfully Initialized: {sdkConfiguration.IsSuccessfullyInitialized}");
            
            try
            {
                // Criar banner após SDK estar inicializado
                Debug.Log($"Criando banner com Placement ID: {bannerPlacementId}");
                MaxSdk.CreateBanner(bannerPlacementId, MaxSdkBase.BannerPosition.BottomCenter);
                MaxSdk.SetBannerExtraParameter(bannerPlacementId, "adaptive_banner", "false");
                MaxSdk.SetBannerExtraParameter(bannerPlacementId, "banner_size", "320x50");
                MaxSdk.SetBannerExtraParameter(bannerPlacementId, "banner_placement", "top_center");
                MaxSdk.SetBannerBackgroundColor(bannerPlacementId, Color.clear);
                Debug.Log("✅ Banner criado e configurado");
                
                // Carregar anúncios após SDK estar inicializado
                Debug.Log("Carregando anúncios...");
                LoadRewardedAd();
                LoadInterstitial();
                Debug.Log("✅ Comandos de carregamento de anúncios enviados");
                
                // Notificar que a inicialização foi bem-sucedida
                Debug.Log("✅ Notificando sucesso da inicialização");
                pendingInitStatus?.Invoke(true);
                pendingInitStatus = null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Erro ao configurar anúncios após inicialização: {ex.Message}");
                pendingInitStatus?.Invoke(false);
                pendingInitStatus = null;
            }
        }



        #region BANNER
        public override void ShowBanner()
        {
            Debug.Log("=== MAX ShowBanner() CHAMADO ===");
            Debug.Log($"Banner Placement ID: {bannerPlacementId}");
            
            try
            {
                MaxSdk.ShowBanner(bannerPlacementId);
                Debug.Log("✅ Comando ShowBanner() enviado ao MAX SDK");
                BannerShowEvent?.Invoke(GetBanner());
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Erro ao exibir banner: {ex.Message}");
            }
        }


        public override void HideBanner()
        {
            MaxSdk.HideBanner(bannerPlacementId);
        }
        private Banner GetBanner()
        {
            Banner banner = new Banner();

            if (!isManualBannerRect)
            {
                // Banner pequeno centralizado no topo (320x50)
                float bannerWidth = 320f;
                float bannerHeight = 50f;
                float x = (Screen.width - bannerWidth) / 2f; // Centralizar horizontalmente
                float y = 0f; // Topo da tela
                
                banner.rect = new AdsRect(x, y, bannerWidth, bannerHeight);
            }
            else
                banner.rect = bannerRect;

            return banner;
        }


        private void OnBannerAdLoadedEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {
            BannerShowEvent?.Invoke(GetBanner());
        }

        private void OnBannerAdLoadFailedEvent(string arg1, MaxSdkBase.ErrorInfo arg2)
        {
            Debug.LogError($"❌ Banner falhou ao carregar: {arg1}");
            Debug.LogError($"Erro: {arg2.Message} - Código: {arg2.Code}");
        }

        private void OnBannerAdClickedEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {

        }

        private void OnBannerAdRevenuePaidEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {
            OnPaidImpression?.Invoke(new AdsRevenue(TypeAds.Banner, (long)arg2.Revenue));
        }

        private void OnBannerAdExpandedEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {
        }

        private void OnBannerAdCollapsedEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {

        }

        #endregion FIM_BANNER

        private void SetupCallbacks()
        {
            //Initialize Interstitial
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailedEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayedEvent;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClickedEvent;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHiddenEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialAdFailedToDisplayEvent;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialAdRevenuePaidEvent;

            //Initialize Banner
            MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerAdLoadedEvent;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerAdLoadFailedEvent;
            MaxSdkCallbacks.Banner.OnAdClickedEvent += OnBannerAdClickedEvent;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaidEvent;
            MaxSdkCallbacks.Banner.OnAdExpandedEvent += OnBannerAdExpandedEvent;
            MaxSdkCallbacks.Banner.OnAdCollapsedEvent += OnBannerAdCollapsedEvent;

            //Initialize Rewarded
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoadedEvent;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailedEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayedEvent;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClickedEvent;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaidEvent;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHiddenEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplayEvent;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;
        }

        #region INTERSTITIAL
        public override void ShowInterstitial()
        {
            Debug.Log("=== MAX ShowInterstitial() CHAMADO ===");
            Debug.Log($"Interstitial Placement ID: {interstitialPlacementId}");
            Debug.Log($"Interstitial pronto: {MaxSdk.IsInterstitialReady(interstitialPlacementId)}");
            
            if (MaxSdk.IsInterstitialReady(interstitialPlacementId))
            {
                Debug.Log("✅ Interstitial pronto - Exibindo");
                MaxSdk.ShowInterstitial(interstitialPlacementId);
                TimeToNexrInterstitial();
            }
            else
            {
                Debug.LogWarning("⚠️ Interstitial não está pronto - Carregando novo");
                LoadInterstitial();
            }
        }
        private void LoadInterstitial()
        {
            Debug.Log($"Carregando Interstitial com Placement ID: {interstitialPlacementId}");
            MaxSdk.LoadInterstitial(interstitialPlacementId);
        }

        private void TimeToNexrInterstitial()
        {
            retryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, retryAttempt));
            InvokeDelay((float)retryDelay);
        }

        private void OnInterstitialLoadedEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {
            // Interstitial ad is ready for you to show. MaxSdk.IsInterstitialReady(adUnitId) now returns 'true'
            // Reset retry attempt
            retryAttempt = 0;
        }

        private void OnInterstitialLoadFailedEvent(string arg1, MaxSdkBase.ErrorInfo arg2)
        {
            // Interstitial ad failed to load 
            // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds)

            TimeToNexrInterstitial();
        }

        public async void InvokeDelay(float delay)
        {
            int time = (int)delay * 1000;
            await System.Threading.Tasks.Task.Delay(time);
            LoadInterstitial();

        }

        private void OnInterstitialDisplayedEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {

        }
        private void OnInterstitialAdFailedToDisplayEvent(string arg1, MaxSdkBase.ErrorInfo arg2, MaxSdkBase.AdInfo arg3)
        {
            // Interstitial ad failed to display. AppLovin recommends that you load the next ad.
            LoadInterstitial();
        }

        private void OnInterstitialClickedEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {

        }


        private void OnInterstitialHiddenEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {
            // Interstitial ad is hidden. Pre-load the next ad.
            LoadInterstitial();
        }

        private void OnInterstitialAdRevenuePaidEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {
            OnPaidImpression?.Invoke(new AdsRevenue(TypeAds.Interstitial, (long)arg2.Revenue));
        }

        #endregion FIM_INTERSTITIAL

        #region REWARDED

        private Action<AdsResult> CallbackRewarded;

        public override void ShowRewarded(Action<AdsResult> CallbackRewarded)
        {
            Debug.Log("=== MAX ShowRewarded CHAMADO ===");
            Debug.Log($"Placement ID: {rewardedPlacementId}");
            Debug.Log($"SDK Key: {sdkKey}");
            Debug.Log($"Vídeo pronto: {MaxSdk.IsRewardedAdReady(rewardedPlacementId)}");
            
            if (MaxSdk.IsRewardedAdReady(rewardedPlacementId))
            {
                Debug.Log("✅ MAX Rewarded Ad pronto - Exibindo vídeo");
                MaxSdk.ShowRewardedAd(rewardedPlacementId);
                this.CallbackRewarded = CallbackRewarded;
                LoadRewardedAd();
            }
            else
            {
                Debug.LogWarning("❌ MAX Rewarded Ad não está pronto - Carregando novo");
                Debug.LogWarning("Verifique se os IDs estão corretos no AppLovin Dashboard");
                LoadRewardedAd();
                CallbackRewarded?.Invoke(new AdsResult(AdsStatus.Unloaded));
            }
        }

        private void LoadRewardedAd()
        {
            Debug.Log($"Carregando Rewarded Ad com Placement ID: {rewardedPlacementId}");
            MaxSdk.LoadRewardedAd(rewardedPlacementId);
        }

        private void OnRewardedAdLoadedEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {
            Debug.Log($"MAX Rewarded Ad Carregado: {arg1}");
            Debug.Log($"Ad Info: {arg2.NetworkName} - Revenue: {arg2.Revenue}");
        }

        private void OnRewardedAdLoadFailedEvent(string arg1, MaxSdkBase.ErrorInfo arg2)
        {
            Debug.LogError($"MAX Rewarded Ad Falhou ao Carregar: {arg1}");
            Debug.LogError($"Erro: {arg2.Message} - Código: {arg2.Code}");
            LoadRewardedAd();
        }

        private void OnRewardedAdDisplayedEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {

        }

        private void OnRewardedAdClickedEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {

        }

        private void OnRewardedAdRevenuePaidEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {
            OnPaidImpression?.Invoke(new AdsRevenue(TypeAds.RewardedVideo, (long)arg2.Revenue));
        }

        private void OnRewardedAdHiddenEvent(string arg1, MaxSdkBase.AdInfo arg2)
        {
            Debug.Log("MAX Rewarded Ad Fechado - OnRewardedAdHiddenEvent");
            // Se o callback ainda não foi chamado (usuário não recebeu recompensa), chama como cancelado
            if (CallbackRewarded != null)
            {
                Debug.Log("MAX Rewarded Ad - Chamando callback como Canceled (usuário fechou sem receber recompensa)");
                CallbackRewarded?.Invoke(new AdsResult(AdsStatus.Canceled));
                CallbackRewarded = null; // Limpa o callback para evitar chamadas duplicadas
            }
        }

        private void OnRewardedAdFailedToDisplayEvent(string arg1, MaxSdkBase.ErrorInfo arg2, MaxSdkBase.AdInfo arg3)
        {
            Debug.Log("FailedToDisplay");
            LoadRewardedAd();
        }

        private void OnRewardedAdReceivedRewardEvent(string arg1, MaxSdkBase.Reward arg2, MaxSdkBase.AdInfo arg3)
        {
            Debug.Log("MAX Rewarded Ad - Usuário recebeu recompensa!");
            LoadRewardedAd();
            CallbackRewarded?.Invoke(new AdsResult(AdsStatus.Success));
            CallbackRewarded = null; // Limpa o callback para evitar chamadas duplicadas
        }

        public override void ShowRewardedInterstitial(Action<AdsResult> CallbackRewarded)
        {
            //implementar interstitial premiado
        }

        #endregion FIM_REWARDED
    }
}
