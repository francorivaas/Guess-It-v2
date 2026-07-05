using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Riddle Database")]
    [SerializeField] private RiddleDatabaseSO database;

    [Header("Game State")]
    public int score = 0;
    public int lives = 3;
    public int currentStreak = 0;

    private RiddleSO currentRiddle;
    private List<RiddleSO> availableRiddles;

    // Representa cuántas pistas están visibles actualmente.
    private int currentHintCount = 3;

    private const int InitialHintCount = 3;

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
        if (!ValidateDatabase())
        {
            enabled = false;
            return;
        }

        availableRiddles = new List<RiddleSO>();

        RefillAvailableRiddles();
        NextRound();
    }

    /// <summary>
    /// Devuelve el acertijo que está activo actualmente.
    /// </summary>
    public RiddleSO GetCurrentRiddle()
    {
        return currentRiddle;
    }

    /// <summary>
    /// Devuelve cuántas pistas deben mostrarse.
    /// </summary>
    public int GetCurrentHintCount()
    {
        return currentHintCount;
    }

    /// <summary>
    /// Selecciona un nuevo acertijo que todavía no apareció
    /// durante el ciclo actual.
    /// </summary>
    public void NextRound()
    {
        if (!ValidateDatabase())
        {
            return;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClearCards();
        }

        if (availableRiddles == null)
        {
            availableRiddles = new List<RiddleSO>();
        }

        // Modo endless:
        // cuando todos los acertijos aparecieron, restauramos la lista.
        if (availableRiddles.Count == 0)
        {
            RefillAvailableRiddles();
        }

        if (availableRiddles.Count == 0)
        {
            Debug.LogError(
                "No hay acertijos válidos disponibles en la base de datos."
            );

            return;
        }

        int randomIndex = Random.Range(
            0,
            availableRiddles.Count
        );

        currentRiddle = availableRiddles[randomIndex];
        availableRiddles.RemoveAt(randomIndex);

        int totalHints = GetTotalHintCount();

        currentHintCount = Mathf.Min(
            InitialHintCount,
            totalHints
        );

        if (UIManager.Instance != null)
        {
            UIManager.Instance.RefreshUI();
        }
    }

    /// <summary>
    /// Muestra una pista adicional, siempre que todavía quede alguna.
    /// </summary>
    public void RequestHint()
    {
        if (currentRiddle == null)
        {
            return;
        }

        int totalHints = GetTotalHintCount();

        if (currentHintCount >= totalHints)
        {
            return;
        }

        HapticManager.HeavyVibration();

        currentHintCount++;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.RefreshUI();
        }
    }

    /// <summary>
    /// Evalúa la respuesta escrita por el jugador.
    /// </summary>
    public void SubmitAnswer(string playerAnswer)
    {
        if (currentRiddle == null)
        {
            Debug.LogError(
                "No hay ningún acertijo activo."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(playerAnswer))
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

#if UNITY_ANDROID && !UNITY_EDITOR
        HapticManager.HeavyVibration();
        Debug.Log("Vibration");
#endif
    }

    /// <summary>
    /// Comprueba la respuesta principal y todas las variantes aceptadas.
    /// </summary>
    private bool IsCorrectAnswer(string playerAnswer)
    {
        string normalizedPlayerAnswer =
            NormalizeText(playerAnswer);

        if (string.IsNullOrEmpty(normalizedPlayerAnswer))
        {
            return false;
        }

        // Primero comprobamos la respuesta principal.
        if (
            normalizedPlayerAnswer ==
            NormalizeText(currentRiddle.answer)
        )
        {
            return true;
        }

        // Después comprobamos las variantes aceptadas.
        if (currentRiddle.acceptedAnswers == null)
        {
            return false;
        }

        foreach (
            string acceptedAnswer
            in currentRiddle.acceptedAnswers
        )
        {
            if (
                normalizedPlayerAnswer ==
                NormalizeText(acceptedAnswer)
            )
            {
                return true;
            }
        }

        return false;
    }

    private void HandleCorrectAnswer()
    {
        int roundScore = CalculateScore();

        score += roundScore;
        currentStreak++;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage(
                $"¡Correcto! +{roundScore}"
            );

            UIManager.Instance.UpdateStreakUI(
                currentStreak
            );
        }

        NextRound();
    }

    private void HandleIncorrectAnswer()
    {
        lives--;
        currentStreak = 0;

        HapticManager.HeavyVibration();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateStreakUI(
                currentStreak
            );

            UIManager.Instance.TriggerFailureJuice();
        }

        int totalHints = GetTotalHintCount();
        bool hasNoMoreHints =
            currentHintCount >= totalHints;

        if (lives <= 0 || hasNoMoreHints)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowRevealPanel(
                    currentRiddle.answer
                );
            }

            return;
        }

        // Fallar también revela automáticamente una pista.
        currentHintCount++;

        string failMessage =
            "Incorrecto. Pierdes 1 vida." +
            "\n¡Aquí tienes una pista extra!";

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowMessage(
                failMessage
            );

            UIManager.Instance.RefreshUI();
            UIManager.Instance.TriggerErrorShake();
        }
    }

    /// <summary>
    /// Calcula los puntos según la racha y las pistas usadas.
    /// </summary>
    private int CalculateScore()
    {
        float maximumRoundScore =
            100f + currentStreak * 50f;

        float hintMultiplier = 1f;

        if (currentHintCount == 4)
        {
            hintMultiplier = 0.75f;
        }
        else if (currentHintCount >= 5)
        {
            hintMultiplier = 0.50f;
        }

        return Mathf.RoundToInt(
            maximumRoundScore * hintMultiplier
        );
    }

    /// <summary>
    /// Normaliza una respuesta para comparar mayúsculas,
    /// espacios y caracteres con tilde.
    /// </summary>
    private string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        string normalizedText = text
            .Trim()
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        StringBuilder result = new StringBuilder();

        foreach (char character in normalizedText)
        {
            UnicodeCategory category =
                CharUnicodeInfo.GetUnicodeCategory(
                    character
                );

            if (
                category !=
                UnicodeCategory.NonSpacingMark
            )
            {
                result.Append(character);
            }
        }

        return result
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Devuelve la cantidad real de pistas del acertijo actual.
    /// </summary>
    private int GetTotalHintCount()
    {
        if (
            currentRiddle == null ||
            currentRiddle.hints == null
        )
        {
            return 0;
        }

        return currentRiddle.hints.Length;
    }

    /// <summary>
    /// Restaura la lista de acertijos disponibles,
    /// eliminando referencias vacías.
    /// </summary>
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

    private bool ValidateDatabase()
    {
        if (database == null)
        {
            Debug.LogError(
                "RiddleDatabaseSO no está asignada en el GameManager."
            );

            return false;
        }

        if (
            database.riddles == null ||
            database.riddles.Count == 0
        )
        {
            Debug.LogError(
                "RiddleDatabaseSO no contiene acertijos."
            );

            return false;
        }

        return true;
    }

    public void ContinueFromReveal()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideRevealPanel();
        }

        if (lives <= 0)
        {
            SceneManager.LoadScene(0);
        }
        else
        {
            NextRound();
        }
    }

    private void GameOver()
    {
        enabled = false;

        int currentHighScore =
            PlayerPrefs.GetInt("HighScore", 0);

        bool isNewHighScore =
            score > currentHighScore;

        if (isNewHighScore)
        {
            PlayerPrefs.SetInt(
                "HighScore",
                score
            );

            PlayerPrefs.Save();

            currentHighScore = score;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameOverScreen(
                score,
                currentHighScore,
                isNewHighScore
            );
        }
    }

    public void RestartGame()
    {
        enabled = true;
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }
}