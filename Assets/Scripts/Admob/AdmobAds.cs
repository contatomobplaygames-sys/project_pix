using System;
using UnityEngine;
using GoogleMobileAds.Api;

namespace Ads
{
    [CreateAssetMenu(fileName = "AdmobAds", menuName = "Ads/Admob Ads", order = 1)]
    public class AdmobAds : AdsObject
    {
        [SerializeField]
        private bool isShowDebug;
        [Header("Ads IDs")]
        [Header("Settings")]
        public string BannerId = "ca-app-pub-3940256099942544/6300978111";
        public string InterstitialId = "ca-app-pub-3940256099942544/1033173712";
        public string RewardedId = "ca-app-pub-3940256099942544/5224354917";
        public string RewardedInterstitialId = "ca-app-pub-3940256099942544/5354046379";

        [Header("Banner Block")]
        public bool isManualBannerRect;
        public AdsRect bannerRect;

        private BannerView bannerView;

        private InterstitialAd interstitialAd;
        private RewardedInterstitialAd rewardedInterstitialAd;

        private GoogleMobileAds.Api.RewardedAd rewardedAd;
        public override bool IsLoadRewarded { get => rewardedAd.CanShowAd(); set => rewardedAd.CanShowAd(); }

        private Action<AdsResult> CallbackRewarded;

        public override event AdsAPI.AdsEvent<Banner> BannerShowEvent;

        public override event AdsAPI.AdsEvent<Banner> BannerCloseEvent;
        public override event AdsAPI.AdsEvent<AdsRevenue> OnPaidImpression;
        public event Action OnInterstitialAdImpressionRecorded;
        public event Action OnRewardedAdImpressionRecorded;

        void Start()
        {
            ShowBanner();
        }

        public override void Initialize(Action<bool> initStatus)
        {
            MobileAds.Initialize(iniStatusAdmob =>
            {
                try
                {

                    foreach (var ini in iniStatusAdmob.getAdapterStatusMap())
                    {
                        Debug.LogFormat("Name:{0}\n" +
                            "Description:{2}\n" +
                            "Init State:{1}\n" +
                            "Latency:{3}", ini.Key, ini.Value.InitializationState, ini.Value.Description, ini.Value.Latency);
                    }

                    initStatus?.Invoke(true);
                    MobileAdsIsInitialized();
                }
                catch
                {
                    initStatus?.Invoke(false);
                }
            });


        }

        private void MobileAdsIsInitialized()
        {
            Debug.Log("=== MobileAdsIsInitialized() CHAMADO ===");
            
            //Initialize Banner
            AdSize adaptiveSize = AdSize.Banner; // Usar tamanho fixo menor (320x50)

            Debug.Log($"AdaptiveSize: {adaptiveSize.Width}x{adaptiveSize.Height}");

            if (bannerView != null) 
            {
                Debug.Log("Destruindo bannerView existente...");
                DestroyAd();
            }

            Debug.Log($"Criando novo BannerView com ID: {BannerId}");
            bannerView = new BannerView(BannerId, adaptiveSize, AdPosition.Bottom);
            Debug.Log($"BannerView criado: {(bannerView != null ? "SUCCESS" : "FAILED")}");

            LoadBanner();
            bannerView.OnBannerAdLoaded += BannerView_OnAdLoaded;
            bannerView.OnAdFullScreenContentClosed += BannerView_OnAdClosed;
            bannerView.OnAdPaid += BannerView_OnPaidEvent;

            //Initialize Rewarded
            LoadRewardedAd();

            //Initialize Interstitial
            LoadInterstitialAd();
            //Initialize Rewarded Interstitial
            LoadRewardedInterstitialAd();
        }

        #region Banner
        public override void ShowBanner()
        {
            Debug.Log("=== AdmobAds.ShowBanner() CHAMADO ===");
            Debug.Log($"bannerView: {(bannerView != null ? "EXISTS" : "NULL")}");
            
            if (bannerView == null)
            {
                Debug.LogError("❌ bannerView é NULL! Carregando banner primeiro...");
                LoadBanner();
                return;
            }
            
            Debug.Log("✅ Chamando bannerView.Show()...");
            bannerView.Show();
            BannerShowEvent?.Invoke(GetBanner());
        }

