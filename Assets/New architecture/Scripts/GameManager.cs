using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public const string HighScorePlayerPrefsKey = "HighScore";

    public static GameManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private RiddleDatabaseSO database;
    [SerializeField, Min(1)] private int startingLives = 3;
    [SerializeField, Min(1)] private int initialHintCount = 3;

    [Header("Scenes")]
    [Tooltip("Nombre exacto de la escena del menú principal. Debe estar agregada en Build Settings.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Tooltip("Se usa como respaldo si mainMenuSceneName está vacío o falla.")]
    [SerializeField, Min(0)] private int fallbackMainMenuBuildIndex = 0;

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int CurrentStreak { get; private set; }
    public int HighScore { get; private set; }

    private RiddleSO currentRiddle;
    private readonly List<RiddleSO> availableRiddles = new List<RiddleSO>();
    private readonly HashSet<RiddleSO> usedRiddlesThisRun = new HashSet<RiddleSO>();

    private int currentHintCount;

    // Recompensa que permanece pendiente mientras el panel de victoria está abierto.
    private bool hasPendingCorrectReward;
    private int pendingCorrectPoints;
    private bool runFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartNewRun();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            TrySaveHighScore();
        }
    }

    private void OnApplicationQuit()
    {
        TrySaveHighScore();
    }

    private void StartNewRun()
    {
        Score = 0;
        Lives = startingLives;
        CurrentStreak = 0;
        HighScore = PlayerPrefs.GetInt(HighScorePlayerPrefsKey, 0);

        currentRiddle = null;
        currentHintCount = 0;
        hasPendingCorrectReward = false;
        pendingCorrectPoints = 0;
        runFinished = false;

        usedRiddlesThisRun.Clear();
        availableRiddles.Clear();

        if (!TryInitializeRiddles())
        {
            enabled = false;
            return;
        }

        UIManager.Instance?.RequestCategoryAttention();
        NextRound();
    }

    public RiddleSO GetCurrentRiddle()
    {
        return currentRiddle;
    }

    public int GetCurrentHintCount()
    {
        return currentHintCount;
    }

    public void NextRound()
    {
        if (runFinished)
        {
            return;
        }

        if (availableRiddles.Count == 0)
        {
            RefillAvailableRiddles();
        }

        if (availableRiddles.Count == 0)
        {
            HandleAllRiddlesUsed();
            return;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClearCards();
            UIManager.Instance.ClearAnswerInput();
        }

        int randomIndex = UnityEngine.Random.Range(0, availableRiddles.Count);
        currentRiddle = availableRiddles[randomIndex];

        // Evita que este acertijo vuelva a aparecer durante esta run.
        availableRiddles.RemoveAt(randomIndex);
        usedRiddlesThisRun.Add(currentRiddle);

        currentHintCount = Mathf.Min(initialHintCount, GetTotalHintCount());

        UIManager.Instance?.RefreshUI();
    }

    public void RequestHint()
    {
        if (runFinished || currentRiddle == null || currentHintCount >= GetTotalHintCount())
        {
            return;
        }

        currentHintCount++;
        HapticManager.HeavyVibration();
        UIManager.Instance?.RefreshUI();
    }

    public void SubmitAnswer(string playerAnswer)
    {
        if (runFinished || currentRiddle == null || string.IsNullOrWhiteSpace(playerAnswer))
        {
            return;
        }

        if (IsCorrectAnswer(playerAnswer))
        {
            HandleCorrectAnswer();
        }
        else
        {
            HandleIncorrectAnswer();
        }
    }

    private void HandleCorrectAnswer()
    {
        if (hasPendingCorrectReward)
        {
            return;
        }

        // Todavía no se modifica Score ni CurrentStreak.
        // La recompensa queda pendiente hasta pulsar Continuar.
        pendingCorrectPoints = CalculateScore();
        hasPendingCorrectReward = true;

        if (
            UIManager.Instance != null &&
            UIManager.Instance.ShowVictoryPanel(currentRiddle.answer)
        )
        {
            return;
        }

        // Respaldo para escenas o pruebas sin panel de victoria.
        ContinueFromVictory();
    }

    private void HandleIncorrectAnswer()
    {
        Lives--;
        CurrentStreak = 0;

        HapticManager.HeavyVibration();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateStreakUI(CurrentStreak);
            UIManager.Instance.TriggerFailureFeedback();
        }

        bool noMoreHints = currentHintCount >= GetTotalHintCount();

        if (Lives <= 0 || noMoreHints)
        {
            UIManager.Instance?.RefreshStatusUI();
            UIManager.Instance?.ShowRevealPanel(currentRiddle.answer);
            return;
        }

        currentHintCount++;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.RefreshUI();
            UIManager.Instance.TriggerErrorShake();
        }
    }

    private bool IsCorrectAnswer(string playerAnswer)
    {
        string normalizedPlayerAnswer = NormalizeText(playerAnswer);

        if (normalizedPlayerAnswer == NormalizeText(currentRiddle.answer))
        {
            return true;
        }

        if (currentRiddle.acceptedAnswers == null)
        {
            return false;
        }

        foreach (string acceptedAnswer in currentRiddle.acceptedAnswers)
        {
            if (string.IsNullOrWhiteSpace(acceptedAnswer))
            {
                continue;
            }

            if (normalizedPlayerAnswer == NormalizeText(acceptedAnswer))
            {
                return true;
            }
        }

        return false;
    }

    private int CalculateScore()
    {
        float maximumRoundScore = 100f + CurrentStreak * 50f;
        float multiplier = 1f;

        if (currentHintCount == initialHintCount + 1)
        {
            multiplier = 0.75f;
        }
        else if (currentHintCount >= initialHintCount + 2)
        {
            multiplier = 0.50f;
        }

        return Mathf.RoundToInt(maximumRoundScore * multiplier);
    }

    private int GetTotalHintCount()
    {
        return currentRiddle?.hints?.Length ?? 0;
    }

    private bool TryInitializeRiddles()
    {
        if (database == null)
        {
            Debug.LogError("RiddleDatabaseSO no está asignada en GameManager.");
            return false;
        }

        if (database.riddles == null || database.riddles.Count == 0)
        {
            Debug.LogError("RiddleDatabaseSO no contiene acertijos.");
            return false;
        }

        RefillAvailableRiddles();

        if (availableRiddles.Count == 0)
        {
            Debug.LogError("La base de datos solo contiene referencias vacías.");
            return false;
        }

        return true;
    }

    private void RefillAvailableRiddles()
    {
        availableRiddles.Clear();

        foreach (RiddleSO riddle in database.riddles)
        {
            if (riddle == null)
            {
                continue;
            }

            if (usedRiddlesThisRun.Contains(riddle))
            {
                continue;
            }

            availableRiddles.Add(riddle);
        }
    }

    private void HandleAllRiddlesUsed()
    {
        runFinished = true;
        TrySaveHighScore();

        Debug.LogWarning("No quedan acertijos sin usar en esta run.");

        UIManager.Instance?.ClearCards();
        UIManager.Instance?.ClearAnswerInput();
        UIManager.Instance?.ShowMessage("¡Completaste todos los acertijos disponibles!");
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string decomposedText = text
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        StringBuilder result = new StringBuilder();

        foreach (char character in decomposedText)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category != UnicodeCategory.NonSpacingMark)
            {
                result.Append(character);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Confirma el panel de victoria, aplica la recompensa pendiente
    /// y comienza la secuencia visual de puntos y racha.
    /// </summary>
    public void ContinueFromVictory()
    {
        if (!hasPendingCorrectReward)
        {
            return;
        }

        int gainedPoints = pendingCorrectPoints;

        hasPendingCorrectReward = false;
        pendingCorrectPoints = 0;

        // El UIManager ya terminó la animación de salida del panel.
        // Recién en este momento se aplica la recompensa real.
        Score += gainedPoints;
        CurrentStreak++;
        TrySaveHighScore();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateStreakUI(CurrentStreak);

            // Al terminar la transferencia de puntos comienza la próxima ronda.
            UIManager.Instance.ShowCorrectFeedback(
                Score,
                gainedPoints,
                BeginNextRoundAfterCorrectAnswer
            );

            return;
        }

        // Respaldo para escenas sin UIManager.
        NextRound();
    }

    private void BeginNextRoundAfterCorrectAnswer()
    {
        if (runFinished)
        {
            return;
        }

        UIManager.Instance?.RequestCategoryAttention();
        NextRound();
    }

    public void ContinueFromReveal()
    {
        // El UIManager ya terminó la animación de salida del panel.
        if (Lives <= 0)
        {
            GoToMainMenu();
            return;
        }

        NextRound();
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        TrySaveHighScore();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        TrySaveHighScore();
        LoadMainMenuScene();
    }

    private bool TrySaveHighScore()
    {
        if (Score <= HighScore)
        {
            return false;
        }

        HighScore = Score;
        PlayerPrefs.SetInt(HighScorePlayerPrefsKey, HighScore);
        PlayerPrefs.Save();
        return true;
    }

    private void LoadMainMenuScene()
    {
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            try
            {
                SceneManager.LoadScene(mainMenuSceneName);
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"No se pudo cargar la escena '{mainMenuSceneName}'. " +
                    $"Se usará el Build Index {fallbackMainMenuBuildIndex}.\n{exception.Message}"
                );
            }
        }

        SceneManager.LoadScene(fallbackMainMenuBuildIndex);
    }

    public int GetRemainingHintCount()
    {
        if (currentRiddle == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            GetTotalHintCount() - currentHintCount
        );
    }

    public bool HasMoreHints()
    {
        return currentRiddle != null &&
               currentHintCount < GetTotalHintCount();
    }
}