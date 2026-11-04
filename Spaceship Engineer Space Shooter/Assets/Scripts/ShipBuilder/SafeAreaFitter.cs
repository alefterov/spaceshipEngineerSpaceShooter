using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    [Tooltip("Which edges keep clear of the unsafe parts. Off = that edge stays at the very edge of the screen (e.g. keep Bottom off " +
             "if a panel should reach down under the gesture bar).")]
    public bool top = true, bottom = true, left = true, right = true;

    private RectTransform rect;
    private Rect appliedArea;
    private Vector2Int appliedScreen;

    private void Awake() => rect = (RectTransform)transform;

    private void OnEnable() => Apply();

    private void Update()
    {
        if (Screen.safeArea != appliedArea || Screen.width != appliedScreen.x || Screen.height != appliedScreen.y) Apply();
    }

    private void Apply()
    {
        if (rect == null) rect = (RectTransform)transform;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Rect area = Screen.safeArea;
        appliedArea = area;
        appliedScreen = new Vector2Int(Screen.width, Screen.height);

        Vector2 min = new(area.xMin / Screen.width, area.yMin / Screen.height);
        Vector2 max = new(area.xMax / Screen.width, area.yMax / Screen.height);

        if (!left) min.x = 0f;
        if (!bottom) min.y = 0f;
        if (!right) max.x = 1f;
        if (!top) max.y = 1f;

        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
