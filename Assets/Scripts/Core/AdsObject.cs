using System;
using UnityEngine;
using static Ads.AdsAPI;

namespace Ads
{
    public abstract class AdsObject : ScriptableObject, IAds
    {

        public abstract event AdsEvent<Banner> BannerShowEvent;

        public abstract event AdsEvent<Banner> BannerCloseEvent;

        public abstract event AdsEvent<AdsRevenue> OnPaidImpression;

        public abstract bool IsLoadRewarded { get; set; }

        public abstract void ShowBanner();

        public abstract void ShowInterstitial();

        public abstract void ShowRewarded(Action<AdsResult> CallbackRewarded);

        public abstract void HideBanner();

        public abstract void Initialize(Action<bool> initStatus);

        public abstract void ShowRewardedInterstitial(Action<AdsResult> CallbackRewarded);

    }
}
