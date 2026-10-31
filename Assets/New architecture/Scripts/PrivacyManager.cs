using System;
using System.Threading;
using UnityEngine;
using GoogleMobileAds.Ump.Api;

public class PrivacyManager : MonoBehaviour
{
    public static PrivacyManager Instance { get; private set; }

    // Se conserva para que PrivacyOptionsButtonUI siga funcionando sin cambios.
    public event Action<bool> PrivacyOptionsStateChanged;
    // Permite renovar el banner cuando cambia el estado de consentimiento.
    public event Action ConsentStatusUpdated;

    private SynchronizationContext unityContext;

    public bool ConsentFlowFinished { get; private set; }

    public bool CanRequestAds => ConsentInformation.CanRequestAds();

    public bool PrivacyOptionsRequired =>
        ConsentInformation.PrivacyOptionsRequirementStatus ==
        PrivacyOptionsRequirementStatus.Required;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        unityContext = SynchronizationContext.Current;

        if (unityContext == null)
            Debug.LogError("[PRIVACY] Falta el contexto principal de Unity.");

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializePrivacy();
    }

    public void InitializePrivacy()
    {
        ConsentFlowFinished = false;
        Debug.Log("[PRIVACY] Actualizando consentimiento...");

        // Sin DebugGeography, ni TestDeviceHashedIds, ni Reset().
        // Google determinara el mensaje segun la region real del usuario.
        var request = new ConsentRequestParameters();

        ConsentInformation.Update(request, updateError =>
        {
            if (updateError != null)
            {
                Debug.LogWarning("[PRIVACY] Error al actualizar: " +
                                 updateError.Message);
                // UMP puede conservar un estado valido de la sesion anterior.
                CheckConsentStatus();
                return;
            }

            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null)
                    Debug.LogWarning("[PRIVACY] Error del formulario: " +
                                     formError.Message);

                CheckConsentStatus();
            });
        });
    }

    private void CheckConsentStatus()
    {
        if (unityContext == null)
        {
            Debug.LogError("[PRIVACY] No se puede evaluar consentimiento sin UnityContext.");
            return;
        }

        unityContext.Post(_ =>
        {
            if (this == null || Instance != this)
                return;

            ConsentFlowFinished = true;
            bool canRequest = CanRequestAds;
            bool optionsRequired = PrivacyOptionsRequired;

            Debug.Log("[PRIVACY] Puede solicitar anuncios: " + canRequest);
            Debug.Log("[PRIVACY] Opciones de privacidad requeridas: " +
                      optionsRequired);

            PrivacyOptionsStateChanged?.Invoke(optionsRequired);

            // Descarta anuncios preparados con preferencias anteriores.
            AdsManager.Instance?.OnPrivacyStatusUpdated();

            // InitializeOnce protege contra inicializaciones duplicadas.
            if (canRequest)
                AdsManager.Instance?.InitializeOnce();

            ConsentStatusUpdated?.Invoke();
        }, null);
    }

    public void ShowPrivacyOptions()
    {
        if (!PrivacyOptionsRequired)
            return;

        ConsentForm.ShowPrivacyOptionsForm(formError =>
        {
            if (formError != null)
                Debug.LogWarning("[PRIVACY] Error en opciones: " +
                                 formError.Message);

            CheckConsentStatus();
        });
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
