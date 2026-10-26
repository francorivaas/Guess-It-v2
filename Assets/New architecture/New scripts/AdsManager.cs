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

    [Header("Frecuencia de interstitials")]
    [SerializeField, Min(1)] private int gamesPerInterstitial = 3;
    [SerializeField, Min(0f)] private float minimumSecondsBetweenAds = 120f;
    [SerializeField, Min(0f)] private float minimumSecondsAfterResume = 15f;

    private const string FinishedGamesKey = "GuessIt_FinishedGamesSinceInterstitial";
    private const string LastInterstitialUtcKey = "GuessIt_LastInterstitialUtc";

    private int finishedGamesSinceInterstitial;
    private long lastInterstitialUtc;
    private float lastResumeRealtime;
    private Action pendingAfterInterstitial;
    private bool currentInterstitialIsFromGameOver;

    public bool IsInitialized => state == AdsState.Initialized;

    public bool IsInterstitialReady()
    {
        return !isInterstitialShowing &&
               interstitialAd != null &&
               interstitialAd.CanShowAd();
    }

    // Se invoca UNA vez al quedarse sin vidas. Persiste entre escenas y sesiones.
    public void RegisterFinishedGame()
    {
        finishedGamesSinceInterstitial = Mathf.Min(
            finishedGamesSinceInterstitial + 1, 1000000);

        PlayerPrefs.SetInt(FinishedGamesKey, finishedGamesSinceInterstitial);
        PlayerPrefs.Save();

        Debug.Log($"[ADS] Partidas terminadas desde el último anuncio: " +
                  $"{finishedGamesSinceInterstitial}/{gamesPerInterstitial}");
    }

    // Devuelve true solo cuando AdsManager se hace cargo de la transición.
    // Si devuelve false, GameManager debe continuar inmediatamente.
    public bool TryShowGameOverInterstitial(Action onFinished)
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (onFinished == null)
            return false;

        if (finishedGamesSinceInterstitial < gamesPerInterstitial)
        {
            Debug.Log("[ADS] Frecuencia no alcanzada. Continuando sin anuncio.");
            return false;
        }

        double elapsedSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - lastInterstitialUtc;
        if (lastInterstitialUtc > 0 && elapsedSeconds < minimumSecondsBetweenAds)
        {
            Debug.Log("[ADS] Cooldown activo. Continuando sin anuncio.");
            return false;
        }

        if (Time.realtimeSinceStartup - lastResumeRealtime < minimumSecondsAfterResume)
        {
            Debug.Log("[ADS] Juego recién abierto o reanudado. Sin anuncio.");
            return false;
        }

        return TryShowLoadedInterstitial(onFinished, true);
#else
        return false; // Nunca usamos el ID de prueba en una build de producción.
#endif
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
            lastResumeRealtime = Time.realtimeSinceStartup;
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

        finishedGamesSinceInterstitial = Mathf.Max(
            0, PlayerPrefs.GetInt(FinishedGamesKey, 0));
        long.TryParse(PlayerPrefs.GetString(LastInterstitialUtcKey, "0"),
            out lastInterstitialUtc);
        lastResumeRealtime = Time.realtimeSinceStartup;
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
                if (this == null || Instance != this || interstitialAd != ad ||
                    !isInterstitialShowing)
                    return;

                Debug.Log("[ADS] Interstitial abierto.");
                lastInterstitialUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                PlayerPrefs.SetString(LastInterstitialUtcKey,
                    lastInterstitialUtc.ToString());

                if (currentInterstitialIsFromGameOver)
                {
                    finishedGamesSinceInterstitial = 0;
                    PlayerPrefs.SetInt(FinishedGamesKey, 0);
                }

                PlayerPrefs.Save();
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

    // Permite conservar el botón de pruebas del menú durante el desarrollo.
    // El botón de pruebas ignora la frecuencia; el gameplay NO la ignora.
    public void ShowTestInterstitial()
    {
        if (!TryShowLoadedInterstitial(null, false))
        {
            Debug.Log("[ADS] Interstitial aún no disponible para prueba.");
        }
    }

    private bool TryShowLoadedInterstitial(Action onFinished, bool fromGameOver)
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (!IsInitialized || PrivacyManager.Instance == null ||
            !PrivacyManager.Instance.CanRequestAds || !IsInterstitialReady())
            return false;

        pendingAfterInterstitial = onFinished;
        currentInterstitialIsFromGameOver = fromGameOver;
        isInterstitialShowing = true;
        Debug.Log("[ADS] Mostrando interstitial de prueba...");

        try
        {
            interstitialAd.Show();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ADS] Show() falló: " + exception.Message);
            // Si Show lanza una excepción, igualmente continuamos la transición.
            FinishInterstitial(interstitialAd, null);
        }

        return true;
#else
        Debug.Log("[ADS] Mostrar anuncio de prueba requiere Android Development Build.");
        return false;
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
        currentInterstitialIsFromGameOver = false;
        Action onFinished = pendingAfterInterstitial;
        pendingAfterInterstitial = null;
        ad.Destroy();

        // Cada interstitial solo puede mostrarse una vez.
        LoadTestInterstitial();

        // Siempre regresar al juego, tanto al cerrar como si el anuncio falla.
        // El callback ya se ha vaciado, evitando duplicar reinicios.
        onFinished?.Invoke();
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
