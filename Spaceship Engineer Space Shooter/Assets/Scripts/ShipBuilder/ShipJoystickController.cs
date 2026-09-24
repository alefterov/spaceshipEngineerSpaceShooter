using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Floating joystick: press ANYWHERE on this UI element and drag — the ship moves toward wherever the
/// stick points (see ShipMovement.SetInput), at a strength proportional to how far the drag has gone
/// (capped at Max Handle Distance, 0..1). The joystick has no fixed screen position — wherever the
/// pointer goes down becomes its center for that gesture.
///
/// Driven by uGUI's own pointer events (OnPointerDown/OnDrag/OnPointerUp) rather than raw
/// Touchscreen/Mouse polling — this means it only reacts to presses that actually raycast-hit THIS
/// element, so a button rendered on top of it (a later sibling) correctly steals its own tap instead
/// of also starting the joystick underneath it. Put this on the movement zone's own Image (can be
/// full-screen or smaller), with other HUD buttons as LATER siblings so they render — and raycast —
/// above it.
///
/// SETUP: put on a UI Image covering the movement zone, Raycast Target ON. Joystick Base/Handle are
/// optional — assign them for a visible stick that appears at the touch point; movement works even
/// without them.
/// </summary>
public class ShipJoystickController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public ShipMovement shipMovement;

    [Header("Visuals (optional)")]
    [Tooltip("Moved to the press point and shown while held.")]
    public RectTransform joystickBase;
    [Tooltip("Moved within Max Handle Distance of the base, toward the pointer.")]
    public RectTransform joystickHandle;
    public float maxHandleDistance = 100f;

    // int.MinValue (not a real pointer id) means "not tracking a gesture" — PointerEventData.pointerId
    // is -1 for the mouse and a small non-negative index per touch, so this never collides with either.
    private int activePointerId = int.MinValue;
    private Vector2 centerScreenPos;
    private Canvas canvas;

    private void Awake() => canvas = GetComponentInParent<Canvas>();

    private void OnEnable() => SetVisualsActive(false);

    private void OnDisable() => ForceRelease();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue) return; // already tracking a different finger — ignore a second one

        activePointerId = eventData.pointerId;
        centerScreenPos = eventData.position;

        PlaceAtScreenPoint(joystickBase, centerScreenPos);
        SetVisualsActive(true);

        UpdateStick(centerScreenPos);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        UpdateStick(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId) return;
        ForceRelease();
    }

    private void Update()
    {
        // Safety net: uGUI's OnPointerUp isn't 100% guaranteed to fire for every release — this
        // project already hit exactly that class of bug once before (see BlockButtonDragHandle's own
        // doc comment). If nothing is pressed at all anymore but we still think we're tracking a
        // gesture, something went wrong — release rather than leave the ship thrusting forever in
        // whatever direction the stick was last pointing.
        if (activePointerId != int.MinValue && !AnyPointerPressed()) ForceRelease();
    }

    private static bool AnyPointerPressed()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) return true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
        return false;
    }

    private void UpdateStick(Vector2 screenPos)
    {
        Vector2 offset = screenPos - centerScreenPos;
        Vector2 clamped = Vector2.ClampMagnitude(offset, maxHandleDistance);

        PlaceAtScreenPoint(joystickHandle, centerScreenPos + clamped);

        Vector2 strengthAndDirection = maxHandleDistance > 0f ? clamped / maxHandleDistance : Vector2.zero;
        if (shipMovement != null) shipMovement.SetInput(strengthAndDirection);
    }

    /// <summary>Moves a UI element to a screen-space point CORRECTLY regardless of the Canvas's own
    /// Render Mode. Assigning RectTransform.position directly (screen pixels straight into a WORLD
    /// position) only happens to work under Screen Space - Overlay — under Camera/World Space it sends
    /// the element to a wildly wrong world coordinate instead, appearing to just vanish.</summary>
    private void PlaceAtScreenPoint(RectTransform target, Vector2 screenPos)
    {
        if (target == null) return;

        var parentRect = target.parent as RectTransform;
        if (parentRect == null) return;

        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, cam, out Vector2 localPoint))
            target.anchoredPosition = localPoint;
    }

    private void ForceRelease()
    {
        activePointerId = int.MinValue;
        SetVisualsActive(false);
        if (shipMovement != null) shipMovement.SetInput(Vector2.zero);
    }

    private void SetVisualsActive(bool value)
    {
        if (joystickBase != null) joystickBase.gameObject.SetActive(value);
        if (joystickHandle != null) joystickHandle.gameObject.SetActive(value);
    }
}
