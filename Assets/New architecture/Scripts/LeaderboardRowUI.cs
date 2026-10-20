using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardRowUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Readability")]
    [SerializeField, Min(8f)] private float rankFontSize = 30f;
    [SerializeField, Min(8f)] private float playerNameFontSize = 29f;
    [SerializeField, Min(8f)] private float scoreFontSize = 30f;
    [SerializeField] private Color textColor = new Color(0.07f, 0.18f, 0.27f, 1f);
    [SerializeField] private Color currentPlayerTextColor = new Color(0.09f, 0.16f, 0.24f, 1f);

    [Header("Current Player Highlight")]
    [SerializeField] private Graphic backgroundGraphic;
    [SerializeField]
    private Color normalBackgroundColor =
        new Color(1f, 1f, 1f, 0.13f);
    [SerializeField]
    private Color currentPlayerBackgroundColor =
        new Color(1f, 0.85f, 0.15f, 0.42f);

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Range(0.1f, 1f)] private float normalAlpha = 1f;
    [SerializeField, Range(0.1f, 1f)] private float currentPlayerAlpha = 1f;

    public void Setup(UGSLeaderboardManager.LeaderboardDisplayEntry entry)
    {
        if (entry == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        Color foreground = entry.isCurrentPlayer ? currentPlayerTextColor : textColor;

        ConfigureText(rankText, rankFontSize, foreground, TextAlignmentOptions.Center);
        ConfigureText(playerNameText, playerNameFontSize, foreground, TextAlignmentOptions.MidlineLeft);
        ConfigureText(scoreText, scoreFontSize, foreground, TextAlignmentOptions.MidlineRight);

        if (rankText != null)
            rankText.text = $"#{entry.rank}";

        if (playerNameText != null)
        {
            // TextMeshPro recorta visualmente el final si el alias no entra.
            // Conservamos el nombre completo como dato y evitamos saltos de línea.
            playerNameText.text = string.IsNullOrWhiteSpace(entry.playerName)
                ? "Jugador"
                : entry.playerName;
            playerNameText.enableWordWrapping = false;
            playerNameText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (scoreText != null)
            scoreText.text = entry.score.ToString("N0");

        if (backgroundGraphic != null)
            backgroundGraphic.color = entry.isCurrentPlayer
                ? currentPlayerBackgroundColor
                : normalBackgroundColor;

        if (canvasGroup != null)
            canvasGroup.alpha = entry.isCurrentPlayer
                ? currentPlayerAlpha
                : normalAlpha;
    }

    private static void ConfigureText(
        TextMeshProUGUI target,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        if (target == null)
            return;

        target.fontSize = fontSize;
        target.enableAutoSizing = false;
        target.fontStyle = FontStyles.Normal;
        target.color = color;
        target.alignment = alignment;
        target.enableWordWrapping = false;
        target.raycastTarget = false;
        target.margin = Vector4.zero;
    }
}
