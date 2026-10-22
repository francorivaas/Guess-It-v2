
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class PrivacyOptionsButtonUI : MonoBehaviour
{
    private Button button;
    private CanvasGroup canvasGroup;
    private bool isVisible;

    private void Awake()
    {
        button = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();

        button.onClick.AddListener(OpenPrivacyOptions);

        SetVisible(false);
    }

    private void Update()
    {
        bool shouldBeVisible =
            PrivacyManager.Instance != null &&
            PrivacyManager.Instance.PrivacyOptionsRequired;

        if (shouldBeVisible != isVisible)
        {
            SetVisible(shouldBeVisible);
        }
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void OpenPrivacyOptions()
    {
        if (PrivacyManager.Instance == null)
        {
            Debug.LogWarning(
                "[PRIVACY] PrivacyManager no disponible."
            );
            return;
        }

        if (!PrivacyManager.Instance.PrivacyOptionsRequired)
        {
            return;
        }

        Debug.Log("[PRIVACY] Abriendo opciones de privacidad.");

        PrivacyManager.Instance.ShowPrivacyOptions();
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OpenPrivacyOptions);
        }
    }
}
