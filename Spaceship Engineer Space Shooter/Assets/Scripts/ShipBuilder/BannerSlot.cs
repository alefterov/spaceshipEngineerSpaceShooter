using UnityEngine;

/// <summary>
/// Marks a place on the screen where the banner ad lives: put it on an object with a RectTransform (your BanerContainer). The banner is shown
/// while any active object has this component and hides when the last one goes away; it takes the WIDTH of this rectangle and sits in the
/// middle of it. The banner itself is a native view floating over the game (it is not a UI object), so the rectangle is just its reserved
/// place — give it a background or leave it empty, make it as wide as the banner should be and about 50-90 dp tall (a canvas unit is about one
/// dp at the reference resolution). Use it on screens that may carry a banner (main menu, level map) and NOT on the ship builder or the battle.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BannerSlot : MonoBehaviour
{
    private bool registered;

    private void OnEnable() => TryRegister();

    // The AdsManager may not exist yet when this object wakes up (Unity does not order the Awake of different objects): keep trying.
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

    /// <summary>This slot's rectangle in screen pixels (the origin at the bottom left).</summary>
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
