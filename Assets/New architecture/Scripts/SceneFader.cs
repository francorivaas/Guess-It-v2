using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance;

    [Header("Referencias UI")]
    public CanvasGroup fadeGroup;

    [Header("Configuración")]
    public float fadeDuration = 0.4f; // Duración del fundido en segundos

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Al iniciar cualquier escena, automáticamente hacemos un Fade In (de negro a transparente)
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        fadeGroup.alpha = 1f;
        fadeGroup.blocksRaycasts = true; // Bloquea la UI mientras aparece la pantalla

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false; // Libera la UI para que el jugador interactúe
    }

    // Esta es la función pública que llamarán tus botones
    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        fadeGroup.blocksRaycasts = true; // Bloquea la UI para evitar dobles clics

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = 1f;

        // Una vez que la pantalla está totalmente negra, cargamos la escena
        SceneManager.LoadScene(sceneName);
    }
}