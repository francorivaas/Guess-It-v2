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

    private AdsState state = AdsState.Uninitialized;

    public bool IsInitialized =>
        state == AdsState.Initialized;

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
            Debug.Log("[ADS] Esperando autorización de UMP.");
            return;
        }

        state = AdsState.Initializing;

        Debug.Log("[ADS] Inicializando Google Mobile Ads...");

        MobileAds.Initialize(initStatus =>
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (this == null)
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
            });
        });
    }
}
