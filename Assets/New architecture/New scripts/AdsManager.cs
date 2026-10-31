using System;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    private enum AdsState { Uninitialized, Initializing, Initialized }

    // IDs de AdMob para Android. Nunca usar IDs reales en Development Build.
    private const string TestInterstitialId =
        "ca-app-pub-3940256099942544/1033173712";
    private const string ProductionInterstitialId =
        "ca-app-pub-4382732485118871/5361235288";

    private static string InterstitialUnitId
    {
        get
        {
#if DEVELOPMENT_BUILD
            return TestInterstitialId;
#else
            return ProductionInterstitialId;
#endif
        }
    }

    [Header("Frecuencia de interstitials")]
    [SerializeField, Min(1)] private int gamesPerInterstitial = 3;
    [SerializeField, Min(0f)] private float minimumSecondsBetweenAds = 120f;
    [SerializeField, Min(0f)] private float minimumSecondsAfterResume = 15f;

    private const string FinishedGamesKey = "GuessIt_FinishedGamesSinceInterstitial";
    private const string LastInterstitialUtcKey = "GuessIt_LastInterstitialUtc";
    private const float MaxCachedAdAgeSeconds = 50f * 60f;

    private AdsState state = AdsState.Uninitialized;
    private InterstitialAd interstitialAd;
    private bool isInterstitialLoading;
    private bool isInterstitialShowing;
    private int loadGeneration;
    private float loadedAtRealtime;

    private int finishedGamesSinceInterstitial;
    private long lastInterstitialUtc;
    private float lastResumeRealtime;
    private Action pendingAfterInterstitial;
    private bool currentInterstitialIsFromGameOver;

    public bool IsInitialized => state == AdsState.Initialized;

    public bool IsInterstitialReady()
    {
        return !isInterstitialShowing && interstitialAd != null &&
               interstitialAd.CanShowAd() &&
               Time.realtimeSinceStartup - loadedAtRealtime < MaxCachedAdAgeSeconds;
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

    private void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            lastResumeRealtime = Time.realtimeSinceStartup;
            RefreshExpiredInterstitial();
        }
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
                LoadInterstitial();
            });
        });
    }

    // Llamado al cerrar el formulario de privacidad o actualizar UMP.
    // Descarta el anuncio preparado bajo las preferencias anteriores.
    public void OnPrivacyStatusUpdated()
    {
        if (!IsInitialized || isInterstitialShowing)
            return;

        loadGeneration++; // Invalida callbacks de cargas anteriores.
        isInterstitialLoading = false;
        DestroyLoadedInterstitial();
        LoadInterstitial();
    }

    private void LoadInterstitial()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
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
        int generation = ++loadGeneration;
        Debug.Log("[ADS] Cargando interstitial " +
#if DEVELOPMENT_BUILD
                  "DE PRUEBA...");
#else
                  "DE PRODUCCION...");
#endif

        InterstitialAd.Load(InterstitialUnitId, new AdRequest(),
            (InterstitialAd ad, LoadAdError error) =>
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (this == null || Instance != this ||
                        generation != loadGeneration)
                    {
                        ad?.Destroy();
                        return;
                    }

                    isInterstitialLoading = false;

                    if (error != null || ad == null)
                    {
                        ad?.Destroy();
                        Debug.LogWarning("[ADS] Fallo al cargar interstitial: " +
                            (error != null ? error.ToString() : "Anuncio nulo"));
                        return; // Reintentar en una siguiente oportunidad, sin bucles.
                    }

                    // Si UMP cambió mientras se procesaba la carga, no conservarla.
                    if (PrivacyManager.Instance == null ||
                        !PrivacyManager.Instance.CanRequestAds)
                    {
                        ad.Destroy();
                        return;
                    }

                    interstitialAd = ad;
                    loadedAtRealtime = Time.realtimeSinceStartup;
                    RegisterInterstitialEvents(ad);
                    Debug.Log("[ADS] Interstitial cargado. Listo: " +
                              IsInterstitialReady());
                });
            });
#else
        Debug.Log("[ADS] Interstitial disponible solo en Android.");
#endif
    }

    private void RefreshExpiredInterstitial()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsInitialized || isInterstitialShowing)
            return;

        if (interstitialAd != null && !IsInterstitialReady())
        {
            DestroyLoadedInterstitial();
        }

        if (interstitialAd == null && !isInterstitialLoading)
            LoadInterstitial();
#endif
    }

    private void DestroyLoadedInterstitial()
    {
        if (interstitialAd != null)
        {
            interstitialAd.Destroy();
            interstitialAd = null;
        }
    }

    private void RegisterInterstitialEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened += () =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (this == null || Instance != this ||
                    interstitialAd != ad || !isInterstitialShowing)
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

    // Se llama UNA vez al terminar una partida por quedarse sin vidas.
    public void RegisterFinishedGame()
    {
        finishedGamesSinceInterstitial = Mathf.Min(
            finishedGamesSinceInterstitial + 1, 1000000);
        PlayerPrefs.SetInt(FinishedGamesKey, finishedGamesSinceInterstitial);
        PlayerPrefs.Save();

        Debug.Log("[ADS] Partidas terminadas desde el ultimo anuncio: " +
                  finishedGamesSinceInterstitial + "/" + gamesPerInterstitial);
    }

    // Si devuelve false, GameManager sigue inmediatamente con la transicion.
    public bool TryShowGameOverInterstitial(Action onFinished)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (onFinished == null)
            return false;

        if (finishedGamesSinceInterstitial < gamesPerInterstitial)
        {
            Debug.Log("[ADS] Frecuencia no alcanzada.");
            return false;
        }

        double elapsedSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() -
                                lastInterstitialUtc;
        if (lastInterstitialUtc > 0 && elapsedSeconds < minimumSecondsBetweenAds)
        {
            Debug.Log("[ADS] Cooldown activo. Sin anuncio.");
            return false;
        }

        if (Time.realtimeSinceStartup - lastResumeRealtime < minimumSecondsAfterResume)
        {
            Debug.Log("[ADS] Se reanudo la app recientemente. Sin anuncio.");
            return false;
        }

        RefreshExpiredInterstitial();
        return TryShowLoadedInterstitial(onFinished, true);
#else
        return false;
#endif
    }

    // Exclusivamente para el boton temporal de pruebas. Inerte en produccion.
    public void ShowTestInterstitial()
    {
#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (!TryShowLoadedInterstitial(null, false))
            Debug.Log("[ADS] Interstitial no disponible para prueba.");
#else
        Debug.Log("[ADS] Boton de prueba deshabilitado fuera de Development Build Android.");
#endif
    }

    private bool TryShowLoadedInterstitial(Action onFinished, bool fromGameOver)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsInitialized || PrivacyManager.Instance == null ||
            !PrivacyManager.Instance.CanRequestAds || !IsInterstitialReady())
            return false;

        pendingAfterInterstitial = onFinished;
        currentInterstitialIsFromGameOver = fromGameOver;
        isInterstitialShowing = true;
        Debug.Log("[ADS] Mostrando interstitial...");

        try
        {
            interstitialAd.Show();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ADS] Show() fallo: " + exception.Message);
            FinishInterstitial(interstitialAd, null);
        }

        // En caso de fallo de Show(), el callback ya continuo la transicion.
        return true;
#else
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

        LoadInterstitial(); // Cada anuncio puede mostrarse una sola vez.
        onFinished?.Invoke(); // No bloquear una partida si hubo un error.
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        loadGeneration++;
        DestroyLoadedInterstitial();
        Instance = null;
    }
}