        public override void HideBanner()
        {
            bannerView.Hide();
            BannerCloseEvent?.Invoke(GetBanner());
        }

        private void LoadBanner()
        {
            Debug.Log("=== LoadBanner() CHAMADO ===");
            Debug.Log($"BannerId: {BannerId}");
            
            if (bannerView == null)
            {
                Debug.LogError("❌ bannerView é NULL! Não é possível carregar banner.");
                return;
            }
            
            Debug.Log("✅ Criando AdRequest e carregando banner...");
            AdRequest request = new AdRequest();
            bannerView.LoadAd(request);
        }

        private void BannerView_OnAdClosed()
        {
            Debug.Log("=== BannerView_OnAdClosed() ===");
            BannerCloseEvent?.Invoke(GetBanner());
        }

        private void BannerView_OnAdLoaded()
        {
            Debug.Log("=== BannerView_OnAdLoaded() ===");
            Debug.Log("✅ Banner carregado com sucesso! Invocando BannerShowEvent...");
            BannerShowEvent?.Invoke(GetBanner());
        }

        private Banner GetBanner()
        {
            Banner banner = new Banner();

            if (!isManualBannerRect)
                banner.rect = new AdsRect(0, 0, bannerView.GetWidthInPixels(), bannerView.GetHeightInPixels());
            else
                banner.rect = bannerRect;

            return banner;
        }
        private void BannerView_OnPaidEvent(AdValue obj)
        {
            OnPaidImpression?.Invoke(new AdsRevenue(TypeAds.Banner, obj.Value));
        }

        public void DestroyAd()
        {
            if (bannerView != null)
            {
                if (isShowDebug)
                    Debug.Log("Destroying banner ad.");
                bannerView.Destroy();
                bannerView = null;
            }
        }

        #endregion

        #region Interstitial 

        public void LoadInterstitialAd()
        {
            // Clean up the old ad before loading a new one.
            if (interstitialAd != null)
            {
                interstitialAd.Destroy();
                interstitialAd = null;
            }

            if (isShowDebug)
                Debug.Log("Loading the interstitial ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest();

            // send the request to load the ad.
            InterstitialAd.Load(InterstitialId, adRequest,
                (InterstitialAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        Debug.LogError("interstitial ad failed to load an ad " +
                                        "with error : " + error);
                        return;
                    }

                    if (isShowDebug)
                        Debug.Log("Interstitial ad loaded with response : "
                                + ad.GetResponseInfo());

                    interstitialAd = ad;
                    RegisterInterstitialEventHandlers();
                });
        }

        public override void ShowInterstitial()
        {
            if (interstitialAd != null && !interstitialAd.CanShowAd())
            {
                LoadInterstitialAd();
            }
            else
            {
                interstitialAd?.Show();
            }
        }

        private void Interstitial_OnAdFailedToShow(AdError error)
        {
            Debug.LogError("Interstitial ad failed to open full screen content " +
           "with error : " + error);

            // Reload the ad so that we can show another as soon as possible.
            LoadInterstitialAd();
        }

        private void Interstitial_OnAdClosed()
        {
            LoadInterstitialAd();
        }

        private void Interstitial_OnPaidEvent(AdValue adValue)
        {
            if (isShowDebug)
                Debug.Log(String.Format("<color=#{2}>Interstitial ad paid {0} {1}.</color>",
                    adValue.Value,
                    adValue.CurrencyCode, Color.green));

            OnPaidImpression?.Invoke(new AdsRevenue(TypeAds.Interstitial, adValue.Value));
        }

        private void InterstitialAd_OnAdImpressionRecorded()
        {
            if (isShowDebug)
                Debug.Log("Interstitial ad recorded an impression.");

            OnInterstitialAdImpressionRecorded?.Invoke();
        }

