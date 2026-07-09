using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    private RectTransform targetRect;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void OnEnable()
    {
        targetRect = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        Rect currentSafeArea = Screen.safeArea;
        Vector2Int currentScreenSize =
            new Vector2Int(Screen.width, Screen.height);

        if (currentSafeArea != lastSafeArea ||
            currentScreenSize != lastScreenSize)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        if (targetRect == null ||
            Screen.width <= 0 ||
            Screen.height <= 0)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        targetRect.anchorMin = anchorMin;
        targetRect.anchorMax = anchorMax;
        targetRect.offsetMin = Vector2.zero;
        targetRect.offsetMax = Vector2.zero;

        lastSafeArea = safeArea;
        lastScreenSize =
            new Vector2Int(Screen.width, Screen.height);
    }
}