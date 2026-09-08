using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

public class UGSLeaderboardManager : MonoBehaviour
{
    public static UGSLeaderboardManager Instance { get; private set; }

    [Serializable]
    public sealed class LeaderboardDisplayEntry
    {
        public int rank;
        public string playerId;
        public string playerName;
        public int score;
        public bool isCurrentPlayer;
    }

    [Header("Leaderboard")]
    [SerializeField] private string leaderboardId = "guessit_high_score";

    [Header("Environment")]
    [SerializeField] private string environmentName = "production";

    [Header("Player Alias")]
    [Tooltip("Si está activo, el juego crea un alias propio para el ranking sin pedir registro ni datos personales.")]
    [SerializeField] private bool createGuessItAliasAutomatically = true;

    [Tooltip("Nombre de respaldo si todavía no se pudo leer o crear el alias.")]
    [SerializeField] private string fallbackPlayerName = "Jugador";

    public bool IsReady { get; private set; }

    public string PlayerId
    {
        get
        {
            if (
                AuthenticationService.Instance != null &&
                AuthenticationService.Instance.IsSignedIn
            )
            {
                return AuthenticationService.Instance.PlayerId;
            }

            return string.Empty;
        }
    }

    public string CurrentPlayerName
    {
        get
        {
            if (
                AuthenticationService.Instance != null &&
                AuthenticationService.Instance.IsSignedIn &&
                !string.IsNullOrWhiteSpace(AuthenticationService.Instance.PlayerName)
            )
            {
                return AuthenticationService.Instance.PlayerName;
            }

            return cachedPlayerName;
        }
    }

    private const string GuessItAliasCreatedPlayerPrefsKey =
        "GuessItAliasCreated";

    private const string GuessItAliasBasePlayerPrefsKey =
        "GuessItAliasBaseName";

    private static readonly string[] AliasPrefixes =
    {
        "Dulce",
        "Pixel",
        "Luna",
        "Mago",
        "Chispa",
        "Rayo",
        "Nube",
        "Mega",
        "Turbo",
        "Cosmico",
        "Brillante",
        "Feliz",
        "Bravo",
        "Rapido",
        "Candy",
        "Genio"
    };

    private static readonly string[] AliasSuffixes =
    {
        "Zorro",
        "Tigre",
        "Koala",
        "Dragon",
        "Menta",
        "Estrella",
        "Caramelo",
        "Riddle",
        "Cometa",
        "Trueno",
        "Panda",
        "Robot",
        "Goma",
        "Ninja",
        "Oraculo",
        "Globo"
    };

