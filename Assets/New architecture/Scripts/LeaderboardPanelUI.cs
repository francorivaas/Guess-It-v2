using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Panel Animation")]
    [Tooltip("RectTransform visual que hace el pop. Normalmente es Canvas/Ranking/RankingPanel.")]
    [SerializeField] private RectTransform animatedRoot;

    [Tooltip("CanvasGroup del panel visual. Si queda vacío, el script intenta encontrarlo o crearlo automáticamente.")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [SerializeField, Range(0.1f, 1f)] private float popStartScale = 0.72f;
    [SerializeField, Range(1f, 1.3f)] private float popOvershootScale = 1.08f;
    [SerializeField, Min(0.05f)] private float popInDuration = 0.28f;
    [SerializeField, Min(0.05f)] private float closeDuration = 0.16f;

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
    private Coroutine panelAnimation;
    private Vector3 animatedRootBaseScale = Vector3.one;

    private void Awake()
    {
        CacheAnimationReferences();

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

    private void CacheAnimationReferences()
    {
        if (animatedRoot == null && panelRoot != null)
        {
            Transform rankingPanelChild = panelRoot.transform.Find("RankingPanel");

            if (rankingPanelChild != null)
            {
                animatedRoot = rankingPanelChild as RectTransform;
            }
            else
            {
                animatedRoot = panelRoot.GetComponent<RectTransform>();
            }
        }

        if (animatedRoot != null)
        {
            animatedRootBaseScale = animatedRoot.localScale;

            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = animatedRoot.GetComponent<CanvasGroup>();
            }

            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = animatedRoot.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    public void OpenPanel()
    {
        CacheAnimationReferences();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
        }

        if (panelAnimation != null)
        {
            StopCoroutine(panelAnimation);
            panelAnimation = null;
        }

        panelAnimation = StartCoroutine(AnimatePanelOpen());
        RefreshPanel();
    }

    public void ClosePanel()
    {
        if (panelAnimation != null)
        {
            StopCoroutine(panelAnimation);
            panelAnimation = null;
        }

        if (!gameObject.activeInHierarchy)
        {
            HidePanelImmediately();
            return;
        }

        panelAnimation = StartCoroutine(AnimatePanelClose());
    }

    private IEnumerator AnimatePanelOpen()
    {
        if (animatedRoot == null)
        {
            panelAnimation = null;
            yield break;
        }

        Vector3 startScale = animatedRootBaseScale * popStartScale;
        Vector3 overshootScale = animatedRootBaseScale * popOvershootScale;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = true;
        }

        animatedRoot.localScale = startScale;

        float firstPhaseDuration = popInDuration * 0.72f;
        float secondPhaseDuration = Mathf.Max(
            0.01f,
            popInDuration - firstPhaseDuration
        );

        float elapsed = 0f;

        while (elapsed < firstPhaseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / firstPhaseDuration);
            float easedProgress = EaseOutBack(progress);

            animatedRoot.localScale = Vector3.LerpUnclamped(
                startScale,
                overshootScale,
                easedProgress
            );

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = Mathf.Clamp01(progress);
            }

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < secondPhaseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / secondPhaseDuration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

            animatedRoot.localScale = Vector3.Lerp(
                overshootScale,
                animatedRootBaseScale,
                easedProgress
            );

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 1f;
            }

            yield return null;
        }

        animatedRoot.localScale = animatedRootBaseScale;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        panelAnimation = null;
    }

    private IEnumerator AnimatePanelClose()
    {
        if (animatedRoot == null)
        {
            HidePanelImmediately();
            yield break;
        }

        Vector3 startScale = animatedRoot.localScale;
        Vector3 endScale = animatedRootBaseScale * 0.92f;
        float startAlpha = panelCanvasGroup != null
            ? panelCanvasGroup.alpha
            : 1f;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = true;
        }

        float elapsed = 0f;

        while (elapsed < closeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / closeDuration);
            float easedProgress = EaseInBack(progress);

            animatedRoot.localScale = Vector3.LerpUnclamped(
                startScale,
                endScale,
                easedProgress
            );

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    0f,
                    progress
                );
            }

            yield return null;
        }

        HidePanelImmediately();
        panelAnimation = null;
    }

    private void HidePanelImmediately()
    {
        if (animatedRoot != null)
        {
            animatedRoot.localScale = animatedRootBaseScale;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

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
            $"Tu puesto: #{playerEntry.rank} | Tu score: {playerEntry.score}"
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
}