using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class BannerSlot : MonoBehaviour
{
    private bool registered;

    private void OnEnable() => TryRegister();

    private void Update()
    {
        if (!registered) TryRegister();
    }

    private void TryRegister()
    {
        if (registered || AdsManager.Instance == null) return;
        AdsManager.Instance.AddBannerSlot(this);
        registered = true;
    }

    private void OnDisable()
    {
        if (!registered) return;
        registered = false;
        if (AdsManager.Instance != null) AdsManager.Instance.RemoveBannerSlot(this);
    }

    public Rect GetScreenRect()
    {
        var rect = (RectTransform)transform;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        var canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;

        Vector2 a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
}
