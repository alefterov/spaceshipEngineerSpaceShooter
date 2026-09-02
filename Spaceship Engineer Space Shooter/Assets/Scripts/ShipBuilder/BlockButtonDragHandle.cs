using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to the BlockButton prefab (alongside BlockButtonView). Distinguishes a plain tap
/// (select the block, e.g. to rotate it before placing) from a press-and-drag-off gesture
/// (start dragging it onto the build grid).
///
/// uGUI always routes OnDrag/OnEndDrag to whichever object received OnBeginDrag, regardless of
/// what's currently under the finger — so once a drag starts here, it keeps reporting to this
/// component even after the finger has moved off the button and over the build grid. That's
/// exactly what lets a single continuous gesture go "press button -> drag onto grid -> release".
///
/// Knows nothing about ShipGrid/GhostBlockController — BuildPaletteUI subscribes to these events
/// when it instantiates each button and wires them to the right BuildModeController calls.
/// </summary>
public class BlockButtonDragHandle : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>Fired on a plain tap — finger never left this button's rect.</summary>
    public event Action OnTap;
    /// <summary>Fired once, the moment the dragging finger first leaves this button's rect.</summary>
    public event Action<Vector2> OnDragStarted;
    /// <summary>Fired on every subsequent move while the finger is outside this button's rect.</summary>
    public event Action<Vector2> OnDragMoved;
    /// <summary>Fired on release, only if OnDragStarted fired earlier this gesture.</summary>
    public event Action<Vector2> OnDragReleased;

    private RectTransform rt;
    private bool leftButtonRect;

    private void Awake() => rt = (RectTransform)transform;

    public void OnPointerClick(PointerEventData eventData)
    {
        // uGUI suppresses OnPointerClick once a gesture has exceeded the drag threshold, so this
        // only fires for a genuine tap — leftButtonRect is never true here in practice, but the
        // guard costs nothing and documents the intent.
        if (!leftButtonRect) OnTap?.Invoke();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        leftButtonRect = false;
        CheckLeftRect(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!leftButtonRect) CheckLeftRect(eventData);
        if (leftButtonRect) OnDragMoved?.Invoke(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (leftButtonRect) OnDragReleased?.Invoke(eventData.position);
        leftButtonRect = false;
    }

    private void CheckLeftRect(PointerEventData eventData)
    {
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(rt, eventData.position, eventData.pressEventCamera);
        if (inside) return;

        leftButtonRect = true;
        OnDragStarted?.Invoke(eventData.position);
    }
}
