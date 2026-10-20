
using UnityEngine;
using GoogleMobileAds.Ump.Api;

public class PrivacyManager : MonoBehaviour
{
    public static PrivacyManager Instance { get; private set; }

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

        ConsentInformation.Update(request, updateError =>
        {
            if (updateError != null)
            {
                Debug.LogWarning(
                    "[PRIVACY] Error al actualizar: " +
                    updateError.Message
                );

                CheckConsentStatus();
                return;
            }

            ConsentForm.LoadAndShowConsentFormIfRequired(
                formError =>
                {
                    if (formError != null)
                    {
                        Debug.LogWarning(
                            "[PRIVACY] Error del formulario: " +
                            formError.Message
                        );
                    }

                    CheckConsentStatus();
                }
            );
        });
    }

    private void CheckConsentStatus()
    {
        Debug.Log(
            "[PRIVACY] Puede solicitar anuncios: " +
            CanRequestAds
        );

        Debug.Log(
            "[PRIVACY] Opciones de privacidad requeridas: " +
            PrivacyOptionsRequired
        );

        // Más adelante conectaremos AdsManager aquí.
        // Por ahora NO inicializamos anuncios.
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

            CheckConsentStatus();
        });
    }
}