    private Task initializationTask;
    private bool playerAliasCheckedThisSession;
    private string cachedPlayerName = string.Empty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        StartInitialization();
    }

    private async void StartInitialization()
    {
        await InitializeAsync();
    }

    public Task InitializeAsync()
    {
        if (initializationTask != null)
        {
            return initializationTask;
        }

        initializationTask = InitializeInternalAsync();
        return initializationTask;
    }

    private async Task InitializeInternalAsync()
    {
        try
        {
            IsReady = false;

            InitializationOptions options = new InitializationOptions();

            if (!string.IsNullOrWhiteSpace(environmentName))
            {
                options.SetEnvironmentName(environmentName);
            }

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            IsReady = true;

            if (createGuessItAliasAutomatically)
            {
                await EnsurePlayerAliasInternalAsync(false);
            }

            Debug.Log(
                $"UGS listo. Login anónimo correcto. " +
                $"Player ID: {PlayerId} | Nombre: {CurrentPlayerName}"
            );
        }
        catch (Exception exception)
        {
            IsReady = false;
            initializationTask = null;

            Debug.LogError(
                $"No se pudo inicializar Unity Gaming Services: {exception}"
            );
        }
    }

    public async Task<string> EnsurePlayerAliasAsync()
    {
        await InitializeAsync();

        if (!IsReady)
        {
            return GetFallbackPlayerName();
        }

        return await EnsurePlayerAliasInternalAsync(false);
    }

    public async Task<string> GenerateNewRandomPlayerAliasAsync()
    {
        await InitializeAsync();

        if (!IsReady)
        {
            return GetFallbackPlayerName();
        }

        return await EnsurePlayerAliasInternalAsync(true);
    }

    public async Task<string> GetCurrentPlayerNameAsync()
    {
        await InitializeAsync();

        if (!IsReady)
        {
            return GetFallbackPlayerName();
        }

        if (!string.IsNullOrWhiteSpace(cachedPlayerName))
        {
            return cachedPlayerName;
        }

        try
        {
            string playerName = await AuthenticationService.Instance
                .GetPlayerNameAsync(false);

            if (!string.IsNullOrWhiteSpace(playerName))
            {
                cachedPlayerName = playerName;
                return cachedPlayerName;
            }
        }
        catch (Exception exception)
        {
            Debug.Log(
                $"No se pudo leer el nombre actual del jugador: {exception.Message}"
            );
        }

        return GetFallbackPlayerName();
    }

    private async Task<string> EnsurePlayerAliasInternalAsync(bool forceNewAlias)
    {
        if (
            AuthenticationService.Instance == null ||
            !AuthenticationService.Instance.IsSignedIn
        )
        {
            return GetFallbackPlayerName();
        }

        if (
            !forceNewAlias &&
            playerAliasCheckedThisSession &&
            !string.IsNullOrWhiteSpace(cachedPlayerName)
        )
        {
            return cachedPlayerName;
        }

        bool aliasWasCreatedByGuessIt =
            PlayerPrefs.GetInt(GuessItAliasCreatedPlayerPrefsKey, 0) == 1;

        if (!forceNewAlias && aliasWasCreatedByGuessIt)
        {
            string existingPlayerName = AuthenticationService.Instance.PlayerName;

            if (string.IsNullOrWhiteSpace(existingPlayerName))
            {
                try
                {
                    existingPlayerName = await AuthenticationService.Instance
                        .GetPlayerNameAsync(false);
                }
                catch (Exception exception)
                {
                    Debug.Log(
                        $"No se pudo leer el alias existente: {exception.Message}"
                    );
                }
            }

            if (!string.IsNullOrWhiteSpace(existingPlayerName))
            {
                cachedPlayerName = existingPlayerName;
                playerAliasCheckedThisSession = true;
                return cachedPlayerName;
            }
        }

        string generatedAlias = GenerateRandomAliasBase();
        return await UpdatePlayerAliasInternalAsync(generatedAlias);
    }

    private async Task<string> UpdatePlayerAliasInternalAsync(string aliasBase)
    {
        string safeAliasBase = SanitizeAliasBase(aliasBase);

        if (string.IsNullOrWhiteSpace(safeAliasBase))
        {
            safeAliasBase = GenerateRandomAliasBase();
        }

        try
        {
            string updatedPlayerName = await AuthenticationService.Instance
                .UpdatePlayerNameAsync(safeAliasBase);

            if (string.IsNullOrWhiteSpace(updatedPlayerName))
            {
                updatedPlayerName = AuthenticationService.Instance.PlayerName;
            }

            if (string.IsNullOrWhiteSpace(updatedPlayerName))
            {
                updatedPlayerName = safeAliasBase;
            }

            cachedPlayerName = updatedPlayerName;
            playerAliasCheckedThisSession = true;

            PlayerPrefs.SetInt(GuessItAliasCreatedPlayerPrefsKey, 1);
            PlayerPrefs.SetString(GuessItAliasBasePlayerPrefsKey, safeAliasBase);
            PlayerPrefs.Save();

            Debug.Log($"Alias de ranking actualizado: {cachedPlayerName}");

            return cachedPlayerName;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"No se pudo actualizar el alias de ranking: {exception.Message}"
            );

            string fallbackName = AuthenticationService.Instance.PlayerName;

            if (string.IsNullOrWhiteSpace(fallbackName))
            {
                fallbackName = PlayerPrefs.GetString(
                    GuessItAliasBasePlayerPrefsKey,
                    GetFallbackPlayerName()
                );
            }

            cachedPlayerName = fallbackName;
            playerAliasCheckedThisSession = true;

            return cachedPlayerName;
        }
    }

    private static string GenerateRandomAliasBase()
    {
        string prefix = AliasPrefixes[UnityEngine.Random.Range(0, AliasPrefixes.Length)];
        string suffix = AliasSuffixes[UnityEngine.Random.Range(0, AliasSuffixes.Length)];

        return $"{prefix}{suffix}";
    }

    private static string SanitizeAliasBase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmedValue = value.Trim();
        List<char> acceptedCharacters = new List<char>(trimmedValue.Length);

        foreach (char character in trimmedValue)
        {
            if (char.IsLetterOrDigit(character))
            {
                acceptedCharacters.Add(character);
            }
        }

        string safeValue = new string(acceptedCharacters.ToArray());

        if (safeValue.Length > 20)
        {
            safeValue = safeValue.Substring(0, 20);
        }

        return safeValue;
    }

    private string GetFallbackPlayerName()
    {
        if (!string.IsNullOrWhiteSpace(fallbackPlayerName))
        {
            return fallbackPlayerName;
        }

        return "Jugador";
    }

    public async Task SubmitScoreAsync(int score)
    {
        if (score <= 0)
        {
            Debug.Log("No se envía score porque es 0 o negativo.");
            return;
        }

        await InitializeAsync();

        if (!IsReady)
        {
            Debug.LogWarning(
                "No se pudo enviar el score porque UGS no está listo."
            );
            return;
        }

        if (createGuessItAliasAutomatically)
        {
            await EnsurePlayerAliasInternalAsync(false);
        }

        try
        {
            await Task.Yield();

            LeaderboardEntry playerEntry =
                await LeaderboardsService.Instance.AddPlayerScoreAsync(
                    leaderboardId,
                    score
                );

            Debug.Log(
                $"Score de run enviado: {score} | " +
                $"Score guardado en leaderboard: {playerEntry.Score} | " +
                $"Rank: {playerEntry.Rank} | " +
                $"Nombre: {CurrentPlayerName}"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Error enviando score al leaderboard '{leaderboardId}': {exception}"
            );
        }
    }

    public async Task<List<LeaderboardDisplayEntry>> GetTopScoresAsync(
        int limit = 10
    )
    {
        List<LeaderboardDisplayEntry> entries =
            new List<LeaderboardDisplayEntry>();

        await InitializeAsync();

        if (!IsReady)
        {
            Debug.LogWarning(
                "No se pudo cargar el ranking porque UGS no está listo."
            );
            return entries;
        }

        if (createGuessItAliasAutomatically)
        {
            await EnsurePlayerAliasInternalAsync(false);
        }

        try
        {
            await Task.Yield();

            LeaderboardScoresPage scoresResponse =
                await LeaderboardsService.Instance.GetScoresAsync(
                    leaderboardId,
                    new GetScoresOptions
                    {
                        Offset = 0,
                        Limit = Mathf.Max(1, limit)
                    }
                );

            if (scoresResponse == null || scoresResponse.Results == null)
            {
                return entries;
            }

            foreach (LeaderboardEntry entry in scoresResponse.Results)
            {
                entries.Add(ConvertEntry(entry));
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Error cargando Top Scores del leaderboard '{leaderboardId}': {exception}"
            );
        }

        return entries;
    }

    public async Task<LeaderboardDisplayEntry> GetCurrentPlayerScoreAsync()
    {
        await InitializeAsync();

        if (!IsReady)
        {
            Debug.LogWarning(
                "No se pudo cargar el score personal porque UGS no está listo."
            );
            return null;
        }

        if (createGuessItAliasAutomatically)
        {
            await EnsurePlayerAliasInternalAsync(false);
        }

        try
        {
            await Task.Yield();

            LeaderboardEntry playerEntry =
                await LeaderboardsService.Instance.GetPlayerScoreAsync(
                    leaderboardId
                );

            return ConvertEntry(playerEntry);
        }
        catch (Exception exception)
        {
            Debug.Log(
                $"El jugador todavía no tiene score en '{leaderboardId}' " +
                $"o no se pudo leer: {exception.Message}"
            );

            return null;
        }
    }

    private LeaderboardDisplayEntry ConvertEntry(LeaderboardEntry entry)
    {
        if (entry == null)
        {
            return null;
        }

        string currentPlayerId = PlayerId;
        bool isCurrentPlayer =
            !string.IsNullOrWhiteSpace(currentPlayerId) &&
            entry.PlayerId == currentPlayerId;

        string safeName = entry.PlayerName;

        if (isCurrentPlayer && !string.IsNullOrWhiteSpace(cachedPlayerName))
        {
            safeName = cachedPlayerName;
        }

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = GetFallbackPlayerName();
        }

        return new LeaderboardDisplayEntry
        {
            // UGS usa rank base 0. Para el jugador mostramos base 1.
            rank = entry.Rank + 1,
            playerId = entry.PlayerId,
            playerName = safeName,
            score = Mathf.RoundToInt((float)entry.Score),
            isCurrentPlayer = isCurrentPlayer
        };
    }
}
