using System;
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

    // ID oficial de prueba de Google para intersticiales Android.
    // Solo se utiliza en compilaciones Android Development Build.
    private const string AndroidTestInterstitialId =
        "ca-app-pub-3940256099942544/1033173712";

    private AdsState state = AdsState.Uninitialized;
    private InterstitialAd interstitialAd;
    private bool isInterstitialLoading;
    private bool isInterstitialShowing;

    public bool IsInitialized => state == AdsState.Initialized;

    public bool IsInterstitialReady()
    {
        return !isInterstitialShowing &&
               interstitialAd != null &&
               interstitialAd.CanShowAd();
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
                    Debug.LogWarning("[ADS] No se pudo inicializar el SDK.");
                    return;
                }

                state = AdsState.Initialized;
                Debug.Log("[ADS] Google Mobile Ads inicializado correctamente.");
                LoadTestInterstitial();
            });
        });
    }

    private void LoadTestInterstitial()
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (!IsInitialized || isInterstitialLoading ||
            isInterstitialShowing || interstitialAd != null)
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
                // Los callbacks de AdMob pueden llegar desde otro hilo.
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
                    RegisterInterstitialEvents(ad);

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

    private void RegisterInterstitialEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (this != null && Instance == this)
                    Debug.Log("[ADS] Interstitial abierto.");
            });
        };

        ad.OnAdFullScreenContentClosed += () =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
                FinishInterstitial(ad, null));
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
                FinishInterstitial(ad, error));
        };
    }

    // Solo para conectar un boton temporal de pruebas.
    public void ShowTestInterstitial()
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (PrivacyManager.Instance == null ||
            !PrivacyManager.Instance.CanRequestAds)
        {
            Debug.LogWarning("[ADS] UMP no permite mostrar anuncios.");
            return;
        }

        if (!IsInterstitialReady())
        {
            Debug.Log("[ADS] Interstitial aun no disponible.");
            return;
        }

        isInterstitialShowing = true;
        Debug.Log("[ADS] Mostrando interstitial de prueba...");

        try
        {
            interstitialAd.Show();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[ADS] Error al mostrar interstitial: " + exception.Message
            );
            FinishInterstitial(interstitialAd, null);
        }
#else
        Debug.Log(
            "[ADS] Solo se puede mostrar el test en una Development Build Android."
        );
#endif
    }

    private void FinishInterstitial(InterstitialAd ad, AdError error)
    {
        if (this == null || Instance != this)
        {
            ad?.Destroy();
            return;
        }

        if (ad == null || interstitialAd != ad || !isInterstitialShowing)
            return;

        if (error != null)
            Debug.LogWarning("[ADS] Error al abrir interstitial: " + error);
        else
            Debug.Log("[ADS] Interstitial cerrado.");

        isInterstitialShowing = false;
        interstitialAd = null;
        ad.Destroy();

        // Cada interstitial solo puede mostrarse una vez.
        LoadTestInterstitial();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        interstitialAd?.Destroy();
        interstitialAd = null;
        Instance = null;
    }
}
