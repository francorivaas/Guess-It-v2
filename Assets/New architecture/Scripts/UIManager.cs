using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [SerializeField]
    private Color unavailableHintColor =
        new Color32(120, 120, 120, 255);

    [SerializeField] private AudioClip noMoreHintsSFX;
    [Header("Status")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private LifeUI lifeUI;

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
    private Vector3 originalStreakPosition;
    private bool originalStreakPositionSaved;
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

    public void ShowCorrectFeedback(int totalScore, int gainedPoints)
    {
        displayedScore = totalScore;

        if (scoreText != null)
        {
            scoreText.text = displayedScore.ToString();
        }

        ShowMessage($"¡Correcto! +{gainedPoints}");
        PlayEffect(correctSFX);
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

        // Debe continuar activo para poder emitir el sonido.
        if (requestHintButton != null)
        {
            requestHintButton.interactable = true;
        }
    }
}
