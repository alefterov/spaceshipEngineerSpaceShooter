using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Floating joystick: press ANYWHERE on the battle screen and drag — the ship moves toward wherever
/// the stick points (see ShipMovement.SetInput), at a strength proportional to how far the drag has
/// gone (capped at Max Handle Distance, 0..1). The joystick has no fixed screen position — wherever
/// the finger/click goes down becomes its center for that gesture.
///
/// Polls Touchscreen/Mouse directly every frame rather than uGUI drag events — same reasoning as
/// BuildCameraController: a lost OnEndDrag would leave the ship thrusting forever in the last
/// direction with no way to cancel it, so a from-scratch-every-frame read is safer here than
/// incremental event bookkeeping.
///
/// SETUP: put on a full-screen UI Image (can be fully transparent, Raycast Target ON) covering the
/// battle HUD — same "input catcher" role GhostBlockController's own full-screen image plays in the
/// builder. Joystick Base/Handle are optional — assign them if you want a visible stick that appears
/// at the touch point; movement works even without them.
/// </summary>
public class ShipJoystickController : MonoBehaviour
{
    public ShipMovement shipMovement;

    [Header("Visuals (optional)")]
    [Tooltip("Moved to the press point and shown while held.")]
    public RectTransform joystickBase;
    [Tooltip("Moved within Max Handle Distance of the base, toward the finger.")]
    public RectTransform joystickHandle;
    public float maxHandleDistance = 100f;

    private bool active;
    private Vector2 centerScreenPos;

    private void OnEnable() => SetVisualsActive(false);

    private void OnDisable()
    {
        active = false;
        if (shipMovement != null) shipMovement.SetInput(Vector2.zero);
    }

    private void Update()
    {
        bool pressed = TryGetPointer(out Vector2 screenPos, out bool justPressed);

        if (!pressed)
        {
            if (active) Release();
            return;
        }

        if (justPressed) Press(screenPos);
        else if (active) Drag(screenPos);
    }

    private static bool TryGetPointer(out Vector2 screenPos, out bool justPressed)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            justPressed = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            screenPos = Mouse.current.position.ReadValue();
            justPressed = Mouse.current.leftButton.wasPressedThisFrame;
            return true;
        }

        screenPos = default;
        justPressed = false;
        return false;
    }

    private void Press(Vector2 screenPos)
    {
        active = true;
        centerScreenPos = screenPos;

        if (joystickBase != null) joystickBase.position = screenPos;
        if (joystickHandle != null) joystickHandle.position = screenPos;
        SetVisualsActive(true);

        Drag(screenPos); // starts at zero deflection
    }

    private void Drag(Vector2 screenPos)
    {
        Vector2 offset = screenPos - centerScreenPos;
        Vector2 clamped = Vector2.ClampMagnitude(offset, maxHandleDistance);

        if (joystickHandle != null) joystickHandle.position = centerScreenPos + clamped;

        Vector2 strengthAndDirection = maxHandleDistance > 0f ? clamped / maxHandleDistance : Vector2.zero;
        if (shipMovement != null) shipMovement.SetInput(strengthAndDirection);
    }

    private void Release()
    {
        active = false;
        SetVisualsActive(false);
        if (shipMovement != null) shipMovement.SetInput(Vector2.zero);
    }

    private void SetVisualsActive(bool value)
    {
        if (joystickBase != null) joystickBase.gameObject.SetActive(value);
        if (joystickHandle != null) joystickHandle.gameObject.SetActive(value);
    }
}
