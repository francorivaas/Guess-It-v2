using TMPro;
using UnityEngine;

/// <summary>
/// Permite enviar la respuesta desde el teclado (Enter / Intro / Done / Confirmar)
/// sin reemplazar la lógica existente del botón "Responder".
///
/// Debe agregarse al mismo GameObject que contiene el TMP_InputField de respuesta.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_InputField))]
public sealed class AnswerInputSubmitHandler : MonoBehaviour
{
    private TMP_InputField inputField;

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();

        // La respuesta es de una sola línea. De esta forma Enter / Done
        // se interpreta como confirmación y no como un salto de línea.
        inputField.lineType = TMP_InputField.LineType.SingleLine;
    }

    private void OnEnable()
    {
        if (inputField == null)
        {
            inputField = GetComponent<TMP_InputField>();
        }

        // RemoveListener evita duplicados si el objeto se desactiva y vuelve a activar.
        inputField.onSubmit.RemoveListener(HandleInputSubmitted);
        inputField.onSubmit.AddListener(HandleInputSubmitted);
    }

    private void OnDisable()
    {
        if (inputField != null)
        {
            inputField.onSubmit.RemoveListener(HandleInputSubmitted);
        }
    }

    private void HandleInputSubmitted(string submittedText)
    {
        // No enviamos intentos vacíos desde el teclado.
        if (string.IsNullOrWhiteSpace(submittedText))
        {
            return;
        }

        if (UIManager.Instance == null)
        {
            Debug.LogWarning(
                "No se pudo enviar la respuesta desde el teclado porque no existe UIManager."
            );
            return;
        }

        // Reutiliza exactamente el mismo flujo que el botón "Responder".
        UIManager.Instance.SubmitAnswer();
    }
}
