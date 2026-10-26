using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    private enum AdsState
    {
        Uninitialized,
        Initializing,
        Initialized
    }

    // ID OFICIAL DE PRUEBA de Google para interstitial en Android.
    // Se usa SOLO en compilaciones Development Build.
    private const string AndroidTestInterstitialId =
        "ca-app-pub-3940256099942544/1033173712";

    private AdsState state = AdsState.Uninitialized;
    private InterstitialAd interstitialAd;
    private bool isInterstitialLoading;

    public bool IsInitialized => state == AdsState.Initialized;

    public bool IsInterstitialReady()
    {
        return interstitialAd != null && interstitialAd.CanShowAd();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void InitializeOnce()
    {
        if (state != AdsState.Uninitialized)
            return;

        if (PrivacyManager.Instance == null ||
            !PrivacyManager.Instance.CanRequestAds)
        {
            Debug.Log("[ADS] Esperando autorizacion de UMP.");
            return;
        }

        state = AdsState.Initializing;
        Debug.Log("[ADS] Inicializando Google Mobile Ads...");

        MobileAds.Initialize(initStatus =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (this == null || Instance != this)
                    return;

                if (initStatus == null)
                {
                    state = AdsState.Uninitialized;
                    Debug.LogWarning(
                        "[ADS] No se pudo inicializar el SDK."
                    );
                    return;
                }

                state = AdsState.Initialized;
                Debug.Log(
                    "[ADS] Google Mobile Ads inicializado correctamente."
                );

                // Solo precarga un anuncio. Aun no se muestra publicidad.
                LoadTestInterstitial();
            });
        });
    }

    private void LoadTestInterstitial()
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (!IsInitialized || isInterstitialLoading || interstitialAd != null)
            return;

        if (PrivacyManager.Instance == null ||
            !PrivacyManager.Instance.CanRequestAds)
        {
            Debug.Log("[ADS] UMP no permite solicitar anuncios.");
            return;
        }

        isInterstitialLoading = true;
        Debug.Log("[ADS] Iniciando carga de interstitial de prueba...");

        InterstitialAd.Load(
            AndroidTestInterstitialId,
            new AdRequest(),
            (InterstitialAd ad, LoadAdError error) =>
            {
                // Los callbacks del SDK pueden llegar desde otro hilo.
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (this == null || Instance != this)
                    {
                        ad?.Destroy();
                        return;
                    }

                    isInterstitialLoading = false;

                    if (error != null || ad == null)
                    {
                        ad?.Destroy();
                        Debug.LogWarning(
                            "[ADS] No se pudo cargar interstitial de prueba: " +
                            (error != null ? error.ToString() : "Anuncio nulo")
                        );
                        return;
                    }

                    interstitialAd = ad;
                    Debug.Log(
                        "[ADS] Interstitial de prueba cargado. Listo: " +
                        IsInterstitialReady()
                    );
                });
            }
        );
#else
        Debug.Log(
            "[ADS] Carga de prueba omitida: requiere Android Development Build."
        );
#endif
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }

        Instance = null;
    }
}
