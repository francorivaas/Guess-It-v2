using System.Collections;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

public class MainMenuBanner : MonoBehaviour
{
    private const string TestBannerId =
        "ca-app-pub-3940256099942544/9214589741";

    // Unidad banner_main_menu de Guess It! (AdMob Android).
    private const string ProductionBannerId =
        "ca-app-pub-4382732485118871/8718923184";

    private static string BannerUnitId
    {
        get
        {
#if DEVELOPMENT_BUILD
            return TestBannerId;
#else
            return ProductionBannerId;
#endif
        }
    }

    private BannerView bannerView;
    private Coroutine loadRoutine;
    private PrivacyManager subscribedPrivacy;

    private IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Espera a que PrivacyManager este disponible, sin consultar UMP cada frame.
        while (PrivacyManager.Instance == null)
            yield return null;

        subscribedPrivacy = PrivacyManager.Instance;
        subscribedPrivacy.ConsentStatusUpdated += RefreshBanner;
        RefreshBanner();
#else
        Debug.Log("[ADS] Banner movil deshabilitado en el Editor.");
        yield break;
#endif
    }

    private void RefreshBanner()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isActiveAndEnabled)
            return;

        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
            loadRoutine = null;
        }

        DestroyBanner();
        loadRoutine = StartCoroutine(WaitAndLoadBanner());
#endif
    }

    private IEnumerator WaitAndLoadBanner()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // No deja una espera infinita cuando UMP no permite anuncios.
        while (AdsManager.Instance == null || !AdsManager.Instance.IsInitialized)
        {
            if (subscribedPrivacy == null ||
                (subscribedPrivacy.ConsentFlowFinished &&
                 !subscribedPrivacy.CanRequestAds))
            {
                loadRoutine = null;
                yield break;
            }
            yield return null;
        }

        if (subscribedPrivacy == null || !subscribedPrivacy.CanRequestAds)
        {
            Debug.Log("[ADS] Banner omitido: UMP no permite anuncios.");
            loadRoutine = null;
            yield break;
        }

        int width = MobileAds.Utils.GetDeviceSafeWidth();
        AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(width);
        BannerView currentBanner = new BannerView(BannerUnitId, size, AdPosition.Bottom);
        bannerView = currentBanner;

        currentBanner.OnBannerAdLoaded += () =>
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (this != null && bannerView == currentBanner)
                    Debug.Log("[ADS] Banner del menu cargado correctamente.");
            });

        currentBanner.OnBannerAdLoadFailed += error =>
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (this != null && bannerView == currentBanner)
                    Debug.LogWarning("[ADS] Error al cargar banner: " + error);
            });

        currentBanner.LoadAd(new AdRequest());
        loadRoutine = null;
#else
        yield break;
#endif
    }

    private void DestroyBanner()
    {
        if (bannerView == null)
            return;

        bannerView.Destroy();
        bannerView = null;
    }

    private void OnDestroy()
    {
        if (subscribedPrivacy != null)
            subscribedPrivacy.ConsentStatusUpdated -= RefreshBanner;

        if (loadRoutine != null)
            StopCoroutine(loadRoutine);

        DestroyBanner();
    }
}
