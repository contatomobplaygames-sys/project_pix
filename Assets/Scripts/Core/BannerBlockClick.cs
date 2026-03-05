using UnityEngine;


namespace Ads {

    using UnityEngine.UI;

    public static class BannerBlockClick 
    {
        private static Canvas canvas;
        private static Image image;
        private static bool isEnabled = true; // Flag para habilitar/desabilitar o sistema

        static BannerBlockClick()
        {
            CreateCanvas();
        }

        public static void InitializeBannerBlock()
        {
            if (isEnabled)
            {
                AdsAPI.BannerShowEvent += BannerShowEvent;
                AdsAPI.BannerCloseEvent += BannerCloseEvent;
            }
        }

        /// <summary>
        /// Habilita ou desabilita o sistema de bloqueio de banner
        /// </summary>
        public static void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            if (!enabled && image != null)
            {
                image.gameObject.SetActive(false);
            }
        }

        private static void BannerShowEvent(Banner banner)
        {
            // Só exibe a barra de bloqueio se o banner estiver na parte inferior
            if (banner.rect.y < Screen.height * 0.5f) // Se o banner estiver na metade inferior da tela
            {
                image.rectTransform.sizeDelta = new Vector2(10, banner.rect.height + 10);
                image.gameObject.SetActive(true);
            }
            else
            {
                // Se o banner estiver no topo, não precisa da barra de bloqueio
                image.gameObject.SetActive(false);
            }
        }

        private static void BannerCloseEvent(Banner banner)
        {
            image.gameObject.SetActive(false);
        }

        private static void CreateCanvas()
        {

            var canvasGameobject = new GameObject("CanvasBlock");

            canvas = canvasGameobject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGameobject.AddComponent<GraphicRaycaster>();

            var scaler = canvasGameobject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvas.sortingLayerName = "UI";
            canvas.sortingOrder = 100;

            var imageGameObject = new GameObject("BannerBlockImage");
            image = imageGameObject.AddComponent<Image>();
            imageGameObject.transform.SetParent(canvasGameobject.transform);

            image.rectTransform.pivot = new Vector2(0.5f, 1);
            image.rectTransform.anchorMin = new Vector2(0, 1);
            image.rectTransform.anchorMax = new Vector2(1, 1);
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.color = Color.clear; // Mudado de Color.black para Color.clear
            image.gameObject.SetActive(false);

            Object.DontDestroyOnLoad(canvasGameobject);

        }
    }
}
