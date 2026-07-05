using UnityEngine;
using TMPro; // Necesario para leer el InputField

public class KeyboardDodger : MonoBehaviour
{
    private RectTransform rectTransform;
    private Vector2 originalAnchoredPosition;

    [Tooltip("El contenedor que quieres mover (BottomUiContainer)")]
    public RectTransform uiToMove;

    [Tooltip("Arrastra aquí tu InputField (answerInput)")]
    public TMP_InputField myInputField;

    [Tooltip("La posición Y a la que subirá cuando el usuario escriba")]
    public float activeYPosition = 400f; // Ajusta este valor según tu pantalla

    [Tooltip("Velocidad de la animación")]
    public float moveSpeed = 10f;

    void Start()
    {
        rectTransform = uiToMove != null ? uiToMove : GetComponent<RectTransform>();
        originalAnchoredPosition = rectTransform.anchoredPosition;
    }

    void Update()
    {
        // La clave del éxito: verificamos directamente si el InputField tiene el foco
        bool isTyping = myInputField != null && myInputField.isFocused;

        // Si está escribiendo, vamos a la posición alta. Si no, volvemos a la original.
        Vector2 targetPosition = isTyping
            ? new Vector2(originalAnchoredPosition.x, activeYPosition)
            : originalAnchoredPosition;

        // Animación suave para que no salte de golpe
        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            targetPosition,
            Time.deltaTime * moveSpeed
        );
    }
}