using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using GoogleMobileAds.Ump.Api;

public class PrivacyManager : MonoBehaviour
{
    public static PrivacyManager Instance { get; private set; }

    public event Action<bool> PrivacyOptionsStateChanged;

    private SynchronizationContext unityContext;

    public bool CanRequestAds =>
        ConsentInformation.CanRequestAds();

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

        // Se captura desde Awake, que se ejecuta en el hilo principal de Unity.
        unityContext = SynchronizationContext.Current;

        if (unityContext == null)
        {
            Debug.LogError(
                "[PRIVACY] No se pudo obtener el contexto principal de Unity."
            );
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializePrivacy();
    }

    public void InitializePrivacy()
    {
        Debug.Log("[PRIVACY] Actualizando consentimiento...");

        var request = new ConsentRequestParameters();

#if UNITY_ANDROID && !UNITY_EDITOR && DEVELOPMENT_BUILD
        // Solo para las pruebas actuales: simula una ubicacion europea.
        // No se incluye en las builds de produccion.
        request.ConsentDebugSettings = new ConsentDebugSettings
        {
            DebugGeography = DebugGeography.EEA,
            TestDeviceHashedIds = new List<string>
            {
                SystemInfo.deviceUniqueIdentifier.ToUpperInvariant()
            }
        };

        Debug.Log("[PRIVACY] Modo de prueba europeo activado.");
#endif

        ConsentInformation.Update(request, updateError =>
        {
            if (updateError != null)
            {
                Debug.LogWarning(
                    "[PRIVACY] Error al actualizar: " +
                    updateError.Message
                );

                // UMP puede mantener un estado valido de la sesion anterior.
                CheckConsentStatus();
                return;
            }

            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
            {
                if (formError != null)
                {
                    Debug.LogWarning(
                        "[PRIVACY] Error del formulario: " +
                        formError.Message
                    );
                }

                CheckConsentStatus();
            });
        });
    }

    private void CheckConsentStatus()
    {
        if (unityContext == null)
        {
            Debug.LogError(
                "[PRIVACY] No se puede actualizar el estado: " +
                "falta el contexto principal de Unity."
            );
            return;
        }

        unityContext.Post(_ =>
        {
            if (this == null || Instance != this)
                return;

            Debug.Log("[PRIVACY] Finalizo el flujo UMP.");

            bool canRequest = CanRequestAds;
            bool optionsRequired = PrivacyOptionsRequired;

            Debug.Log(
                "[PRIVACY] Puede solicitar anuncios: " +
                canRequest
            );

            Debug.Log(
                "[PRIVACY] Opciones de privacidad requeridas: " +
                optionsRequired
            );

            // Actualiza la visibilidad del boton sin consultar UMP en Update().
            PrivacyOptionsStateChanged?.Invoke(optionsRequired);

            // AdsManager protege contra inicializaciones duplicadas.
            if (canRequest)
            {
                AdsManager.Instance?.InitializeOnce();
            }
        }, null);
    }

    public void ShowPrivacyOptions()
    {
        if (!PrivacyOptionsRequired)
            return;

        ConsentForm.ShowPrivacyOptionsForm(formError =>
        {
            if (formError != null)
            {
                Debug.LogWarning(
                    "[PRIVACY] Error en opciones: " +
                    formError.Message
                );
            }

            // Reevalua el estado despues de cerrar el formulario.
            CheckConsentStatus();
        });
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
