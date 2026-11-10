
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(CanvasGroup))]
public class PrivacyOptionsButtonUI : MonoBehaviour
{
    private Button button;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        button = GetComponent<Button>();
        canvasGroup = GetComponent<CanvasGroup>();

        button.onClick.AddListener(OpenPrivacyOptions);

        SetVisible(false);
    }

    private void Start()
    {
        if (PrivacyManager.Instance != null)
        {
            PrivacyManager.Instance.PrivacyOptionsStateChanged
                += SetVisible;

            SetVisible(
                PrivacyManager.Instance.PrivacyOptionsRequired
            );
        }
    }

    private void SetVisible(bool visible)
    {
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void OpenPrivacyOptions()
    {
        if (PrivacyManager.Instance == null)
            return;

        if (!PrivacyManager.Instance.PrivacyOptionsRequired)
            return;

        Debug.Log("[PRIVACY] Abriendo opciones de privacidad.");

        PrivacyManager.Instance.ShowPrivacyOptions();
    }

    private void OnDestroy()
    {
        if (PrivacyManager.Instance != null)
        {
            PrivacyManager.Instance.PrivacyOptionsStateChanged
                -= SetVisible;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(OpenPrivacyOptions);
        }
    }
}
