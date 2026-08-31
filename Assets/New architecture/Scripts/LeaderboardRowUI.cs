using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardRowUI : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Current Player Highlight")]
    [SerializeField] private Graphic backgroundGraphic;
    [SerializeField]
    private Color normalBackgroundColor =
        new Color(1f, 1f, 1f, 0.08f);

    [SerializeField]
    private Color currentPlayerBackgroundColor =
        new Color(1f, 0.85f, 0.15f, 0.35f);

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Range(0.1f, 1f)] private float normalAlpha = 0.9f;
    [SerializeField, Range(0.1f, 1f)] private float currentPlayerAlpha = 1f;

    public void Setup(UGSLeaderboardManager.LeaderboardDisplayEntry entry)
    {
        if (entry == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (rankText != null)
        {
            rankText.text = $"#{entry.rank}";
        }

        if (playerNameText != null)
        {
            playerNameText.text = entry.playerName;
        }

        if (scoreText != null)
        {
            scoreText.text = entry.score.ToString();
        }

        if (backgroundGraphic != null)
        {
            backgroundGraphic.color =
                entry.isCurrentPlayer
                    ? currentPlayerBackgroundColor
                    : normalBackgroundColor;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha =
                entry.isCurrentPlayer
                    ? currentPlayerAlpha
                    : normalAlpha;
        }
    }
}