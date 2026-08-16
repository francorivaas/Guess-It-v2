using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Leaderboards;
using UnityEngine;

public class UGSLeaderboardManager : MonoBehaviour
{
    public static UGSLeaderboardManager Instance { get; private set; }

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
            // Pequeña espera para evitar problemas de timing en Unity 6
            // justo después del login anónimo.
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

    //[ContextMenu("Test Submit 1000")]
    //private async void TestSubmit1000()
    //{
    //    await SubmitScoreAsync(1000);
    //}

    //[ContextMenu("Test Submit Random Score")]
    //private async void TestSubmitRandomScore()
    //{
    //    int randomScore = UnityEngine.Random.Range(100, 10000);
    //    await SubmitScoreAsync(randomScore);
    //}
}