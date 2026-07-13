using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class UIManager : MonoBehaviour
{
    [Serializable]
    private sealed class CategoryVisualData
    {
        [SerializeField] private string categoryName;
        [SerializeField] private Sprite icon;

        public string CategoryName => categoryName;
        public Sprite Icon => icon;
    }

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

    [Header("Hint Counter")]
    [Tooltip("Raíz visual del contador. Este objeto recibe la animación de pulso.")]
    [SerializeField] private RectTransform hintCounterRoot;

    [Tooltip("Imagen o Graphic que muestra el fondo del contador.")]
    [SerializeField] private Graphic hintCounterGraphic;

    [Tooltip("Texto TMP que muestra cuántas pistas extra quedan.")]
    [SerializeField] private TextMeshProUGUI hintCounterText;

    [Tooltip("Color del número mientras todavía quedan pistas.")]
    [SerializeField] private Color hintCounterAvailableTextColor = Color.white;

    [Tooltip("Color del número cuando el contador llega a cero.")]
    [SerializeField]
    private Color hintCounterUnavailableTextColor =
        new Color32(210, 210, 210, 255);

    [Tooltip("Escala máxima del pequeño pulso cuando se consume una pista.")]
    [SerializeField, Range(1f, 1.5f)] private float hintCounterPulseScale = 1.2f;

    [Tooltip("Duración total del pulso del contador.")]
    [SerializeField, Min(0.05f)] private float hintCounterPulseDuration = 0.22f;

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

    // Se obtiene automáticamente desde TMP_InputField.placeholder.
    // Guardamos el texto original para ocultarlo al seleccionar el campo
    // y restaurarlo al comenzar una ronda nueva.
    private Graphic answerPlaceholderGraphic;
    private TMP_Text answerPlaceholderText;
    private string answerPlaceholderOriginalText = string.Empty;
    private bool answerPlaceholderInitialized;
    private bool answerPlaceholderDismissed;

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

    [Header("Fever Background")]
    [Tooltip("Controla las partículas y el tinte del fondo según la racha actual.")]
    [SerializeField] private FeverBackgroundController feverBackground;

    [Header("Pause")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private AudioSource backgroundMusic;
    [SerializeField] private Image musicButtonImage;
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;

    [Header("Reveal")]
    [SerializeField] private GameObject revealPanel;
    [SerializeField] private RectTransform revealPanelRoot;
    [SerializeField] private CanvasGroup revealPanelCanvasGroup;
    [SerializeField] private TextMeshProUGUI correctAnswerText;
    [SerializeField] private Button revealContinueButton;

    [Header("Victory")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private RectTransform victoryPanelRoot;
    [SerializeField] private CanvasGroup victoryPanelCanvasGroup;
    [SerializeField] private TextMeshProUGUI victoryAnswerText;
    [SerializeField] private Button victoryContinueButton;

    [Header("Result Panels Animation")]
    [Tooltip("Escala inicial y final de salida de las tarjetas de resultado.")]
    [SerializeField, Range(0.01f, 0.95f)] private float resultPanelStartScale = 0.15f;

    [Tooltip("Escala máxima alcanzada durante el pop de entrada.")]
    [SerializeField, Range(1f, 1.3f)] private float resultPanelOvershootScale = 1.08f;

    [Tooltip("Duración de la animación de entrada.")]
    [SerializeField, Min(0.05f)] private float resultPanelEnterDuration = 0.32f;

    [Tooltip("Duración de la animación de salida.")]
    [SerializeField, Min(0.05f)] private float resultPanelExitDuration = 0.22f;

    [Tooltip("Espera antes de comenzar el efecto de escritura.")]
    [SerializeField, Min(0f)] private float typewriterStartDelay = 0.12f;

    [Tooltip("Tiempo entre cada carácter del efecto de escritura.")]
    [SerializeField, Min(0.001f)] private float typewriterCharacterDelay = 0.025f;

    [Tooltip("Pausa adicional después de signos y saltos de línea.")]
    [SerializeField, Min(0f)] private float typewriterPunctuationDelay = 0.08f;

    [Header("Category Chip")]
    [SerializeField] private GameObject categoryChip;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private Image categoryIconImage;
    [SerializeField] private Sprite defaultCategoryIcon;
    [SerializeField]
    private List<CategoryVisualData> categoryVisuals =
        new List<CategoryVisualData>();

    [SerializeField] private CanvasGroup categoryCanvasGroup;
    [SerializeField] private RectTransform categoryChipRoot;

    [SerializeField, Min(0.05f)]
    private float categoryChipAnimationDuration = 0.25f;

    [SerializeField, Range(0.5f, 1f)]
    private float categoryChipStartScale = 0.75f;

    [SerializeField, Min(0f)]
    private float categoryChipStartOffsetY = 18f;

    [Header("Category Attention")]
    [Tooltip("Franja brillante que recorre horizontalmente el chip.")]
    [SerializeField] private RectTransform categoryShineRoot;

    [Tooltip("CanvasGroup de la franja brillante.")]
    [SerializeField] private CanvasGroup categoryShineCanvasGroup;

    [Tooltip("Partículas que se disparan cuando se presenta una categoría importante.")]
    [SerializeField] private ParticleSystem categoryAttentionParticles;

    [Tooltip("Duración del recorrido del brillo y del pulso.")]
    [SerializeField, Min(0.05f)] private float categoryAttentionDuration = 0.65f;

    [Tooltip("Escala máxima del chip durante el pulso de atención.")]
    [SerializeField, Range(1f, 1.25f)] private float categoryAttentionPulseScale = 1.06f;

    [Tooltip("Opacidad máxima de la franja brillante.")]
    [SerializeField, Range(0f, 1f)] private float categoryShineMaxAlpha = 0.9f;

    [Tooltip("Margen extra para que el brillo empiece y termine fuera del chip.")]
    [SerializeField, Min(0f)] private float categoryShineHorizontalPadding = 24f;

    private readonly List<GameObject> spawnedCards = new List<GameObject>();

    private Coroutine categoryChipAnimation;
    private Vector2 categoryChipBasePosition;
    private Vector2 categoryShineBasePosition;
    private Vector3 categoryChipBaseScale = Vector3.one;
    private bool categoryChipPositionSaved;
    private bool categoryChipNeedsAnimation = true;
    private bool categoryAttentionRequested;

    private Coroutine streakAnimation;
    private Coroutine scoreTransferAnimation;
    private Coroutine resultPanelAnimation;
    private Coroutine resultTypewriterAnimation;
    private Coroutine hintCounterPulseAnimation;

    private Vector3 originalStreakPosition;
    private Vector3 hintCounterBaseScale = Vector3.one;
    private Vector3 victoryPanelBaseScale = Vector3.one;
    private Vector3 revealPanelBaseScale = Vector3.one;
    private Vector3 originalScoreScale = Vector3.one;
    private Vector3 originalGainedPointsScale = Vector3.one;

    private bool originalStreakPositionSaved;
    private bool scoreVisualsInitialized;
    private bool inputLocked;
    private bool paused;
    private bool resultPanelTransitioning;
    private int displayedScore;
    private int lastDisplayedRemainingHints = -1;


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

        InitializeCategoryChip();
        InitializeResultPanels();
        InitializeHintCounter();
        InitializeAnswerInputPlaceholder();

        if (feverBackground == null)
        {
            feverBackground = FindFirstObjectByType<FeverBackgroundController>();
        }

        feverBackground?.SetStreak(0, true);
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

    private void InitializeCategoryChip()
    {
        if (categoryChipRoot != null)
        {
            categoryChipBasePosition =
                categoryChipRoot.anchoredPosition;

            categoryChipBaseScale =
                categoryChipRoot.localScale;

            categoryChipPositionSaved = true;
        }

        if (categoryShineRoot != null)
        {
            categoryShineBasePosition =
                categoryShineRoot.anchoredPosition;

            categoryShineRoot.gameObject.SetActive(false);
        }

        if (categoryShineCanvasGroup != null)
        {
            categoryShineCanvasGroup.alpha = 0f;
            categoryShineCanvasGroup.interactable = false;
            categoryShineCanvasGroup.blocksRaycasts = false;
        }

        if (categoryAttentionParticles != null)
        {
            categoryAttentionParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        if (categoryCanvasGroup != null)
        {
            categoryCanvasGroup.alpha = 0f;
        }

        if (categoryChip != null)
        {
            categoryChip.SetActive(false);
        }

        categoryAttentionRequested = false;
    }

    /// <summary>
    /// Solicita que la próxima categoría mostrada reciba el recorrido de brillo,
    /// el pulso y las partículas. GameManager lo llama al iniciar la partida y
    /// justo antes de crear la ronda posterior a una respuesta correcta.
    /// </summary>
    public void RequestCategoryAttention()
    {
        categoryAttentionRequested = true;
    }

    private void RefreshCategoryChip(RiddleSO currentRiddle)
    {
        if (
            categoryChip == null ||
            categoryText == null ||
            currentRiddle == null
        )
        {
            return;
        }

        string category = currentRiddle.category?.Trim();

        if (string.IsNullOrWhiteSpace(category))
        {
            categoryChip.SetActive(false);
            categoryAttentionRequested = false;
            return;
        }

        categoryText.text =
            category.ToUpperInvariant().ToString();

        RefreshCategoryIcon(category);
        categoryChip.SetActive(true);

        if (!categoryChipPositionSaved && categoryChipRoot != null)
        {
            categoryChipBasePosition =
                categoryChipRoot.anchoredPosition;

            categoryChipBaseScale =
                categoryChipRoot.localScale;

            categoryChipPositionSaved = true;
        }

        bool shouldAnimateEntrance = categoryChipNeedsAnimation;
        bool shouldPlayAttention = categoryAttentionRequested;

        categoryChipNeedsAnimation = false;
        categoryAttentionRequested = false;

        if (!shouldAnimateEntrance && !shouldPlayAttention)
        {
            SetCategoryChipFinalState();
            return;
        }

        StopCategoryChipAnimation();

        categoryChipAnimation = StartCoroutine(
            AnimateCategoryChip(
                shouldAnimateEntrance,
                shouldPlayAttention
            )
        );
    }

    private void RefreshCategoryIcon(string category)
    {
        if (categoryIconImage == null)
        {
            return;
        }

        Sprite selectedIcon = defaultCategoryIcon;
        string requestedKey = NormalizeCategoryKey(category);

        if (categoryVisuals != null)
        {
            foreach (CategoryVisualData visualData in categoryVisuals)
            {
                if (
                    visualData == null ||
                    string.IsNullOrWhiteSpace(visualData.CategoryName)
                )
                {
                    continue;
                }

                if (
                    NormalizeCategoryKey(visualData.CategoryName) ==
                    requestedKey
                )
                {
                    selectedIcon = visualData.Icon;
                    break;
                }
            }
        }

        categoryIconImage.sprite = selectedIcon;
        categoryIconImage.enabled = selectedIcon != null;
        categoryIconImage.preserveAspect = true;
    }

    private static string NormalizeCategoryKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string decomposed = value
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        StringBuilder builder = new StringBuilder(decomposed.Length);

        foreach (char character in decomposed)
        {
            UnicodeCategory unicodeCategory =
                CharUnicodeInfo.GetUnicodeCategory(character);

            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private IEnumerator AnimateCategoryChip(
        bool animateEntrance,
        bool playAttention
    )
    {
        if (
            categoryChipRoot == null ||
            categoryCanvasGroup == null
        )
        {
            SetCategoryChipFinalState();

            if (playAttention)
            {
                PlayCategoryParticles();
            }

            categoryChipAnimation = null;
            yield break;
        }

        if (animateEntrance)
        {
            Vector2 startPosition =
                categoryChipBasePosition +
                Vector2.up * categoryChipStartOffsetY;

            Vector3 startScale =
                categoryChipBaseScale * categoryChipStartScale;

            float elapsed = 0f;

            categoryCanvasGroup.alpha = 0f;
            categoryChipRoot.anchoredPosition = startPosition;
            categoryChipRoot.localScale = startScale;

            while (elapsed < categoryChipAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(
                    elapsed / categoryChipAnimationDuration
                );

                float easedProgress = Mathf.SmoothStep(
                    0f,
                    1f,
                    progress
                );

                categoryCanvasGroup.alpha = easedProgress;

                categoryChipRoot.anchoredPosition =
                    Vector2.Lerp(
                        startPosition,
                        categoryChipBasePosition,
                        easedProgress
                    );

                // Pequeño overshoot para lograr el efecto candy/pop.
                float pop =
                    Mathf.Sin(progress * Mathf.PI) * 0.12f;

                float scaleMultiplier =
                    Mathf.Lerp(
                        categoryChipStartScale,
                        1f,
                        easedProgress
                    ) + pop;

                categoryChipRoot.localScale =
                    categoryChipBaseScale * scaleMultiplier;

                yield return null;
            }
        }

        SetCategoryChipFinalState();

        if (playAttention)
        {
            yield return AnimateCategoryAttention();
        }

        SetCategoryChipFinalState();
        categoryChipAnimation = null;
    }

    private IEnumerator AnimateCategoryAttention()
    {
        PlayCategoryParticles();

        bool canAnimateShine =
            categoryShineRoot != null &&
            categoryShineCanvasGroup != null;

        if (canAnimateShine)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                categoryChipRoot
            );

            categoryShineRoot.gameObject.SetActive(true);
            categoryShineCanvasGroup.alpha = 0f;

            float chipWidth = Mathf.Max(
                1f,
                categoryChipRoot.rect.width
            );

            float shineWidth = Mathf.Max(
                1f,
                categoryShineRoot.rect.width
            );

            float halfTravel =
                chipWidth * 0.5f +
                shineWidth * 0.5f +
                categoryShineHorizontalPadding;

            categoryShineRoot.anchoredPosition =
                new Vector2(
                    -halfTravel,
                    categoryShineBasePosition.y
                );
        }

        float elapsed = 0f;

        while (elapsed < categoryAttentionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / categoryAttentionDuration
            );

            float easedProgress = Mathf.SmoothStep(
                0f,
                1f,
                progress
            );

            float pulse = Mathf.Sin(progress * Mathf.PI);
            float scaleMultiplier = Mathf.Lerp(
                1f,
                categoryAttentionPulseScale,
                pulse
            );

            if (categoryChipRoot != null)
            {
                categoryChipRoot.localScale =
                    categoryChipBaseScale * scaleMultiplier;
            }

            if (canAnimateShine)
            {
                float chipWidth = Mathf.Max(
                    1f,
                    categoryChipRoot.rect.width
                );

                float shineWidth = Mathf.Max(
                    1f,
                    categoryShineRoot.rect.width
                );

                float halfTravel =
                    chipWidth * 0.5f +
                    shineWidth * 0.5f +
                    categoryShineHorizontalPadding;

                categoryShineRoot.anchoredPosition =
                    new Vector2(
                        Mathf.Lerp(
                            -halfTravel,
                            halfTravel,
                            easedProgress
                        ),
                        categoryShineBasePosition.y
                    );

                categoryShineCanvasGroup.alpha =
                    pulse * categoryShineMaxAlpha;
            }

            yield return null;
        }

        if (categoryChipRoot != null)
        {
            categoryChipRoot.localScale =
                categoryChipBaseScale;
        }

        ResetCategoryShine();
    }

    private void PlayCategoryParticles()
    {
        if (categoryAttentionParticles == null)
        {
            return;
        }

        if (!categoryAttentionParticles.gameObject.activeSelf)
        {
            categoryAttentionParticles.gameObject.SetActive(true);
        }

        categoryAttentionParticles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        categoryAttentionParticles.Play(true);
    }

    private void StopCategoryChipAnimation()
    {
        if (categoryChipAnimation != null)
        {
            StopCoroutine(categoryChipAnimation);
            categoryChipAnimation = null;
        }

        SetCategoryChipFinalState();
        ResetCategoryShine();
    }

    private void ResetCategoryShine()
    {
        if (categoryShineCanvasGroup != null)
        {
            categoryShineCanvasGroup.alpha = 0f;
        }

        if (categoryShineRoot != null)
        {
            categoryShineRoot.anchoredPosition =
                categoryShineBasePosition;

            categoryShineRoot.gameObject.SetActive(false);
        }
    }

    private void SetCategoryChipFinalState()
    {
        if (categoryCanvasGroup != null)
        {
            categoryCanvasGroup.alpha = 1f;
        }

        if (categoryChipRoot != null)
        {
            categoryChipRoot.anchoredPosition =
                categoryChipBasePosition;

            categoryChipRoot.localScale =
                categoryChipBaseScale;
        }
    }

    /// <summary>
    /// Registra el evento de selección del TMP_InputField y conserva el
    /// placeholder original. No requiere asignar una referencia extra
    /// en el Inspector: utiliza answerInput.placeholder.
    /// </summary>
    private void InitializeAnswerInputPlaceholder()
    {
        if (answerInput == null)
        {
            return;
        }

        // Evita listeners duplicados si el método se ejecuta nuevamente.
        answerInput.onSelect.RemoveListener(HandleAnswerInputSelected);
        answerInput.onSelect.AddListener(HandleAnswerInputSelected);

        answerPlaceholderGraphic = answerInput.placeholder;
        answerPlaceholderText = answerPlaceholderGraphic as TMP_Text;

        if (answerPlaceholderText != null)
        {
            answerPlaceholderOriginalText = answerPlaceholderText.text;
        }

        answerPlaceholderInitialized = answerPlaceholderGraphic != null;
        answerPlaceholderDismissed = false;

        RestoreAnswerPlaceholder();
    }

    /// <summary>
    /// Se ejecuta inmediatamente al pulsar o seleccionar el InputField.
    /// El placeholder desaparece antes de que el jugador escriba.
    /// </summary>
    private void HandleAnswerInputSelected(string _)
    {
        if (
            !answerPlaceholderInitialized ||
            answerPlaceholderDismissed
        )
        {
            return;
        }

        answerPlaceholderDismissed = true;

        if (answerPlaceholderText != null)
        {
            // Vaciar el texto es más estable que desactivar el GameObject:
            // TMP_InputField puede seguir actualizando su etiqueta normalmente.
            answerPlaceholderText.text = string.Empty;
        }
        else if (answerPlaceholderGraphic != null)
        {
            answerPlaceholderGraphic.enabled = false;
        }

        answerInput?.ForceLabelUpdate();
    }

    /// <summary>
    /// Restaura el placeholder para la siguiente ronda. Durante una misma
    /// ronda permanece oculto después de la primera selección, aunque el
    /// jugador quite el foco sin haber escrito.
    /// </summary>
    private void RestoreAnswerPlaceholder()
    {
        if (!answerPlaceholderInitialized)
        {
            return;
        }

        answerPlaceholderDismissed = false;

        if (answerPlaceholderGraphic != null)
        {
            answerPlaceholderGraphic.gameObject.SetActive(true);
            answerPlaceholderGraphic.enabled = true;
        }

        if (answerPlaceholderText != null)
        {
            answerPlaceholderText.text = answerPlaceholderOriginalText;
        }

        answerInput?.ForceLabelUpdate();
    }

    private void OnDestroy()
    {
        if (answerInput != null)
        {
            answerInput.onSelect.RemoveListener(HandleAnswerInputSelected);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RefreshUI()
    {
        RefreshStatusUI();
        RefreshHintButtonState();
        RefreshHintCounterState();

        RiddleSO currentRiddle = GameManager.Instance?.GetCurrentRiddle();

        if (currentRiddle?.hints == null)
        {
            Debug.LogError("No hay un acertijo válido para mostrar.");
            return;
        }

        RefreshCategoryChip(currentRiddle);

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
        // El panel de victoria ya comunicó el acierto y reprodujo el sonido.
        // En esta fase se muestran solamente la recompensa y la racha.
        ShowMessage(string.Empty);
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

        // No limpiamos el campo al enviar.
        // Si la respuesta es incorrecta, el jugador puede ver y corregir
        // exactamente lo que había escrito.
        GameManager.Instance?.SubmitAnswer(submittedAnswer);
    }

    /// <summary>
    /// Limpia el campo de respuesta al comenzar una ronda nueva.
    /// No debe llamarse después de un intento incorrecto.
    /// </summary>
    public void ClearAnswerInput()
    {
        if (answerInput == null)
        {
            return;
        }

        answerInput.SetTextWithoutNotify(string.Empty);
        answerInput.caretPosition = 0;
        answerInput.selectionAnchorPosition = 0;
        answerInput.selectionFocusPosition = 0;

        // Cada ronda comienza mostrando nuevamente la indicación.
        // Al primer toque, HandleAnswerInputSelected la oculta de inmediato.
        RestoreAnswerPlaceholder();
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
        categoryChipNeedsAnimation = true;

        // La próxima actualización pertenece a una ronda nueva.
        // Reiniciamos el valor previo para que el contador no haga un pulso
        // simplemente por volver a su cantidad inicial.
        lastDisplayedRemainingHints = -1;

        if (hintCounterRoot != null)
        {
            hintCounterRoot.localScale = hintCounterBaseScale;
        }
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
        // El fondo reacciona siempre a la racha, incluso si el texto visual
        // de racha no estuviera asignado en alguna escena de prueba.
        feverBackground?.SetStreak(streak);

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

    /// <summary>
    /// Muestra el panel de victoria sin aplicar todavía el puntaje ni la racha.
    /// El título "¡Correcto!" debe ser un TMP independiente dentro de la tarjeta.
    /// Este método solo escribe con typewriter "La respuesta era..." y la respuesta.
    /// </summary>
    public bool ShowVictoryPanel(string correctAnswer)
    {
        if (!ValidateResultPanel(
                victoryPanel,
                victoryPanelRoot,
                victoryPanelCanvasGroup,
                victoryAnswerText,
                victoryContinueButton,
                "Victory"
            ))
        {
            return false;
        }

        LockInput(true);
        PlayEffect(correctSFX);

        StartResultPanelEntrance(
            victoryPanel,
            victoryPanelRoot,
            victoryPanelCanvasGroup,
            victoryAnswerText,
            victoryContinueButton,
            BuildAnswerMessage(correctAnswer),
            victoryPanelBaseScale
        );

        return true;
    }

    /// <summary>
    /// Se conecta al botón Continuar del panel de victoria.
    /// Primero reproduce la salida y después aplica la recompensa pendiente.
    /// </summary>
    public void ContinueFromVictory()
    {
        if (resultPanelTransitioning)
        {
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError(
                "No existe un GameManager para continuar la victoria."
            );
            return;
        }

        StartResultPanelExit(
            victoryPanel,
            victoryPanelRoot,
            victoryPanelCanvasGroup,
            victoryContinueButton,
            victoryPanelBaseScale,
            GameManager.Instance.ContinueFromVictory
        );
    }

    /// <summary>
    /// Oculta el panel de victoria inmediatamente.
    /// Se conserva como utilidad; el flujo normal usa ContinueFromVictory().
    /// </summary>
    public void HideVictoryPanel()
    {
        HideResultPanelImmediately(
            victoryPanel,
            victoryPanelRoot,
            victoryPanelCanvasGroup,
            victoryPanelBaseScale
        );
    }

    /// <summary>
    /// Muestra el panel de respuesta incorrecta.
    /// El título "¡Incorrecto!" debe ser un TMP independiente dentro de la tarjeta.
    /// </summary>
    public void ShowRevealPanel(string correctAnswer)
    {
        if (!ValidateResultPanel(
                revealPanel,
                revealPanelRoot,
                revealPanelCanvasGroup,
                correctAnswerText,
                revealContinueButton,
                "Reveal"
            ))
        {
            return;
        }

        LockInput(true);

        StartResultPanelEntrance(
            revealPanel,
            revealPanelRoot,
            revealPanelCanvasGroup,
            correctAnswerText,
            revealContinueButton,
            BuildAnswerMessage(correctAnswer),
            revealPanelBaseScale
        );
    }

    /// <summary>
    /// Conectar este método al botón Continuar del panel de derrota.
    /// Primero reproduce la salida y después continúa el flujo del GameManager.
    /// </summary>
    public void ContinueFromReveal()
    {
        if (resultPanelTransitioning)
        {
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError(
                "No existe un GameManager para continuar desde el panel de derrota."
            );
            return;
        }

        StartResultPanelExit(
            revealPanel,
            revealPanelRoot,
            revealPanelCanvasGroup,
            revealContinueButton,
            revealPanelBaseScale,
            GameManager.Instance.ContinueFromReveal
        );
    }

    /// <summary>
    /// Oculta el panel de derrota inmediatamente.
    /// Se conserva como utilidad; el flujo normal usa ContinueFromReveal().
    /// </summary>
    public void HideRevealPanel()
    {
        HideResultPanelImmediately(
            revealPanel,
            revealPanelRoot,
            revealPanelCanvasGroup,
            revealPanelBaseScale
        );
    }

    private void InitializeResultPanels()
    {
        if (victoryPanelRoot != null)
        {
            victoryPanelBaseScale = victoryPanelRoot.localScale;
        }

        if (revealPanelRoot != null)
        {
            revealPanelBaseScale = revealPanelRoot.localScale;
        }

        HideResultPanelImmediately(
            victoryPanel,
            victoryPanelRoot,
            victoryPanelCanvasGroup,
            victoryPanelBaseScale
        );

        HideResultPanelImmediately(
            revealPanel,
            revealPanelRoot,
            revealPanelCanvasGroup,
            revealPanelBaseScale
        );
    }

    private bool ValidateResultPanel(
        GameObject panel,
        RectTransform panelRoot,
        CanvasGroup panelCanvasGroup,
        TextMeshProUGUI answerText,
        Button continueButton,
        string panelName
    )
    {
        if (panel == null)
        {
            Debug.LogError($"{panelName} Panel no está asignado en UIManager.");
            return false;
        }

        if (panelRoot == null)
        {
            Debug.LogError($"{panelName} Panel Root no está asignado en UIManager.");
            return false;
        }

        if (panelCanvasGroup == null)
        {
            Debug.LogError($"{panelName} Canvas Group no está asignado en UIManager.");
            return false;
        }

        if (answerText == null)
        {
            Debug.LogError($"{panelName} Answer Text no está asignado en UIManager.");
            return false;
        }

        if (continueButton == null)
        {
            Debug.LogError($"{panelName} Continue Button no está asignado en UIManager.");
            return false;
        }

        return true;
    }

    private string BuildAnswerMessage(string correctAnswer)
    {
        return $"La respuesta era:\n{correctAnswer}";
    }

    private void StartResultPanelEntrance(
        GameObject panel,
        RectTransform panelRoot,
        CanvasGroup panelCanvasGroup,
        TextMeshProUGUI answerText,
        Button continueButton,
        string message,
        Vector3 baseScale
    )
    {
        StopResultPanelCoroutines();

        panel.SetActive(true);
        panel.transform.SetAsLastSibling();

        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = true;

        panelRoot.localScale = baseScale * resultPanelStartScale;

        answerText.text = message;
        answerText.maxVisibleCharacters = 0;

        continueButton.interactable = false;
        resultPanelTransitioning = true;

        resultPanelAnimation = StartCoroutine(
            AnimateResultPanelEntrance(
                panelRoot,
                panelCanvasGroup,
                answerText,
                continueButton,
                message,
                baseScale
            )
        );
    }

    private IEnumerator AnimateResultPanelEntrance(
        RectTransform panelRoot,
        CanvasGroup panelCanvasGroup,
        TextMeshProUGUI answerText,
        Button continueButton,
        string message,
        Vector3 baseScale
    )
    {
        float elapsed = 0f;
        float firstPhaseDuration = resultPanelEnterDuration * 0.72f;
        float secondPhaseDuration = Mathf.Max(
            0.01f,
            resultPanelEnterDuration - firstPhaseDuration
        );

        Vector3 startScale = baseScale * resultPanelStartScale;
        Vector3 overshootScale = baseScale * resultPanelOvershootScale;

        while (elapsed < firstPhaseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / firstPhaseDuration);
            float easedProgress = EaseOutBack(progress);

            panelCanvasGroup.alpha = Mathf.Clamp01(progress);
            panelRoot.localScale = Vector3.LerpUnclamped(
                startScale,
                overshootScale,
                easedProgress
            );

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < secondPhaseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / secondPhaseDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

            panelCanvasGroup.alpha = 1f;
            panelRoot.localScale = Vector3.Lerp(
                overshootScale,
                baseScale,
                easedProgress
            );

            yield return null;
        }

        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.interactable = true;
        panelRoot.localScale = baseScale;

        resultPanelAnimation = null;
        resultPanelTransitioning = false;

        resultTypewriterAnimation = StartCoroutine(
            TypewriterRoutine(
                answerText,
                continueButton,
                message
            )
        );
    }

    private IEnumerator TypewriterRoutine(
        TextMeshProUGUI targetText,
        Button continueButton,
        string message
    )
    {
        targetText.text = message;
        targetText.maxVisibleCharacters = 0;
        targetText.ForceMeshUpdate();

        if (typewriterStartDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(typewriterStartDelay);
        }

        int totalCharacters = targetText.textInfo.characterCount;

        for (int visibleCharacters = 1;
             visibleCharacters <= totalCharacters;
             visibleCharacters++)
        {
            targetText.maxVisibleCharacters = visibleCharacters;

            char currentCharacter = GetVisibleCharacter(
                targetText,
                visibleCharacters - 1
            );

            float delay = typewriterCharacterDelay;

            if (
                currentCharacter == ':' ||
                currentCharacter == '.' ||
                currentCharacter == '!' ||
                currentCharacter == '?' ||
                currentCharacter == '\n'
            )
            {
                delay += typewriterPunctuationDelay;
            }

            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }
            else
            {
                yield return null;
            }
        }

        targetText.maxVisibleCharacters = int.MaxValue;
        continueButton.interactable = true;
        resultTypewriterAnimation = null;
    }

    private static char GetVisibleCharacter(
        TextMeshProUGUI targetText,
        int characterInfoIndex
    )
    {
        if (
            targetText == null ||
            targetText.textInfo == null ||
            characterInfoIndex < 0 ||
            characterInfoIndex >= targetText.textInfo.characterCount
        )
        {
            return '\0';
        }

        int stringIndex =
            targetText.textInfo.characterInfo[characterInfoIndex].index;

        if (
            stringIndex < 0 ||
            stringIndex >= targetText.text.Length
        )
        {
            return '\0';
        }

        return targetText.text[stringIndex];
    }

    private void StartResultPanelExit(
        GameObject panel,
        RectTransform panelRoot,
        CanvasGroup panelCanvasGroup,
        Button continueButton,
        Vector3 baseScale,
        Action onComplete
    )
    {
        if (
            panel == null ||
            panelRoot == null ||
            panelCanvasGroup == null
        )
        {
            onComplete?.Invoke();
            return;
        }

        StopResultPanelCoroutines();

        continueButton.interactable = false;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = true;
        resultPanelTransitioning = true;

        resultPanelAnimation = StartCoroutine(
            AnimateResultPanelExit(
                panel,
                panelRoot,
                panelCanvasGroup,
                baseScale,
                onComplete
            )
        );
    }

    private IEnumerator AnimateResultPanelExit(
        GameObject panel,
        RectTransform panelRoot,
        CanvasGroup panelCanvasGroup,
        Vector3 baseScale,
        Action onComplete
    )
    {
        float elapsed = 0f;
        Vector3 startScale = panelRoot.localScale;
        Vector3 endScale = baseScale * resultPanelStartScale;
        float startAlpha = panelCanvasGroup.alpha;

        while (elapsed < resultPanelExitDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(
                elapsed / resultPanelExitDuration
            );
            float easedProgress = EaseInBack(progress);

            panelRoot.localScale = Vector3.LerpUnclamped(
                startScale,
                endScale,
                easedProgress
            );

            panelCanvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                0f,
                progress
            );

            yield return null;
        }

        HideResultPanelImmediately(
            panel,
            panelRoot,
            panelCanvasGroup,
            baseScale
        );

        resultPanelAnimation = null;
        resultPanelTransitioning = false;

        onComplete?.Invoke();
    }

    private void HideResultPanelImmediately(
        GameObject panel,
        RectTransform panelRoot,
        CanvasGroup panelCanvasGroup,
        Vector3 baseScale
    )
    {
        if (panelRoot != null)
        {
            panelRoot.localScale = baseScale;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void StopResultPanelCoroutines()
    {
        if (resultPanelAnimation != null)
        {
            StopCoroutine(resultPanelAnimation);
            resultPanelAnimation = null;
        }

        if (resultTypewriterAnimation != null)
        {
            StopCoroutine(resultTypewriterAnimation);
            resultTypewriterAnimation = null;
        }
    }

    private static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = value - 1f;

        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static float EaseInBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return c3 * value * value * value -
               c1 * value * value;
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

    private void InitializeHintCounter()
    {
        if (hintCounterRoot != null)
        {
            hintCounterBaseScale = hintCounterRoot.localScale;
        }

        if (hintCounterText != null)
        {
            hintCounterText.text = "0";
        }

        lastDisplayedRemainingHints = -1;
    }

    private void RefreshHintCounterState()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        int remainingHints = GameManager.Instance.GetRemainingHintCount();

        bool shouldPulse =
            lastDisplayedRemainingHints >= 0 &&
            remainingHints < lastDisplayedRemainingHints;

        if (hintCounterText != null)
        {
            hintCounterText.text = remainingHints.ToString();
            hintCounterText.color =
                remainingHints > 0
                    ? hintCounterAvailableTextColor
                    : hintCounterUnavailableTextColor;
        }

        if (hintCounterGraphic != null)
        {
            // Usa exactamente los mismos tintes que el botón Pedir pista,
            // para que ambos se apaguen de forma coherente al llegar a cero.
            hintCounterGraphic.color =
                remainingHints > 0
                    ? availableHintColor
                    : unavailableHintColor;
        }

        if (hintCounterRoot != null)
        {
            if (hintCounterPulseAnimation != null)
            {
                StopCoroutine(hintCounterPulseAnimation);
                hintCounterPulseAnimation = null;
            }

            hintCounterRoot.localScale = hintCounterBaseScale;

            if (shouldPulse)
            {
                hintCounterPulseAnimation =
                    StartCoroutine(AnimateHintCounterPulse());
            }
        }

        lastDisplayedRemainingHints = remainingHints;
    }

    private IEnumerator AnimateHintCounterPulse()
    {
        if (hintCounterRoot == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < hintCounterPulseDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(
                elapsed / hintCounterPulseDuration
            );

            // Sube y vuelve a bajar en una sola curva suave.
            float pulse = Mathf.Sin(progress * Mathf.PI);
            float scaleMultiplier = Mathf.Lerp(
                1f,
                hintCounterPulseScale,
                pulse
            );

            hintCounterRoot.localScale =
                hintCounterBaseScale * scaleMultiplier;

            yield return null;
        }

        hintCounterRoot.localScale = hintCounterBaseScale;
        hintCounterPulseAnimation = null;
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