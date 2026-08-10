using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Riddles")]
    [SerializeField] private RiddleDatabaseSO database;

    [Header("Run Settings")]
    [SerializeField] private int startingLives = 3;
    [SerializeField] private int initialHintCount = 3;

    [Tooltip("Si está desactivado, un acertijo nunca se repite dentro de la misma run.")]
    [SerializeField] private bool allowRepeatsAfterAllRiddlesHaveAppeared = false;

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int CurrentStreak { get; private set; }

    private readonly List<RiddleSO> availableRiddles = new List<RiddleSO>();
    private readonly HashSet<RiddleSO> usedRiddlesThisRun = new HashSet<RiddleSO>();

    private RiddleSO currentRiddle;
    private int currentHintCount;

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

    private void StartNewRun()
    {
        Score = 0;
        Lives = startingLives;
        CurrentStreak = 0;

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

    private bool TryInitializeRiddles()
    {
        if (database == null)
        {
            Debug.LogError("GameManager: no hay RiddleDatabaseSO asignada.");
            return false;
        }

        if (database.riddles == null || database.riddles.Count == 0)
        {
            Debug.LogError("GameManager: la base de acertijos está vacía.");
            return false;
        }

        RefillAvailableRiddlesForCurrentRun();

        if (availableRiddles.Count == 0)
        {
            Debug.LogError("GameManager: no hay acertijos válidos disponibles.");
            return false;
        }

        return true;
    }

    private void RefillAvailableRiddlesForCurrentRun()
    {
        availableRiddles.Clear();

        if (database == null || database.riddles == null)
        {
            return;
        }

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

    public void NextRound()
    {
        if (runFinished)
        {
            return;
        }

        if (availableRiddles.Count == 0)
        {
            RefillAvailableRiddlesForCurrentRun();
        }

        if (availableRiddles.Count == 0)
        {
            HandleAllRiddlesUsed();
            return;
        }

        UIManager.Instance?.ClearCards();
        UIManager.Instance?.ClearAnswerInput();

        int randomIndex = Random.Range(0, availableRiddles.Count);

        currentRiddle = availableRiddles[randomIndex];

        // Punto clave:
        // el acertijo se remueve de la pool disponible y se guarda como usado
        // para que no pueda volver a aparecer durante esta run.
        availableRiddles.RemoveAt(randomIndex);
        usedRiddlesThisRun.Add(currentRiddle);

        currentHintCount = Mathf.Min(initialHintCount, GetTotalHintCount());

        UIManager.Instance?.RefreshUI();
    }

    private void HandleAllRiddlesUsed()
    {
        if (allowRepeatsAfterAllRiddlesHaveAppeared)
        {
            usedRiddlesThisRun.Clear();
            RefillAvailableRiddlesForCurrentRun();

            if (availableRiddles.Count > 0)
            {
                NextRound();
                return;
            }
        }

        runFinished = true;

        Debug.LogWarning(
            "GameManager: no quedan acertijos sin usar en esta run."
        );

        UIManager.Instance?.ClearCards();
        UIManager.Instance?.ClearAnswerInput();
        UIManager.Instance?.ShowMessage(
            "¡Completaste todos los acertijos disponibles!"
        );
    }

    public void RequestHint()
    {
        if (runFinished || currentRiddle == null)
        {
            return;
        }

        if (!HasMoreHints())
        {
            return;
        }

        currentHintCount++;
        HapticManager.HeavyVibration();
        UIManager.Instance?.RefreshUI();
    }

    public void SubmitAnswer(string submittedAnswer)
    {
        if (
            runFinished ||
            currentRiddle == null ||
            string.IsNullOrWhiteSpace(submittedAnswer)
        )
        {
            return;
        }

        string normalizedSubmittedAnswer =
            NormalizeAnswer(submittedAnswer);

        string normalizedCorrectAnswer =
            NormalizeAnswer(currentRiddle.answer);

        if (normalizedSubmittedAnswer == normalizedCorrectAnswer)
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
        pendingCorrectPoints = CalculateScore();
        hasPendingCorrectReward = true;

        bool victoryPanelShown =
            UIManager.Instance != null &&
            UIManager.Instance.ShowVictoryPanel(currentRiddle.answer);

        if (!victoryPanelShown)
        {
            ContinueFromVictory();
        }
    }

    public void ContinueFromVictory()
    {
        if (!hasPendingCorrectReward)
        {
            BeginNextRoundAfterCorrectAnswer();
            return;
        }

        int gainedPoints = pendingCorrectPoints;

        Score += gainedPoints;
        CurrentStreak++;

        hasPendingCorrectReward = false;
        pendingCorrectPoints = 0;

        UIManager.Instance?.UpdateStreakUI(CurrentStreak);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowCorrectFeedback(
                Score,
                gainedPoints,
                BeginNextRoundAfterCorrectAnswer
            );
        }
        else
        {
            BeginNextRoundAfterCorrectAnswer();
        }
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

    private void HandleIncorrectAnswer()
    {
        Lives--;
        CurrentStreak = 0;

        HapticManager.HeavyVibration();

        UIManager.Instance?.UpdateStreakUI(CurrentStreak);
        UIManager.Instance?.TriggerFailureFeedback();

        bool noLivesLeft = Lives <= 0;
        bool noMoreHints = !HasMoreHints();

        if (noLivesLeft || noMoreHints)
        {
            UIManager.Instance?.RefreshStatusUI();
            UIManager.Instance?.ShowRevealPanel(currentRiddle.answer);
            return;
        }

        currentHintCount++;

        UIManager.Instance?.RefreshUI();
        UIManager.Instance?.TriggerErrorShake();
    }

    public void ContinueFromReveal()
    {
        if (Lives <= 0)
        {
            EndRun();
            return;
        }

        NextRound();
    }

    private void EndRun()
    {
        runFinished = true;

        // Por ahora conserva tu comportamiento actual:
        // al terminar la run vuelve al menú principal.
        SceneManager.LoadScene(0);
    }

    public RiddleSO GetCurrentRiddle()
    {
        return currentRiddle;
    }

    public int GetCurrentHintCount()
    {
        return currentHintCount;
    }

    public int GetRemainingHintCount()
    {
        return Mathf.Max(0, GetTotalHintCount() - currentHintCount);
    }

    public bool HasMoreHints()
    {
        return currentRiddle != null &&
               currentRiddle.hints != null &&
               currentHintCount < currentRiddle.hints.Count();
    }

    private int GetTotalHintCount()
    {
        if (currentRiddle == null || currentRiddle.hints == null)
        {
            return 0;
        }

        return currentRiddle.hints.Count();
    }

    private int CalculateScore()
    {
        int baseScore = 100 + CurrentStreak * 50;

        int extraHintsUsed =
            Mathf.Max(0, currentHintCount - initialHintCount);

        if (extraHintsUsed == 1)
        {
            return Mathf.RoundToInt(baseScore * 0.75f);
        }

        if (extraHintsUsed >= 2)
        {
            return Mathf.RoundToInt(baseScore * 0.5f);
        }

        return baseScore;
    }

    private static string NormalizeAnswer(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Trim()
            .ToLowerInvariant();
    }
}