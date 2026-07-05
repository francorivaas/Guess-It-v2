using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI Containers & Prefabs")]
    public Transform hintContainer;
    public GameObject hintCardPrefab;
    public RectTransform uiToShake;

    [Header("Texts & Inputs")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI livesText;
    public TextMeshProUGUI messageText;
    public TMP_InputField answerInput;

    [Header("Streak UI - Core")]
    public TextMeshProUGUI streakText;
    public ParticleSystem streakParticles;
    public AudioClip streakSFX;

    [Header("Streak UI - Settings")]
    public float streakDisplayDuration = 1.5f;
    public float streakFadeDuration = 0.5f;

    [Tooltip("Intensidad del temblor/vibración cuando la racha es >= 10")]
    public float shakeIntensity = 4f;

    [Header("Streak UI - Colors")]
    [Tooltip("Añade aquí los colores llamativos entre los que alternará el combo (ej: Rojo Neón, Dorado, Cian...)")]
    public Color[] streakColorVariants;

    private Coroutine streakAnimation;
    private Vector3 originalStreakLocalPosition;
    private bool hasSavedOriginalPosition = false;

    [Header("Juice & Effects")]
    public TextMeshProUGUI gainedPointsText;

    [SerializeField] private LifeUI lifeUI;

    private List<GameObject> spawnedCards = new List<GameObject>();

    private AudioSource audioSrc;
    public AudioClip correctSFX;
    public AudioClip incorrectSFX;
    public AudioClip hintCardSFX;

    private int currentDisplayedScore = 0;
    private bool isInputLocked = false;

    [Header("UI Containers & Prefabs")]
    public UnityEngine.UI.ScrollRect scrollRect;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI highScoreText;
    public GameObject newHighScoreBadge;

    [Header("Pause UI")]
    public GameObject pausePanel;
    public AudioSource backgroundMusic; // Arrastra aquí el objeto con el componente AudioSource de tu música
    public Image musicButtonImage;      // Opcional: Para cambiar el ícono/color si está silenciado
    public Sprite musicOnSprite;        // Opcional: Icono de música activada
    public Sprite musicOffSprite;       // Opcional: Icono de música desactivada

    private bool isPaused = false;
    
    [Header("Reveal UI")]
    public GameObject revealPanel; // El panel oscuro con la carta
    public TextMeshProUGUI correctAnswerText;

    private void Awake()
    {
        Instance = this;
        audioSrc = GetComponentInChildren<AudioSource>();
    }

    private void Start()
    {
        // Limpiamos el texto inferior al iniciar
        if (messageText != null) messageText.text = "";

        Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button btn in allButtons)
        {
            Vector3 originalScale = btn.transform.localScale;
            btn.onClick.AddListener(() => StartCoroutine(AnimateButtonPop(btn.transform, originalScale)));
        }
    }

    public void RefreshUI()
    {
        // Ahora la UI lee el puntaje "mostrado" y no salta automáticamente
        scoreText.text = currentDisplayedScore.ToString();
        livesText.text = "Vidas: " + GameManager.Instance.lives;
        lifeUI.UpdateLives(GameManager.Instance.lives);

        RiddleSO currentRiddle =
            GameManager.Instance.GetCurrentRiddle();

        int targetHintCount =
            GameManager.Instance.GetCurrentHintCount();

        if (
            currentRiddle == null ||
            currentRiddle.hints == null
        )
        {
            Debug.LogError(
                "No hay un acertijo válido para mostrar."
            );

            return;
        }

        List<string> allHints =
            new List<string>(currentRiddle.hints);

        if (spawnedCards.Count == 0)
        {
            StartCoroutine(SpawnInitialCardsSequentially(allHints, targetHintCount));
        }
        else if (spawnedCards.Count < targetHintCount)
        {
            int nextIndex = spawnedCards.Count;
            if (nextIndex < allHints.Count)
            {
                SpawnAndAnimateCard(allHints[nextIndex]);
            }
        }
    }

    private IEnumerator SpawnInitialCardsSequentially(List<string> hints, int count)
    {
        LockInput(true); // Bloqueamos el input mientras caen las cartas

        for (int i = 0; i < count; i++)
        {
            if (i < hints.Count)
            {
                SpawnAndAnimateCard(hints[i]);
                yield return new WaitForSeconds(0.2f);
            }
        }

        LockInput(false); // Liberamos al terminar
    }

    private void SpawnAndAnimateCard(string hintText)
    {
        GameObject newCard = Instantiate(hintCardPrefab, hintContainer);
        spawnedCards.Add(newCard);
        audioSrc.PlayOneShot(hintCardSFX);

        TextMeshProUGUI cardText = newCard.GetComponentInChildren<TextMeshProUGUI>();
        if (cardText != null) cardText.text = hintText;

        CanvasGroup canvasGroup = newCard.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = newCard.AddComponent<CanvasGroup>();

        StartCoroutine(AnimateCardAppearance(newCard.transform, canvasGroup));
        StartCoroutine(ScrollToBottom());
    }

    private IEnumerator AnimateCardAppearance(Transform cardTransform, CanvasGroup canvasGroup)
    {
        float elapsed = 0f;
        float duration = 0.3f;

        canvasGroup.alpha = 0f;
        cardTransform.localScale = new Vector3(0.7f, 0.7f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            canvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);

            float scaleValue = Mathf.Lerp(0.7f, 1.05f, progress);
            if (progress > 0.8f)
                scaleValue = Mathf.Lerp(1.05f, 1f, (progress - 0.8f) / 0.2f);

            cardTransform.localScale = new Vector3(scaleValue, scaleValue, 1f);

            yield return null;
        }

        canvasGroup.alpha = 1f;
        cardTransform.localScale = Vector3.one;
    }

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

    public void ClearCards()
    {
        foreach (GameObject card in spawnedCards)
        {
            Destroy(card);
        }
        spawnedCards.Clear();
    }

    public void TriggerVictorySequence(int targetScore, int pointsGained, System.Action onComplete)
    {
        StartCoroutine(AnimateScoreRollup(targetScore, pointsGained, onComplete));
    }

    private IEnumerator AnimateScoreRollup(int targetScore, int pointsGained, System.Action onComplete)
    {
        LockInput(true);

        // Preparar el "+XX"
        if (gainedPointsText != null)
        {
            gainedPointsText.text = "+" + pointsGained;
            gainedPointsText.alpha = 1f; // Lo hacemos visible
        }

        if (messageText != null) messageText.text = "";
        if (audioSrc != null && correctSFX != null) audioSrc.PlayOneShot(correctSFX);

        Vector3 originalScale = scoreText.transform.localScale;
        Color originalColor = scoreText.color;
        float duration = 1.0f;
        float elapsed = 0f;
        int startScore = currentDisplayedScore;

        // FASE 1: Conteo
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            currentDisplayedScore = Mathf.RoundToInt(Mathf.Lerp(startScore, targetScore, elapsed / duration));
            scoreText.text = currentDisplayedScore.ToString();
            yield return null;
        }

        currentDisplayedScore = targetScore;
        scoreText.text = currentDisplayedScore.ToString();

        // FASE 2: Ocultar el "+XX" justo antes del pulso
        if (gainedPointsText != null) gainedPointsText.alpha = 0f;

        // FASE 3: Pulso de victoria
        float pulseDuration = 0.35f;
        float pulseElapsed = 0f;
        Color pulseColor = new Color(1f, 0.9f, 0.2f);

        while (pulseElapsed < pulseDuration)
        {
            pulseElapsed += Time.deltaTime;
            float progress = pulseElapsed / pulseDuration;
            float curve = Mathf.Sin(progress * Mathf.PI);

            // Escalado uniforme ahora que el Pivot está bien
            scoreText.transform.localScale = originalScale * (1f + (curve * 0.3f));
            scoreText.color = Color.Lerp(originalColor, pulseColor, curve);

            yield return null;
        }

        scoreText.transform.localScale = originalScale;
        scoreText.color = originalColor;

        yield return new WaitForSeconds(0.2f);
        LockInput(false);
        onComplete?.Invoke();
    }

    private IEnumerator AnimateScoreRollup(int targetScore, System.Action onComplete)
    {
        LockInput(true);

        // 1. Ocultamos textos de error previos
        if (messageText != null) messageText.text = "";

        // 2. Ejecutamos Audio y Partículas
        if (audioSrc != null && correctSFX != null) audioSrc.PlayOneShot(correctSFX);

        // Guardamos el estado original para poder restaurarlo perfectamente
        Vector3 originalScale = scoreText.transform.localScale;
        Color originalColor = scoreText.color;

        // FASE 1: Animación numérica rápida (1 segundo) SIN MOVIMIENTO
        float duration = 1.0f;
        float elapsed = 0f;
        int startScore = currentDisplayedScore;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // Interpolación de los números solamente
            currentDisplayedScore = Mathf.RoundToInt(Mathf.Lerp(startScore, targetScore, progress));
            scoreText.text = currentDisplayedScore.ToString();

            yield return null; // Esperamos al siguiente frame
        }

        // Aseguramos que termine con el valor exacto
        currentDisplayedScore = targetScore;
        scoreText.text = currentDisplayedScore.ToString();

        // FASE 2: Pulso visual y Tinte amarillo (0.3 segundos)
        float pulseDuration = 0.35f;
        float pulseElapsed = 0f;
        Color pulseColor = new Color(1f, 0.9f, 0.2f); // Un amarillo dorado agradable

        while (pulseElapsed < pulseDuration)
        {
            pulseElapsed += Time.deltaTime;
            float progress = pulseElapsed / pulseDuration;

            // Usamos un Seno (Mathf.Sin) para hacer una curva suave que sube y baja.
            // Al multiplicar por PI, la curva va de 0 a 1 y vuelve a 0.
            float curve = Mathf.Sin(progress * Mathf.PI);

            // Aumentamos la escala hasta un 30% más grande en el pico del pulso
            float scaleMultiplier = 1f + (curve * 0.3f);
            scoreText.transform.localScale = originalScale * scaleMultiplier;

            // Interpolamos el color hacia el amarillo y de vuelta al original
            scoreText.color = Color.Lerp(originalColor, pulseColor, curve);

            yield return null;
        }

        // 3. Restauramos estado original por seguridad
        scoreText.transform.localScale = originalScale;
        scoreText.color = originalColor;

        // Breve pausa para saborear la victoria
        yield return new WaitForSeconds(0.2f);

        // 4. ¡Terminó la secuencia! Liberamos todo y avisamos al GameManager
        LockInput(false);
        onComplete?.Invoke();
    }

    public void TriggerFailureJuice()
    {
        if (audioSrc != null && incorrectSFX != null) audioSrc.PlayOneShot(incorrectSFX);
    }

    public void SubmitAnswer()
    {
        // Protegemos la función si estamos animando
        if (isInputLocked || string.IsNullOrEmpty(answerInput.text)) return;

        GameManager.Instance.SubmitAnswer(answerInput.text);
        answerInput.text = "";
    }

    public void RequestHint()
    {
        // Protegemos la función si estamos animando
        if (isInputLocked) return;

        GameManager.Instance.RequestHint();
    }

    public void ShowMessage(string msg)
    {
        // Lo usaremos solo para mensajes de error o pistas extras
        messageText.text = msg;
    }

    // Método centralizado para trancar el juego
    private void LockInput(bool state)
    {
        isInputLocked = state;
        if (answerInput != null) answerInput.interactable = !state;
    }

    private IEnumerator ScrollToBottom()
    {
        // Esperamos a que Unity recalcule los tamaños del contenedor
        yield return new WaitForEndOfFrame();

        // Movemos el scrollbar al fondo (0 es abajo, 1 es arriba)
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    public void TriggerErrorShake()
    {
        if (uiToShake != null)
        {
            // Iniciamos la sacudida: 0.3 segundos de duración, 15 píxeles de fuerza
            StartCoroutine(ShakeCoroutine(uiToShake, 0.3f, 15f));
        }
    }

    private IEnumerator ShakeCoroutine(RectTransform target, float duration, float magnitude)
    {
        // Guardamos la posición original para que no se desalinee al terminar
        Vector2 originalPos = target.anchoredPosition;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            // Generamos un movimiento aleatorio solo en el eje X (izquierda/derecha)
            float offsetX = Random.Range(-1f, 1f) * magnitude;

            target.anchoredPosition = new Vector2(originalPos.x + offsetX, originalPos.y);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Aseguramos que vuelva exactamente a su lugar
        target.anchoredPosition = originalPos;
    }

    public void ShowGameOverScreen(int finalScore, int highScore, bool isNewHighScore)
    {
        // Activamos el panel que tapará la pantalla
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        // Actualizamos los textos
        if (finalScoreText != null) finalScoreText.text = "Puntaje Final: " + finalScore;
        if (highScoreText != null) highScoreText.text = "Mejor Puntaje: " + highScore;

        // Si superó el récord, activamos el cartel de "Nuevo Récord", si no, lo ocultamos
        //if (newHighScoreBadge != null) newHighScoreBadge.SetActive(isNewHighScore);
    }

    /// <summary>
    /// Activa o desactiva el estado de pausa congelando el tiempo del juego.
    /// </summary>
    public void TogglePause()
    {
        isPaused = !isPaused;

        if (pausePanel != null)
            pausePanel.SetActive(isPaused);

        // Congela el tiempo del juego (0 = pausado, 1 = tiempo normal)
        Time.timeScale = isPaused ? 0f : 1f;
    }

    /// <summary>
    /// Método directo para el botón de "Reanudar"
    /// </summary>
    public void ResumeGame()
    {
        isPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Prende o apaga la música de fondo
    /// </summary>
    public void ToggleMusic()
    {
        if (backgroundMusic != null)
        {
            // Invertimos el estado de mute
            backgroundMusic.mute = !backgroundMusic.mute;

            // Opcional: Cambiar el aspecto visual del botón para darle feedback al usuario
            if (musicButtonImage != null && musicOnSprite != null && musicOffSprite != null)
            {
                musicButtonImage.sprite = backgroundMusic.mute ? musicOffSprite : musicOnSprite;
            }
        }
    }

    /// <summary>
    /// Sale al menú principal SIN guardar el High Score
    /// </summary>
    public void ExitToMainMenuFromPause()
    {
        // ¡SÚPER IMPORTANTE! Reestablecer el tiempo antes de cambiar de escena,
        // de lo contrario el Menú Principal se cargará congelado.
        Time.timeScale = 1f;

        // Cargamos el Menú Principal (Asumiendo que es el índice 0)
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    /// <summary>
    /// Actualiza y anima el texto de la racha
    /// </summary>

    private IEnumerator AnimateStreakPop()
    {
        // Hacemos que el texto crezca rápidamente y vuelva a su tamaño (Efecto Pop/Latido)
        float duration = 0.35f;
        float elapsed = 0f;
        Vector3 originalScale = Vector3.one;
        Vector3 targetScale = Vector3.one * 1.6f; // Crece un 60%

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // Usamos una curva Seno para que suba y baje de forma fluida
            float curve = Mathf.Sin(progress * Mathf.PI);
            streakText.transform.localScale = Vector3.Lerp(originalScale, targetScale, curve);

            yield return null;
        }

        streakText.transform.localScale = originalScale;
    }

    public void UpdateStreakUI(int currentStreak)
    {
        // Guardamos la posición inicial exacta la primera vez que se usa para poder resetear el shake sin descolocar la UI
        if (!hasSavedOriginalPosition && streakText != null)
        {
            originalStreakLocalPosition = streakText.transform.localPosition;
            hasSavedOriginalPosition = true;
        }

        // SI SE ROMPE LA RACHA (Menor a 2)
        if (currentStreak < 2)
        {
            if (streakAnimation != null) StopCoroutine(streakAnimation);

            if (streakText != null)
            {
                streakText.transform.localPosition = originalStreakLocalPosition; // Devolvemos a su sitio por si estaba vibrando
                streakText.gameObject.SetActive(false);
            }
            return;
        }

        // SI LA RACHA SIGUE ACTIVA
        if (streakText != null)
        {
            streakText.gameObject.SetActive(true);

            // Si estamos en racha de 10 o más, añadimos un texto más imponente (opcional)
            if (currentStreak >= 10)
                streakText.text = "x" + currentStreak + "!";
            else
                streakText.text = "x" + currentStreak;

            // 1. SELECCIÓN DE COLOR ALEATORIO
            Color chosenColor = Color.white;
            if (streakColorVariants != null && streakColorVariants.Length > 0)
            {
                // Elige un índice al azar del array de colores configurado en el Inspector
                chosenColor = streakColorVariants[UnityEngine.Random.Range(0, streakColorVariants.Length)];
            }
            else
            {
                chosenColor = streakText.color;
            }

            // Forzamos que sea 100% opaco al inicio del golpe
            chosenColor.a = 1f;
            streakText.color = chosenColor;

            // 2. APLICAR COLOR A LAS PARTÍCULAS
            if (streakParticles != null)
            {
                var mainModule = streakParticles.main;
                mainModule.startColor = chosenColor; // Modifica el color de emisión de Unity por código
                streakParticles.Play();
            }

            // Reiniciamos la corrutina para procesar el nuevo número y comportamiento
            if (streakAnimation != null) StopCoroutine(streakAnimation);
            streakAnimation = StartCoroutine(AnimateStreakFlow(currentStreak, chosenColor));
        }

        // Sonido con Pitch dinámico
        if (audioSrc != null && streakSFX != null)
        {
            float pitch = 1f + (currentStreak * 0.05f);
            audioSrc.pitch = Mathf.Clamp(pitch, 1f, 1.6f);
            audioSrc.PlayOneShot(streakSFX);
            Invoke(nameof(ResetPitch), 1f);
        }
    }

    private IEnumerator AnimateStreakFlow(int currentStreak, Color baseColor)
    {
        // Aseguramos que empiece en su posición e incline normal por si venía de un shake anterior
        streakText.transform.localPosition = originalStreakLocalPosition;

        // FASE 1: IMPACTO / POP (Ocurre siempre, da igual el nivel de racha)
        HapticManager.SuccessVibration();
        float popDuration = 0.2f;
        float elapsed = 0f;
        Vector3 originalScale = Vector3.one;
        Vector3 targetScale = Vector3.one * 1.5f;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / popDuration;
            float curve = Mathf.Sin(progress * Mathf.PI);
            streakText.transform.localScale = Vector3.Lerp(originalScale, targetScale, curve);
            yield return null;
        }
        streakText.transform.localScale = originalScale;

        // FASE 2: BIFURCACIÓN DE LÓGICA (¿Es racha mayor a 10?)
        if (currentStreak >= 10)
        {
            // MODO VIBRACIÓN PERPETUA: Mientras el jugador mantenga esta racha, este 'while(true)'
            // se ejecutará frame a frame haciendo que el texto tiemble. Se detendrá inmediatamente
            // cuando el jugador responda otra cosa (ya que el método principal hace 'StopCoroutine').
            while (true)
            {
                float offsetX = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity);
                float offsetY = UnityEngine.Random.Range(-shakeIntensity, shakeIntensity);

                streakText.transform.localPosition = originalStreakLocalPosition + new Vector3(offsetX, offsetY, 0f);

                yield return null; // Espera al siguiente frame
            }
        }
        else
        {
            // MODO NORMAL (< 10): Espera el tiempo configurable y se desvanece
            yield return new WaitForSeconds(streakDisplayDuration);

            elapsed = 0f;
            Color transparentColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

            while (elapsed < streakFadeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / streakFadeDuration;

                streakText.color = Color.Lerp(baseColor, transparentColor, progress);
                yield return null;
            }

            streakText.gameObject.SetActive(false);
            streakText.color = baseColor;
        }
    }

    private void ResetPitch()
    {
        if (audioSrc != null) audioSrc.pitch = 1f;
    }

    public void ShowRevealPanel(string correctAnswer)
    {
        if (revealPanel != null) revealPanel.SetActive(true);
        if (correctAnswerText != null) correctAnswerText.text = correctAnswer;
    }

    public void HideRevealPanel()
    {
        if (revealPanel != null) revealPanel.SetActive(false);
    }
}