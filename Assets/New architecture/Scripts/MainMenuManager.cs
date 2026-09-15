using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // <-- Necesario
using System.Collections; // <-- Necesario

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI menuHighScoreText;

    [Header("Feedback")]
    [SerializeField] private string feedbackFormUrl;

    [System.Obsolete]
    void Start()
    {
        int currentHighScore = PlayerPrefs.GetInt("HighScore", 0);

        if (menuHighScoreText != null)
        {
            menuHighScoreText.text = "Mejor Puntaje: " + currentHighScore;
        }

        // Buscamos los botones del men� y les inyectamos el Juice
        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button btn in allButtons)
        {
            Vector3 originalScale = btn.transform.localScale;
            btn.onClick.AddListener(() => StartCoroutine(AnimateButtonPop(btn.transform, originalScale)));
        }
    }

    public void PlayGame()
    {
        // En lugar de cargar abruptamente, llamamos a la animaci�n
        SceneFader.Instance.FadeToScene("GameScene_UIResponsive");
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void OpenFeedbackForm()
    {
        if (string.IsNullOrWhiteSpace(feedbackFormUrl))
        {
            Debug.LogWarning("No se configur� la URL del formulario de feedback.");
            return;
        }

        Application.OpenURL(feedbackFormUrl);
    }

    // La misma corrutina de elasticidad
    private IEnumerator AnimateButtonPop(Transform btnTransform, Vector3 originalScale)
    {
        float elapsed = 0f;
        float shrinkDuration = 0.05f;
        float returnDuration = 0.15f;
        Vector3 targetScale = originalScale * 0.85f;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.deltaTime;
            btnTransform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / shrinkDuration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            btnTransform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / returnDuration);
            yield return null;
        }

        btnTransform.localScale = originalScale;
    }
}