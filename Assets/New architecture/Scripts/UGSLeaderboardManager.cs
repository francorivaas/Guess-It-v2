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

    private Task initializationTask;

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

            Debug.Log(
                $"UGS listo. Login anónimo correcto. Player ID: {PlayerId}"
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

        try
        {
            await Task.Yield();

            var playerEntry =
                await LeaderboardsService.Instance.AddPlayerScoreAsync(
                    leaderboardId,
                    score
                );

            Debug.Log(
                $"Score de run enviado: {score} | Score guardado en leaderboard: {playerEntry.Score} | Rank: {playerEntry.Rank}"
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

        try
        {
            await Task.Yield();

            var scoresResponse =
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
                $"El jugador todavía no tiene score en '{leaderboardId}' o no se pudo leer: {exception.Message}"
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
        string safeName = entry.PlayerName;

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Jugador";
        }

        return new LeaderboardDisplayEntry
        {
            // UGS usa rank base 0. Para el jugador mostramos base 1.
            rank = entry.Rank + 1,
            playerId = entry.PlayerId,
            playerName = safeName,
            score = Mathf.RoundToInt((float)entry.Score),
            isCurrentPlayer =
                !string.IsNullOrWhiteSpace(currentPlayerId) &&
                entry.PlayerId == currentPlayerId
        };
    }
}