        private void RegisterInterstitialEventHandlers()
        {
            // Raised when the ad is estimated to have earned money.
            interstitialAd.OnAdPaid += Interstitial_OnPaidEvent;

            // Raised when an impression is recorded for an ad.
            interstitialAd.OnAdImpressionRecorded += InterstitialAd_OnAdImpressionRecorded;
            interstitialAd.OnAdFullScreenContentClosed += Interstitial_OnAdClosed;
            interstitialAd.OnAdFullScreenContentFailed += Interstitial_OnAdFailedToShow;
        }

        #endregion

        #region Rewarded 

        private void RegisterRewardedEventHandlers()
        {
            if (rewardedAd == null)
            {
                Debug.LogError("Rewarded ad is not loaded.");
                return;
            }

            // Raised when the ad is estimated to have earned money.
            rewardedAd.OnAdPaid += RewardedAd_OnPaidEvent;

            // Raised when an impression is recorded for an ad.
            rewardedAd.OnAdImpressionRecorded += RewardedAd_OnAdImpressionRecorded;

            // Raised when the ad closed full screen content.
            rewardedAd.OnAdFullScreenContentClosed += RewardedAd_OnAdClosed;
            // Raised when the ad failed to open full screen content.
            rewardedAd.OnAdFullScreenContentFailed += RewardedAd_OnAdFailedToShow;

        }

        private void RewardedAd_OnAdImpressionRecorded()
        {
            if (isShowDebug)
                Debug.Log("Rewarded ad recorded an impression.");
        }

        public void LoadRewardedAd()
        {
            // Clean up the old ad before loading a new one.
            if (rewardedAd != null)
            {
                rewardedAd.Destroy();
                rewardedAd = null;
            }

            Debug.Log("Loading the rewarded ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest();

            // send the request to load the ad.
            RewardedAd.Load(RewardedId, adRequest,
                (RewardedAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        Debug.LogError("Rewarded ad failed to load an ad " +
                                        "with error : " + error);
                        return;
                    }

                    Debug.Log("Rewarded ad loaded with response : "
                                + ad.GetResponseInfo());

                    rewardedAd = ad;
                    RegisterRewardedEventHandlers(); // Move this line here
                });
        }

        public override void ShowRewarded(Action<AdsResult> CallbackRewarded)
        {
            const string rewardMsg = "Rewarded ad rewarded the user. Type: {0}, amount: {1}.";

            if (rewardedAd != null && rewardedAd.CanShowAd())
            {
                rewardedAd.Show((Reward reward) =>
                {
                    // TODO: Reward the user.
                    if (isShowDebug)
                        Debug.Log(String.Format(rewardMsg, reward.Type, reward.Amount));
                    CallbackRewarded?.Invoke(new AdsResult(AdsStatus.Success));
                });
            }
            else
            {
                LoadRewardedAd();
                CallbackRewarded?.Invoke(new AdsResult(AdsStatus.Unloaded));
            }
        }

        private void RewardedAd_OnAdFailedToShow(AdError error)
        {
            Debug.LogError("Rewarded ad failed to open full screen content " +
               "with error : " + error);
            LoadRewardedAd();
            CallbackRewarded?.Invoke(new AdsResult(AdsStatus.Failed));
        }

        private void RewardedAd_OnAdClosed()
        {
            LoadRewardedAd();
        }

        private void RewardedAd_OnPaidEvent(AdValue adValue)
        {
            if (isShowDebug)
                Debug.Log(String.Format("Rewarded ad paid {0} {1}.",
                    adValue.Value,
                    adValue.CurrencyCode));

            OnPaidImpression?.Invoke(new AdsRevenue(TypeAds.RewardedVideo, adValue.Value));
        }

        #endregion

