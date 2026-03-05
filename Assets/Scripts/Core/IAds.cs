using System;
using static Ads.AdsAPI;

namespace Ads
{

    public enum AdsStatus
    {
        Success,
        Failed,
        Canceled,
        Unloaded
    }

    public interface IAds {

        public event AdsEvent<Banner> BannerShowEvent;

        public event AdsEvent<Banner> BannerCloseEvent;

        public event AdsEvent<AdsRevenue> OnPaidImpression;

        public bool IsLoadRewarded { set; get; }

        public void Initialize(Action<bool> initStatus);

        public void ShowBanner();

        public void HideBanner();

        public void ShowInterstitial();

        public void ShowRewarded(Action<AdsResult> CallbackRewarded);
        public void ShowRewardedInterstitial(Action<AdsResult> CallbackRewarded);

    }

    public struct AdsResult
    {
        public AdsStatus adsStatus;

        public AdsResult(AdsStatus adsStatus)
        {
            this.adsStatus = adsStatus;
        }
    }
}
