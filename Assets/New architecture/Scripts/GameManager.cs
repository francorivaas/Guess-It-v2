using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuration")]
    [SerializeField] private RiddleDatabaseSO database;
    [SerializeField, Min(1)] private int startingLives = 3;
    [SerializeField, Min(1)] private int initialHintCount = 3;

    public int Score { get; private set; }
    public int Lives { get; private set; }
    public int CurrentStreak { get; private set; }

    private RiddleSO currentRiddle;
    private readonly List<RiddleSO> availableRiddles = new List<RiddleSO>();
    private int currentHintCount;

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
        Score = 0;
        Lives = startingLives;
        CurrentStreak = 0;

        if (!TryInitializeRiddles())
        {
            enabled = false;
            return;
        }

        NextRound();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
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
        if (availableRiddles.Count == 0)
        {
            RefillAvailableRiddles();
        }

        if (availableRiddles.Count == 0)
        {
            Debug.LogError("No hay acertijos válidos disponibles.");
            return;
        }

        UIManager.Instance?.ClearCards();

        int randomIndex = Random.Range(0, availableRiddles.Count);
        currentRiddle = availableRiddles[randomIndex];
        availableRiddles.RemoveAt(randomIndex);

        currentHintCount = Mathf.Min(initialHintCount, GetTotalHintCount());

        UIManager.Instance?.RefreshUI();
    }

    public void RequestHint()
    {
        if (currentRiddle == null || currentHintCount >= GetTotalHintCount())
        {
            return;
        }

        currentHintCount++;
        HapticManager.HeavyVibration();
        UIManager.Instance?.RefreshUI();
    }

    public void SubmitAnswer(string playerAnswer)
    {
        if (currentRiddle == null || string.IsNullOrWhiteSpace(playerAnswer))
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
        int gainedPoints = CalculateScore();

        Score += gainedPoints;
        CurrentStreak++;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateStreakUI(CurrentStreak);

            // La siguiente ronda se genera cuando termina por completo
            // la animación de transferencia de puntaje.
            UIManager.Instance.ShowCorrectFeedback(
                Score,
                gainedPoints,
                NextRound
            );

            return;
        }

        // Respaldo para escenas o pruebas sin UIManager.
        NextRound();
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
            //UIManager.Instance.ShowMessage(
            //    "Incorrecto. Pierdes 1 vida.\n¡Aquí tienes una pista extra!"
            //);
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
            if (riddle != null)
            {
                availableRiddles.Add(riddle);
            }
        }
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

    public void ContinueFromReveal()
    {
        UIManager.Instance?.HideRevealPanel();

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
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    public bool HasMoreHints()
    {
        return currentRiddle != null &&
               currentHintCount < GetTotalHintCount();
    }
}
