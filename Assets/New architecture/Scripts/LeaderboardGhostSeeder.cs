using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Leaderboards;
using UnityEngine;

public class LeaderboardGhostSeeder : MonoBehaviour
{
    [Header("Leaderboard")]
    [SerializeField] private string leaderboardId = "guessit_high_score";
    [SerializeField] private string environmentName = "production";

    [Header("Ghost Users")]
    [SerializeField, Min(1)] private int ghostCount = 9;
    [SerializeField] private string ghostProfilePrefix = "ghost";
    [SerializeField] private string ghostNamePrefix = "Fantasma";

    [Header("Scores")]
    [SerializeField] private int minimumScore = 100;
    [SerializeField] private int maximumScore = 10000;

    [ContextMenu("Seed Ghost Users")]
    private async void SeedGhostUsersFromContextMenu()
    {
        await SeedGhostUsersAsync();
    }

    public async Task SeedGhostUsersAsync()
    {
        string originalProfile = "default";

        try
        {
            await EnsureUnityServicesInitializedAsync();

            originalProfile = AuthenticationService.Instance.Profile;

            if (string.IsNullOrWhiteSpace(originalProfile))
            {
                originalProfile = "default";
            }

            Debug.Log($"Perfil original: {originalProfile}");

            for (int i = 1; i <= ghostCount; i++)
            {
                string profileName = $"{ghostProfilePrefix}_{i:00}";
                string playerName = $"{ghostNamePrefix}{i:00}";
                int score = UnityEngine.Random.Range(
                    minimumScore,
                    maximumScore + 1
                );

                await SignInAsProfileAsync(profileName);

                try
                {
                    await AuthenticationService.Instance.UpdatePlayerNameAsync(
                        playerName
                    );
                }
                catch (Exception nameException)
                {
                    Debug.LogWarning(
                        $"No se pudo cambiar el nombre de {profileName}: {nameException.Message}"
                    );
                }

                var entry =
                    await LeaderboardsService.Instance.AddPlayerScoreAsync(
                        leaderboardId,
                        score
                    );

                Debug.Log(
                    $"Ghost creado | Profile: {profileName} | PlayerId: {AuthenticationService.Instance.PlayerId} | Name: {AuthenticationService.Instance.PlayerName} | Score enviado: {score} | Score guardado: {entry.Score} | Rank: {entry.Rank}"
                );
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Error creando usuarios fantasma: {exception}"
            );
        }
        finally
        {
            await ReturnToOriginalProfileAsync(originalProfile);
        }
    }

    private async Task EnsureUnityServicesInitializedAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            return;
        }

        InitializationOptions options = new InitializationOptions();

        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            options.SetEnvironmentName(environmentName);
        }

        await UnityServices.InitializeAsync(options);
    }

    private async Task SignInAsProfileAsync(string profileName)
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            // No borramos credenciales. Así no perdemos la sesión de ese perfil.
            AuthenticationService.Instance.SignOut(false);
        }

        AuthenticationService.Instance.SwitchProfile(profileName);

        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log(
            $"Login ghost correcto | Profile: {profileName} | PlayerId: {AuthenticationService.Instance.PlayerId}"
        );
    }

    private async Task ReturnToOriginalProfileAsync(string originalProfile)
    {
        try
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut(false);
            }

            AuthenticationService.Instance.SwitchProfile(originalProfile);

            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log(
                $"Volvimos al perfil original | Profile: {originalProfile} | PlayerId: {AuthenticationService.Instance.PlayerId}"
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"No se pudo volver al perfil original automáticamente: {exception.Message}"
            );
        }
    }
}