        #region RewardedInterstitial
        /// <summary>
        /// Loads the rewarded interstitial ad.
        /// </summary>
        public void LoadRewardedInterstitialAd()
        {
            // Clean up the old ad before loading a new one.
            if (rewardedInterstitialAd != null)
            {
                rewardedInterstitialAd.Destroy();
                rewardedInterstitialAd = null;
            }
            if (isShowDebug)
                Debug.Log("Loading the rewarded interstitial ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest();
            adRequest.Keywords.Add("unity-admob-sample");

            // send the request to load the ad.
            RewardedInterstitialAd.Load(RewardedInterstitialId, adRequest,
                (RewardedInterstitialAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        Debug.LogError("rewarded interstitial ad failed to load an ad " +
                                       "with error : " + error);
                        return;
                    }
                    if (isShowDebug)
                        Debug.Log("Rewarded interstitial ad loaded with response : "
                              + ad.GetResponseInfo());

                    rewardedInterstitialAd = ad;
                    RegisterRewardedInterstitialEventHandlers();
                    RegisterRewardedInterstitialReloadHandler();
                });
        }

        public override void ShowRewardedInterstitial(Action<AdsResult> CallbackRewarded)
        {
            const string rewardMsg =
                "Rewarded interstitial ad rewarded the user. Type: {0}, amount: {1}.";

            if (rewardedInterstitialAd != null && rewardedInterstitialAd.CanShowAd())
            {
                rewardedInterstitialAd.Show((Reward reward) =>
                {
                    if(isShowDebug)
                        Debug.Log(String.Format(rewardMsg, reward.Type, reward.Amount));

                    CallbackRewarded?.Invoke(new AdsResult(AdsStatus.Success));
                });
            }
            else
            {
                LoadRewardedInterstitialAd();
                CallbackRewarded?.Invoke(new AdsResult(AdsStatus.Unloaded));
            }
        }

        private void RegisterRewardedInterstitialEventHandlers()
        {
            // Raised when the ad is estimated to have earned money.
            rewardedInterstitialAd.OnAdPaid += (AdValue adValue) =>
            {
                if (isShowDebug)
                    Debug.Log(String.Format("Rewarded interstitial ad paid {0} {1}.",
                    adValue.Value,
                    adValue.CurrencyCode));
            };
            // Raised when an impression is recorded for an ad.
            rewardedInterstitialAd.OnAdImpressionRecorded += () =>
            {
                if (isShowDebug)
                    Debug.Log("Rewarded interstitial ad recorded an impression.");
            };
            // Raised when a click is recorded for an ad.
            rewardedInterstitialAd.OnAdClicked += () =>
            {
                if (isShowDebug)
                    Debug.Log("Rewarded interstitial ad was clicked.");
            };
            // Raised when an ad opened full screen content.
            rewardedInterstitialAd.OnAdFullScreenContentOpened += () =>
            {
                if (isShowDebug)
                    Debug.Log("Rewarded interstitial ad full screen content opened.");
            };
            // Raised when the ad closed full screen content.
            rewardedInterstitialAd.OnAdFullScreenContentClosed += () =>
            {
                if (isShowDebug)
                    Debug.Log("Rewarded interstitial ad full screen content closed.");
            };
            // Raised when the ad failed to open full screen content.
            rewardedInterstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Debug.LogError("Rewarded interstitial ad failed to open full screen content " +
                               "with error : " + error);
            };
        }

        private void RegisterRewardedInterstitialReloadHandler()
        {
            // Raised when the ad closed full screen content.
            rewardedInterstitialAd.OnAdFullScreenContentClosed += () =>
            {
                if (isShowDebug)
                    Debug.Log("Rewarded interstitial ad full screen content closed.");

                // Reload the ad so that we can show another as soon as possible.
                LoadRewardedInterstitialAd();
            };
            // Raised when the ad failed to open full screen content.
            rewardedInterstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
            {
                Debug.LogError("Rewarded interstitial ad failed to open full screen content " +
                               "with error : " + error);

                // Reload the ad so that we can show another as soon as possible.
                LoadRewardedInterstitialAd();
            };
        }

        #endregion RewardedInterstitial

    }
}
