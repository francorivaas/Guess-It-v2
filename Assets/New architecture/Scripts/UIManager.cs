using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Hints")]
    [SerializeField] private Transform hintContainer;
    [SerializeField] private GameObject hintCardPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [SerializeField] private Button requestHintButton;
    [SerializeField] private Graphic requestHintButtonGraphic;
    [SerializeField] private TextMeshProUGUI requestHintButtonText;
    [SerializeField] private Color availableHintColor = Color.white;
    [SerializeField] private Color unavailableHintColor = new Color32(120, 120, 120, 255);
    [SerializeField] private AudioClip noMoreHintsSFX;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private LifeUI lifeUI;

    [Header("Score Transfer")]
    [Tooltip("Texto que aparece debajo del puntaje total, por ejemplo: +350.")]
    [SerializeField] private TextMeshProUGUI gainedPointsText;

    [Tooltip("Duración de la pequeña animación de entrada del texto +XXX.")]
    [SerializeField, Min(0.01f)] private float gainedPointsIntroDuration = 0.15f;

    [Tooltip("Tiempo que el +XXX permanece completo en pantalla antes de empezar a transferirse.")]
    [SerializeField, Min(0f)] private float gainedPointsInitialHoldDuration = 1f;

    [Tooltip("Tiempo durante el cual los puntos pasan del bonus al puntaje total.")]
    [SerializeField, Min(0.05f)] private float scoreTransferDuration = 0.65f;

    [Tooltip("Tiempo que permanece visible el +0 antes de desaparecer.")]
    [SerializeField, Min(0f)] private float gainedPointsHoldDuration = 0.08f;

    [Tooltip("Duración del desvanecimiento final del texto de puntos ganados.")]
    [SerializeField, Min(0.01f)] private float gainedPointsFadeDuration = 0.2f;

    [Tooltip("Escala máxima del pequeño pulso final del puntaje total.")]
    [SerializeField, Range(1f, 1.5f)] private float finalScorePulseScale = 1.15f;

    [Tooltip("Duración del pulso final del puntaje total.")]
    [SerializeField, Min(0.05f)] private float finalScorePulseDuration = 0.18f;

    [Tooltip("Pausa adicional después de terminar la transferencia y antes de iniciar la siguiente ronda.")]
    [SerializeField, Min(0f)] private float delayBeforeNextRound = 0.15f;

    [Header("Answer")]
    [SerializeField] private TMP_InputField answerInput;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private RectTransform uiToShake;

    [Header("Audio")]
    [SerializeField] private AudioSource effectsAudioSource;
    [SerializeField] private AudioClip correctSFX;
    [SerializeField] private AudioClip incorrectSFX;
    [SerializeField] private AudioClip hintCardSFX;

    [Header("Streak")]
    [SerializeField] private TextMeshProUGUI streakText;
    [SerializeField] private ParticleSystem streakParticles;
    [SerializeField] private AudioClip streakSFX;
    [SerializeField] private Color[] streakColorVariants;
    [SerializeField, Min(0f)] private float streakDisplayDuration = 1.5f;
    [SerializeField, Min(0f)] private float streakFadeDuration = 0.5f;
    [SerializeField, Min(0f)] private float streakShakeIntensity = 4f;

    [Header("Pause")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private AudioSource backgroundMusic;
    [SerializeField] private Image musicButtonImage;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;

    [Header("Reveal")]
    [SerializeField] private GameObject revealPanel;
    [SerializeField] private TextMeshProUGUI correctAnswerText;

    private readonly List<GameObject> spawnedCards = new List<GameObject>();

    private Coroutine streakAnimation;
    private Coroutine scoreTransferAnimation;

    private Vector3 originalStreakPosition;
    private Vector3 originalScoreScale = Vector3.one;
    private Vector3 originalGainedPointsScale = Vector3.one;

    private bool originalStreakPositionSaved;
    private bool scoreVisualsInitialized;
    private bool inputLocked;
    private bool paused;
    private int displayedScore;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (effectsAudioSource == null)
        {
            effectsAudioSource = GetComponentInChildren<AudioSource>();
        }
    }

    private void Start()
    {
        ShowMessage(string.Empty);
        RegisterButtonAnimations();
        InitializeScoreVisuals();

        if (GameManager.Instance != null)
        {
            displayedScore = GameManager.Instance.Score;
        }

        if (scoreText != null)
        {
            scoreText.text = displayedScore.ToString();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RefreshUI()
    {
        RefreshStatusUI();
        RefreshHintButtonState();

        RiddleSO currentRiddle = GameManager.Instance?.GetCurrentRiddle();

        if (currentRiddle?.hints == null)
        {
            Debug.LogError("No hay un acertijo válido para mostrar.");
            return;
        }

        int targetHintCount = GameManager.Instance.GetCurrentHintCount();

        if (spawnedCards.Count == 0)
        {
            StartCoroutine(
                SpawnInitialCardsSequentially(currentRiddle.hints, targetHintCount)
            );
            return;
        }

        if (spawnedCards.Count < targetHintCount)
        {
            SpawnHintCard(currentRiddle.hints[spawnedCards.Count]);
        }
    }

    public void RefreshStatusUI()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        if (scoreText != null)
        {
            scoreText.text = displayedScore.ToString();
        }

        if (livesText != null)
        {
            livesText.text = $"Vidas: {GameManager.Instance.Lives}";
        }

        lifeUI?.UpdateLives(GameManager.Instance.Lives);
    }

    /// <summary>
    /// Muestra el feedback de acierto y anima la transferencia de los puntos ganados
    /// hacia el puntaje total. Por ejemplo: +350 baja hasta +0 mientras el puntaje
    /// total aumenta hasta alcanzar totalScore.
    /// </summary>
    public void ShowCorrectFeedback(
        int totalScore,
        int gainedPoints,
        Action onComplete
    )
    {
        ShowMessage($"¡Correcto! +{gainedPoints}");
        PlayEffect(correctSFX);
        InitializeScoreVisuals();

        // Bloquea respuestas y solicitudes de pista durante toda la celebración.
        LockInput(true);

        if (scoreTransferAnimation != null)
        {
            StopCoroutine(scoreTransferAnimation);
            RestoreScoreVisuals();
        }

        scoreTransferAnimation = StartCoroutine(
            AnimateScoreTransfer(
                totalScore,
                Mathf.Max(0, gainedPoints),
                onComplete
            )
        );
    }

    // Sobrecarga para conservar compatibilidad con cualquier llamada anterior.
    public void ShowCorrectFeedback(int totalScore, int gainedPoints)
    {
        ShowCorrectFeedback(totalScore, gainedPoints, null);
    }

    public void TriggerFailureFeedback()
    {
        PlayEffect(incorrectSFX);
    }

    public void SubmitAnswer()
    {
        if (inputLocked || answerInput == null || string.IsNullOrWhiteSpace(answerInput.text))
        {
            return;
        }

        string submittedAnswer = answerInput.text;
        answerInput.text = string.Empty;

        GameManager.Instance?.SubmitAnswer(submittedAnswer);
    }

    public void RequestHint()
    {
        if (inputLocked || GameManager.Instance == null)
        {
            return;
        }

        if (!GameManager.Instance.HasMoreHints())
        {
            PlayEffect(noMoreHintsSFX);
            return;
        }

        GameManager.Instance.RequestHint();
    }

    public void ShowMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    public void ClearCards()
    {
        foreach (GameObject card in spawnedCards)
        {
            if (card != null)
            {
                Destroy(card);
            }
        }

        spawnedCards.Clear();
    }

    public void TriggerErrorShake()
    {
        if (uiToShake != null)
        {
            StartCoroutine(Shake(uiToShake, 0.3f, 15f));
        }
    }

    public void UpdateStreakUI(int streak)
    {
        if (streakText == null)
        {
            return;
        }

        SaveOriginalStreakPosition();

        if (streakAnimation != null)
        {
            StopCoroutine(streakAnimation);
            streakAnimation = null;
        }

        streakText.transform.localPosition = originalStreakPosition;

        if (streak < 2)
        {
            streakText.gameObject.SetActive(false);
            return;
        }

        streakText.gameObject.SetActive(true);
        streakText.text = streak >= 10 ? $"x{streak}!" : $"x{streak}";

        Color chosenColor = GetRandomStreakColor();
        streakText.color = chosenColor;

        if (streakParticles != null)
        {
            ParticleSystem.MainModule particlesMain = streakParticles.main;
            particlesMain.startColor = chosenColor;
            streakParticles.Play();
        }

        PlayStreakSound(streak);
        streakAnimation = StartCoroutine(AnimateStreak(streak, chosenColor));
    }

    public void TogglePause()
    {
        SetPauseState(!paused);
    }

    public void ResumeGame()
    {
        SetPauseState(false);
    }

    public void ToggleMusic()
    {
        if (backgroundMusic == null)
        {
            return;
        }

        backgroundMusic.mute = !backgroundMusic.mute;

        if (musicButtonImage != null && musicOnSprite != null && musicOffSprite != null)
        {
            musicButtonImage.sprite =
                backgroundMusic.mute ? musicOffSprite : musicOnSprite;
        }
    }

    public void ExitToMainMenuFromPause()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public void ShowRevealPanel(string correctAnswer)
    {
        if (revealPanel != null)
        {
            revealPanel.SetActive(true);
        }

        if (correctAnswerText != null)
        {
            correctAnswerText.text = correctAnswer;
        }

        LockInput(true);
    }

    public void HideRevealPanel()
    {
        if (revealPanel != null)
        {
            revealPanel.SetActive(false);
        }

        LockInput(false);
    }

    private IEnumerator AnimateScoreTransfer(
        int totalScore,
        int gainedPoints,
        Action onComplete
    )
    {
        int startScore = Mathf.Max(0, totalScore - gainedPoints);
        displayedScore = startScore;

        if (scoreText != null)
        {
            scoreText.text = displayedScore.ToString();
            scoreText.transform.localScale = originalScoreScale;
        }

        if (gainedPointsText != null)
        {
            gainedPointsText.gameObject.SetActive(true);
            gainedPointsText.alpha = 1f;
            gainedPointsText.text = $"+{gainedPoints}";
            gainedPointsText.transform.localScale = originalGainedPointsScale * 0.75f;
        }

        if (gainedPoints <= 0)
        {
            displayedScore = totalScore;

            if (scoreText != null)
            {
                scoreText.text = displayedScore.ToString();
            }

            HideGainedPointsImmediately();

            yield return CompleteCorrectSequence(onComplete);
            yield break;
        }

        // Entrada visual del +XXX.
        yield return AnimateGainedPointsEntrance();

        // El bonus permanece completo para que el jugador pueda leer cuánto ganó.
        if (gainedPointsInitialHoldDuration > 0f)
        {
            yield return new WaitForSeconds(gainedPointsInitialHoldDuration);
        }

        float elapsed = 0f;

        while (elapsed < scoreTransferDuration)
        {
            elapsed += Time.deltaTime;
            float linearProgress = Mathf.Clamp01(elapsed / scoreTransferDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, linearProgress);

            int transferredPoints = Mathf.RoundToInt(gainedPoints * easedProgress);
            int remainingPoints = Mathf.Max(0, gainedPoints - transferredPoints);

            displayedScore = startScore + transferredPoints;

            if (scoreText != null)
            {
                scoreText.text = displayedScore.ToString();
            }

            if (gainedPointsText != null)
            {
                gainedPointsText.text = $"+{remainingPoints}";
                gainedPointsText.transform.localScale = originalGainedPointsScale;
            }

            yield return null;
        }

        displayedScore = totalScore;

        if (scoreText != null)
        {
            scoreText.text = displayedScore.ToString();
        }

        if (gainedPointsText != null)
        {
            gainedPointsText.text = "+0";
        }

        yield return PulseFinalScore();

        if (gainedPointsHoldDuration > 0f)
        {
            yield return new WaitForSeconds(gainedPointsHoldDuration);
        }

        yield return FadeOutGainedPoints();

        RestoreScoreVisuals();

        yield return CompleteCorrectSequence(onComplete);
    }

    private IEnumerator CompleteCorrectSequence(Action onComplete)
    {
        if (delayBeforeNextRound > 0f)
        {
            yield return new WaitForSeconds(delayBeforeNextRound);
        }

        scoreTransferAnimation = null;

        if (onComplete != null)
        {
            // En el flujo normal, este callback es GameManager.NextRound().
            // Esa llamada limpia las cartas actuales e inicia las nuevas pistas.
            onComplete.Invoke();
        }
        else
        {
            // Una llamada sin callback no debe dejar el input bloqueado.
            LockInput(false);
        }
    }

    private IEnumerator AnimateGainedPointsEntrance()
    {
        if (gainedPointsText == null)
        {
            yield break;
        }

        float elapsed = 0f;
        Vector3 startScale = originalGainedPointsScale * 0.75f;
        Vector3 overshootScale = originalGainedPointsScale * 1.12f;

        while (elapsed < gainedPointsIntroDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / gainedPointsIntroDuration);

            if (progress < 0.7f)
            {
                float firstPhase = progress / 0.7f;
                gainedPointsText.transform.localScale = Vector3.Lerp(
                    startScale,
                    overshootScale,
                    firstPhase
                );
            }
            else
            {
                float secondPhase = (progress - 0.7f) / 0.3f;
                gainedPointsText.transform.localScale = Vector3.Lerp(
                    overshootScale,
                    originalGainedPointsScale,
                    secondPhase
                );
            }

            yield return null;
        }

        gainedPointsText.transform.localScale = originalGainedPointsScale;
    }

    private IEnumerator PulseFinalScore()
    {
        if (scoreText == null)
        {
            yield break;
        }

        float elapsed = 0f;
        Vector3 pulseScale = originalScoreScale * finalScorePulseScale;

        while (elapsed < finalScorePulseDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / finalScorePulseDuration);
            float curve = Mathf.Sin(progress * Mathf.PI);

            scoreText.transform.localScale = Vector3.Lerp(
                originalScoreScale,
                pulseScale,
                curve
            );

            yield return null;
        }

        scoreText.transform.localScale = originalScoreScale;
    }

    private IEnumerator FadeOutGainedPoints()
    {
        if (gainedPointsText == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < gainedPointsFadeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / gainedPointsFadeDuration);

            gainedPointsText.alpha = 1f - progress;
            gainedPointsText.transform.localScale = Vector3.Lerp(
                originalGainedPointsScale,
                originalGainedPointsScale * 0.9f,
                progress
            );

            yield return null;
        }

        HideGainedPointsImmediately();
    }

    private void InitializeScoreVisuals()
    {
        if (scoreVisualsInitialized)
        {
            return;
        }

        if (scoreText != null)
        {
            originalScoreScale = scoreText.transform.localScale;
        }

        if (gainedPointsText != null)
        {
            originalGainedPointsScale = gainedPointsText.transform.localScale;
            HideGainedPointsImmediately();
        }

        scoreVisualsInitialized = true;
    }

    private void RestoreScoreVisuals()
    {
        if (scoreText != null)
        {
            scoreText.transform.localScale = originalScoreScale;
            scoreText.text = displayedScore.ToString();
        }

        HideGainedPointsImmediately();
    }

    private void HideGainedPointsImmediately()
    {
        if (gainedPointsText == null)
        {
            return;
        }

        gainedPointsText.alpha = 0f;
        gainedPointsText.text = string.Empty;
        gainedPointsText.transform.localScale = originalGainedPointsScale;
        gainedPointsText.gameObject.SetActive(false);
    }

    private IEnumerator SpawnInitialCardsSequentially(
        IReadOnlyList<string> hints,
        int count
    )
    {
        LockInput(true);

        int cardsToSpawn = Mathf.Min(count, hints.Count);

        for (int i = 0; i < cardsToSpawn; i++)
        {
            SpawnHintCard(hints[i]);
            yield return new WaitForSeconds(0.2f);
        }

        LockInput(false);
    }

    private void SpawnHintCard(string hint)
    {
        if (hintCardPrefab == null || hintContainer == null)
        {
            Debug.LogError("Falta asignar Hint Card Prefab o Hint Container.");
            return;
        }

        GameObject newCard = Instantiate(hintCardPrefab, hintContainer);
        spawnedCards.Add(newCard);

        TextMeshProUGUI cardText = newCard.GetComponentInChildren<TextMeshProUGUI>();

        if (cardText != null)
        {
            cardText.text = hint;
        }

        CanvasGroup canvasGroup = newCard.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = newCard.AddComponent<CanvasGroup>();
        }

        PlayEffect(hintCardSFX);
        StartCoroutine(AnimateCardAppearance(newCard.transform, canvasGroup));
        StartCoroutine(ScrollToBottom());
    }

    private IEnumerator AnimateCardAppearance(
        Transform cardTransform,
        CanvasGroup canvasGroup
    )
    {
        const float duration = 0.3f;
        float elapsed = 0f;

        canvasGroup.alpha = 0f;
        cardTransform.localScale = Vector3.one * 0.7f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            canvasGroup.alpha = progress;

            float scale = progress < 0.8f
                ? Mathf.Lerp(0.7f, 1.05f, progress / 0.8f)
                : Mathf.Lerp(1.05f, 1f, (progress - 0.8f) / 0.2f);

            cardTransform.localScale = Vector3.one * scale;
            yield return null;
        }

        canvasGroup.alpha = 1f;
        cardTransform.localScale = Vector3.one;
    }

    private void RegisterButtonAnimations()
    {
        Button[] buttons = FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Button button in buttons)
        {
            Transform buttonTransform = button.transform;
            Vector3 originalScale = buttonTransform.localScale;

            button.onClick.AddListener(
                () => StartCoroutine(AnimateButtonPop(buttonTransform, originalScale))
            );
        }
    }

    private IEnumerator AnimateButtonPop(Transform buttonTransform, Vector3 originalScale)
    {
        const float shrinkDuration = 0.05f;
        const float returnDuration = 0.15f;

        Vector3 targetScale = originalScale * 0.85f;

        yield return ScaleOverTime(
            buttonTransform,
            originalScale,
            targetScale,
            shrinkDuration
        );

        yield return ScaleOverTime(
            buttonTransform,
            targetScale,
            originalScale,
            returnDuration
        );

        buttonTransform.localScale = originalScale;
    }

    private static IEnumerator ScaleOverTime(
        Transform target,
        Vector3 startScale,
        Vector3 endScale,
        float duration
    )
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            target.localScale = Vector3.Lerp(
                startScale,
                endScale,
                Mathf.Clamp01(elapsed / duration)
            );
            yield return null;
        }
    }

    private IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private static IEnumerator Shake(
        RectTransform target,
        float duration,
        float magnitude
    )
    {
        Vector2 originalPosition = target.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float offsetX = Random.Range(-magnitude, magnitude);
            target.anchoredPosition =
                new Vector2(originalPosition.x + offsetX, originalPosition.y);

            yield return null;
        }

        target.anchoredPosition = originalPosition;
    }

    private IEnumerator AnimateStreak(int streak, Color baseColor)
    {
        HapticManager.SuccessVibration();

        const float popDuration = 0.2f;
        float elapsed = 0f;

        while (elapsed < popDuration)
        {
            elapsed += Time.deltaTime;
            float curve = Mathf.Sin((elapsed / popDuration) * Mathf.PI);
            streakText.transform.localScale =
                Vector3.Lerp(Vector3.one, Vector3.one * 1.5f, curve);
            yield return null;
        }

        streakText.transform.localScale = Vector3.one;

        if (streak >= 10)
        {
            while (true)
            {
                float offsetX = Random.Range(
                    -streakShakeIntensity,
                    streakShakeIntensity
                );
                float offsetY = Random.Range(
                    -streakShakeIntensity,
                    streakShakeIntensity
                );

                streakText.transform.localPosition =
                    originalStreakPosition + new Vector3(offsetX, offsetY, 0f);

                yield return null;
            }
        }

        yield return new WaitForSeconds(streakDisplayDuration);

        Color transparentColor =
            new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        elapsed = 0f;

        while (elapsed < streakFadeDuration)
        {
            elapsed += Time.deltaTime;
            streakText.color = Color.Lerp(
                baseColor,
                transparentColor,
                Mathf.Clamp01(elapsed / streakFadeDuration)
            );
            yield return null;
        }

        streakText.gameObject.SetActive(false);
        streakText.color = baseColor;
    }

    private Color GetRandomStreakColor()
    {
        if (streakColorVariants == null || streakColorVariants.Length == 0)
        {
            Color currentColor = streakText.color;
            currentColor.a = 1f;
            return currentColor;
        }

        Color selectedColor =
            streakColorVariants[Random.Range(0, streakColorVariants.Length)];

        selectedColor.a = 1f;
        return selectedColor;
    }

    private void PlayStreakSound(int streak)
    {
        if (effectsAudioSource == null || streakSFX == null)
        {
            return;
        }

        effectsAudioSource.pitch = Mathf.Clamp(1f + streak * 0.05f, 1f, 1.6f);
        effectsAudioSource.PlayOneShot(streakSFX);
        StartCoroutine(ResetAudioPitchAfterDelay(1f));
    }

    private IEnumerator ResetAudioPitchAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (effectsAudioSource != null)
        {
            effectsAudioSource.pitch = 1f;
        }
    }

    private void PlayEffect(AudioClip clip)
    {
        if (effectsAudioSource != null && clip != null)
        {
            effectsAudioSource.PlayOneShot(clip);
        }
    }

    private void SetPauseState(bool shouldPause)
    {
        paused = shouldPause;

        if (pausePanel != null)
        {
            pausePanel.SetActive(paused);
        }

        Time.timeScale = paused ? 0f : 1f;
    }

    private void SaveOriginalStreakPosition()
    {
        if (originalStreakPositionSaved)
        {
            return;
        }

        originalStreakPosition = streakText.transform.localPosition;
        originalStreakPositionSaved = true;
    }

    private void LockInput(bool shouldLock)
    {
        inputLocked = shouldLock;

        if (answerInput != null)
        {
            answerInput.interactable = !shouldLock;
        }
    }

    private void RefreshHintButtonState()
    {
        bool hasMoreHints =
            GameManager.Instance != null &&
            GameManager.Instance.HasMoreHints();

        if (requestHintButtonGraphic != null)
        {
            requestHintButtonGraphic.color =
                hasMoreHints
                    ? availableHintColor
                    : unavailableHintColor;
        }

        if (requestHintButtonText != null)
        {
            requestHintButtonText.text =
                hasMoreHints
                    ? "Pedir pista"
                    : "Sin pistas";
        }

        // Debe continuar activo para poder emitir el sonido de acción bloqueada.
        if (requestHintButton != null)
        {
            requestHintButton.interactable = true;
        }
    }
}
