using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class ShipJoystickController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public ShipMovement shipMovement;

    [Header("Visuals (optional)")]
    [Tooltip("Moved to the press point and shown while held.")]
    public RectTransform joystickBase;
    [Tooltip("Moved within Max Handle Distance of the base, toward the pointer.")]
    public RectTransform joystickHandle;
    public float maxHandleDistance = 100f;

    private int activePointerId = int.MinValue;
    private Vector2 centerScreenPos;
    private Canvas canvas;

    private void Awake() => canvas = GetComponentInParent<Canvas>();

    private void OnEnable() => SetVisualsActive(false);

    private void OnDisable() => ForceRelease();

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointerId != int.MinValue) return;

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
