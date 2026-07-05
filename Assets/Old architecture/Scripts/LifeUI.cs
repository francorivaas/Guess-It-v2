using UnityEngine;
using TMPro;

public class LifeUI : MonoBehaviour
{
    [Header("Duolingo Style UI")]
    [SerializeField] private TextMeshProUGUI livesCountText; // El texto que mostrará el número (ej: "3")

    /// <summary>
    /// Actualiza el contador numérico de vidas en la pantalla.
    /// </summary>
    public void UpdateLives(int currentLives)
    {
        if (livesCountText != null)
        {
            livesCountText.text = currentLives.ToString();
        }
    }
}