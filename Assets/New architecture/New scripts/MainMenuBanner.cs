using System.Collections;
using GoogleMobileAds.Api;
using UnityEngine;

public class MainMenuBanner : MonoBehaviour
{
    // Unidad oficial de Google: banner adaptable de prueba (Android).
    private const string TestBannerId =
        "ca-app-pub-3940256099942544/9214589741";

    private BannerView bannerView;

    private IEnumerator Start()
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
        // AdsManager inicializa el SDK una vez, despues de UMP.
        while (AdsManager.Instance == null || !AdsManager.Instance.IsInitialized)
        {
            yield return null;
        }

        if (PrivacyManager.Instance == null ||
            !PrivacyManager.Instance.CanRequestAds)
        {
            Debug.Log("[ADS] Banner omitido: UMP no permite solicitar anuncios.");
            yield break;
        }

        int safeWidth = MobileAds.Utils.GetDeviceSafeWidth();
        AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(safeWidth);

        bannerView = new BannerView(TestBannerId, size, AdPosition.Bottom);

        bannerView.OnBannerAdLoaded += () =>
            Debug.Log("[ADS] Banner del menu cargado correctamente.");

        bannerView.OnBannerAdLoadFailed += error =>
            Debug.LogWarning("[ADS] Error al cargar banner: " + error);

        bannerView.LoadAd(new AdRequest());
#else
        Debug.Log("[ADS] Banner de prueba disponible solo en Android Development Build.");
        yield break;
#endif
    }

    private void OnDestroy()
    {
        // La escena MainMenu controla la vida del anuncio: al salir, se destruye.
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }
    }
}
