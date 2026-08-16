using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Rows")]
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private LeaderboardRowUI rowPrefab;
    [SerializeField, Min(1)] private int topLimit = 10;

    [Header("Status Texts")]
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI emptyText;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private TextMeshProUGUI playerSummaryText;

    [Header("Buttons")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button closeButton;

    private readonly List<LeaderboardRowUI> spawnedRows =
        new List<LeaderboardRowUI>();

    private bool isLoading;

    private void Awake()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshPanel);
            refreshButton.onClick.AddListener(RefreshPanel);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
            closeButton.onClick.AddListener(ClosePanel);
        }

        SetLoadingState(false);
        SetEmptyState(false);
        SetErrorMessage(string.Empty);
        SetPlayerSummary(string.Empty);
    }

    public void OpenPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        RefreshPanel();
    }

    public void ClosePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public async void RefreshPanel()
    {
        if (isLoading)
        {
            return;
        }

        isLoading = true;

        ClearRows();
        SetLoadingState(true);
        SetEmptyState(false);
        SetErrorMessage(string.Empty);
        SetPlayerSummary(string.Empty);

        if (refreshButton != null)
        {
            refreshButton.interactable = false;
        }

        try
        {
            if (UGSLeaderboardManager.Instance == null)
            {
                SetErrorMessage(
                    "No se encontró el servicio de ranking."
                );

                return;
            }

            List<UGSLeaderboardManager.LeaderboardDisplayEntry> topEntries =
                await UGSLeaderboardManager.Instance.GetTopScoresAsync(
                    topLimit
                );

            UGSLeaderboardManager.LeaderboardDisplayEntry playerEntry =
                await UGSLeaderboardManager.Instance
                    .GetCurrentPlayerScoreAsync();

            RenderTopEntries(topEntries);
            RenderPlayerSummary(playerEntry);

            bool hasEntries = topEntries != null && topEntries.Count > 0;
            SetEmptyState(!hasEntries);
        }
        catch (System.Exception exception)
        {
            SetErrorMessage(
                $"No se pudo cargar el ranking.\n{exception.Message}"
            );
        }
        finally
        {
            SetLoadingState(false);

            if (refreshButton != null)
            {
                refreshButton.interactable = true;
            }

            isLoading = false;
        }
    }

    private void RenderTopEntries(
        List<UGSLeaderboardManager.LeaderboardDisplayEntry> entries
    )
    {
        if (
            entries == null ||
            entries.Count == 0 ||
            rowsContainer == null ||
            rowPrefab == null
        )
        {
            return;
        }

        foreach (UGSLeaderboardManager.LeaderboardDisplayEntry entry in entries)
        {
            LeaderboardRowUI row =
                Instantiate(rowPrefab, rowsContainer);

            row.Setup(entry);
            spawnedRows.Add(row);
        }
    }

    private void RenderPlayerSummary(
        UGSLeaderboardManager.LeaderboardDisplayEntry playerEntry
    )
    {
        if (playerEntry == null)
        {
            SetPlayerSummary(
                "Jugá una partida para aparecer en el ranking."
            );

            return;
        }

        SetPlayerSummary(
            $"Tu puesto: #{playerEntry.rank}   |   Tu score: {playerEntry.score}"
        );
    }

    private void ClearRows()
    {
        foreach (LeaderboardRowUI row in spawnedRows)
        {
            if (row != null)
            {
                Destroy(row.gameObject);
            }
        }

        spawnedRows.Clear();
    }

    private void SetLoadingState(bool visible)
    {
        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(visible);
            loadingText.text = visible ? "Cargando ranking..." : string.Empty;
        }
    }

    private void SetEmptyState(bool visible)
    {
        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(visible);
            emptyText.text = visible
                ? "Todavía no hay puntajes."
                : string.Empty;
        }
    }

    private void SetErrorMessage(string message)
    {
        if (errorText == null)
        {
            return;
        }

        bool hasMessage = !string.IsNullOrWhiteSpace(message);

        errorText.gameObject.SetActive(hasMessage);
        errorText.text = message;
    }

    private void SetPlayerSummary(string message)
    {
        if (playerSummaryText == null)
        {
            return;
        }

        playerSummaryText.text = message;
    }
}