using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
///
/// BUG WORKAROUND: on rare occasions (observed specifically on the very first drag of a Play
/// session, never after) Unity's Input System UI module fails to deliver OnEndDrag for a completed
/// drag, leaving the gesture stuck "in flight" — nothing to confirm, nothing to cancel, no way out
/// short of starting a whole new drag. IPointerUpHandler.OnPointerUp is tracked through a separate,
/// more fundamental code path in uGUI than the Begin/Drag/EndDrag chain, so it's used here as a
/// second way to detect the release. Both funnel through ReleaseDrag(), which clears leftButtonRect
/// on the first call — so if both fire normally for the same release (the usual case), the second
/// one is a harmless no-op instead of a double-fire.
///
/// SCROLLING: because this button takes the drag, the palette's ScrollRect would never see a swipe that starts on a button. So the
/// direction decides: a swipe within Drag Out Min..Max Angle (counted like on a clock face turned so 0° = right, 90° = straight up,
/// 180° = left; 45..135 by default — up, towards the grid) drags the block out as before; any other direction is handed to the
/// ScrollRect and scrolls the list.
/// </summary>
public class BlockButtonDragHandle : MonoBehaviour,
    IPointerClickHandler, IPointerUpHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>Fired on a plain tap — finger never left this button's rect.</summary>
    public event Action OnTap;
    /// <summary>Fired once, the moment the dragging finger first leaves this button's rect.</summary>
    public event Action<Vector2> OnDragStarted;
    /// <summary>Fired on every subsequent move while the finger is outside this button's rect.</summary>
    public event Action<Vector2> OnDragMoved;
    /// <summary>Fired on release, only if OnDragStarted fired earlier this gesture.</summary>
    public event Action<Vector2> OnDragReleased;

    [Tooltip("Swipe directions that drag the block out onto the grid, in degrees: 0 = right, 90 = straight up, 180 = left, 270 = down. " +
             "A swipe in any other direction scrolls the list instead.")]
    [Range(0f, 360f)] public float dragOutMinAngle = 45f;
    [Range(0f, 360f)] public float dragOutMaxAngle = 135f;

    private RectTransform rt;
    private bool leftButtonRect;
    private ScrollRect scroll;
    private bool scrolling; // this gesture scrolls the list instead of dragging the block

    private void Awake()
    {
        rt = (RectTransform)transform;
        scroll = GetComponentInParent<ScrollRect>(true);
    }

    // Lets the list stop its glide the moment a finger touches it, as when touching the list itself.
    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (scroll != null) scroll.OnInitializePotentialDrag(eventData);
    }

    /// <summary>Whether the swipe so far should scroll the list: it goes outside the drag-out angle range.</summary>
    private bool IsAlongScroll(PointerEventData eventData)
    {
        if (scroll == null || !scroll.isActiveAndEnabled) return false;

        Vector2 moved = eventData.position - eventData.pressPosition;
        float angle = Mathf.Atan2(moved.y, moved.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        return angle < dragOutMinAngle || angle > dragOutMaxAngle;
    }

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
        scrolling = IsAlongScroll(eventData);
        if (scrolling)
        {
            scroll.OnBeginDrag(eventData);
            return;
        }

        CheckLeftRect(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (scrolling)
        {
            scroll.OnDrag(eventData);
            return;
        }

        if (!leftButtonRect) CheckLeftRect(eventData);
        if (leftButtonRect) OnDragMoved?.Invoke(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (EndScroll(eventData)) return;
        if (leftButtonRect) ReleaseDrag(eventData.position);
    }

    // Backup path for OnEndDrag — see the class doc comment for why this exists.
    public void OnPointerUp(PointerEventData eventData)
    {
        if (EndScroll(eventData)) return;
        if (leftButtonRect) ReleaseDrag(eventData.position);
    }

    /// <summary>Ends a scrolling gesture (from OnPointerUp or OnEndDrag, whichever comes first). False if this gesture was not scrolling.</summary>
    private bool EndScroll(PointerEventData eventData)
    {
        if (!scrolling) return false;

        scrolling = false;
        scroll.OnEndDrag(eventData);
        return true;
    }

    private void ReleaseDrag(Vector2 screenPos)
    {
        leftButtonRect = false; // cleared first — whichever of OnEndDrag/OnPointerUp fires second sees this as false and no-ops
        OnDragReleased?.Invoke(screenPos);
    }

    private void CheckLeftRect(PointerEventData eventData)
    {
        bool inside = RectTransformUtility.RectangleContainsScreenPoint(rt, eventData.position, eventData.pressEventCamera);
        if (inside) return;

        leftButtonRect = true;
        OnDragStarted?.Invoke(eventData.position);
    }
}
