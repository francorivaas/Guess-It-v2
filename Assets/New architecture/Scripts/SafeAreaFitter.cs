using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    private RectTransform cachedRectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        cachedRectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void OnEnable()
    {
        if (cachedRectTransform == null)
        {
            cachedRectTransform = GetComponent<RectTransform>();
        }

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
        if (cachedRectTransform == null ||
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

        cachedRectTransform.anchorMin = anchorMin;
        cachedRectTransform.anchorMax = anchorMax;
        cachedRectTransform.offsetMin = Vector2.zero;
        cachedRectTransform.offsetMax = Vector2.zero;

        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(
            Screen.width,
            Screen.height
        );
    }
}