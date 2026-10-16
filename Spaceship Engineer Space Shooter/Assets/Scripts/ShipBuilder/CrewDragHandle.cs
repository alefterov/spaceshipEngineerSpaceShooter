using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drag-and-drop for crew cards. Put on anything that also has an ICrewMemberProvider (CrewCardView,
/// CrewMemberView) — they add it themselves if it's missing. Dragging spawns a semi-transparent copy of the
/// portrait that follows the finger on top of everything; on release it's dropped on the topmost
/// ICrewDropTarget under the finger (a slot member released over no target at all is taken off its slot). The original never moves, so a refused drop needs no "snap back".
///
/// INSIDE A SCROLL VIEW: a drag ALONG the scroll's axis scrolls the list as usual; a drag across it
/// (up/down for a horizontal list) pulls the card out. So a horizontal collection works with slots placed
/// above or below it; a vertical one with slots to its side. (A scroll view that scrolls both ways always
/// scrolls — it can't tell the two gestures apart.)
/// </summary>
public class CrewDragHandle : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("The drag ghost is shrunk to fit this many UI units on its longer side.")]
    public float maxGhostSize = 220f;
    [Range(0f, 1f)] public float ghostAlpha = 0.85f;

    private ICrewMemberProvider provider;
    private ScrollRect scroll;
    private bool forwardingToScroll;
    private RectTransform ghost;
    private Canvas rootCanvas;
    /// <summary>Raised when a crew-card drag really starts / ends (the finger lifts). CrewDropHint listens.</summary>
    public static event System.Action<CrewMemberData> DragStarted;
    public static event System.Action DragEnded;

    private CrewMemberData draggedMember; // who is being dragged; null = no card drag of our own is in progress

    private ICrewMemberProvider Provider => provider ??= GetComponent<ICrewMemberProvider>();

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        var parentScroll = GetComponentInParent<ScrollRect>();
        if (parentScroll != null) ExecuteEvents.Execute(parentScroll.gameObject, eventData, ExecuteEvents.initializePotentialDrag);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        forwardingToScroll = false;
        draggedMember = null;
        scroll = GetComponentInParent<ScrollRect>();

        bool canDragCard = Provider != null && Provider.Member != null && Provider.CanDrag;
        if (!canDragCard || (scroll != null && IsAlongScrollAxis(eventData)))
        {
            if (scroll == null) return; // nothing to scroll and nothing to drag — ignore the gesture

            forwardingToScroll = true;
            ExecuteEvents.Execute(scroll.gameObject, eventData, ExecuteEvents.beginDragHandler);
            return;
        }

        draggedMember = Provider.Member; // fixed now — the card may be rebuilt before the drag ends
        DragStarted?.Invoke(draggedMember);
        CreateGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (forwardingToScroll) ExecuteEvents.Execute(scroll.gameObject, eventData, ExecuteEvents.dragHandler);
        else MoveGhost(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (forwardingToScroll)
        {
            forwardingToScroll = false;
            ExecuteEvents.Execute(scroll.gameObject, eventData, ExecuteEvents.endDragHandler);
            return;
        }

        // Only a drag that really started here (OnBeginDrag got as far as picking a member up) may drop
        // anything. Without this, a gesture that was refused at its start — a card that can't be dragged —
        // still ran the drop logic when released, and could e.g. take a member off its slot.
        var member = draggedMember;
        draggedMember = null;
        DestroyGhost(); // first — so the ghost can never be what gets hit, and never outlives a data rebuild
        if (member != null) DragEnded?.Invoke(); // hints start fading out the instant the finger lifts

        if (member != null) DropOnTarget(eventData, member);
    }

    private void OnDisable()
    {
        DestroyGhost();

        // Disabled/destroyed in the middle of a drag (the card was rebuilt): OnEndDrag will never come.
        if (draggedMember != null)
        {
            draggedMember = null;
            DragEnded?.Invoke();
        }
    }

    private bool IsAlongScrollAxis(PointerEventData eventData)
    {
        Vector2 moved = eventData.position - eventData.pressPosition;

        if (scroll.horizontal && !scroll.vertical) return Mathf.Abs(moved.x) > Mathf.Abs(moved.y);
        if (scroll.vertical && !scroll.horizontal) return Mathf.Abs(moved.y) > Mathf.Abs(moved.x);
        return true;
    }

    // The drop is decided by what is TOPMOST under the finger. If that thing belongs to a drop target, that
    // target accepts or refuses. If it belongs to none (empty panel, another card, a popup's background...)
    // the card was released "outside" — nothing underneath may react to it. (Looking past such an object for
    // a target hidden beneath it made releases silently fail whenever a slot or the collection happened to
    // lie behind the popup.)
    private void DropOnTarget(PointerEventData eventData, CrewMemberData member)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits); // sorted topmost first

        if (hits.Count > 0)
        {
            var topmost = hits[0].gameObject;
            var target = topmost.GetComponentInParent<ICrewDropTarget>();
            if (target != null)
            {
                if (target.CanAccept(member)) target.Drop(member);
                return;
            }

            if (topmost.transform.IsChildOf(transform)) return; // dropped back on the dragged object itself
        }

        GetComponent<ICrewReleaseHandler>()?.OnReleasedOutside(member);
    }

    private void CreateGhost(PointerEventData eventData)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        rootCanvas = canvas.rootCanvas;

        var ghostObj = new GameObject("CrewDragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        ghost = (RectTransform)ghostObj.transform;
        ghost.SetParent(rootCanvas.transform, false);
        ghost.SetAsLastSibling();
        ghost.anchorMin = ghost.anchorMax = ghost.pivot = new Vector2(0.5f, 0.5f);

        ghostObj.GetComponent<CanvasGroup>().blocksRaycasts = false;

        var portrait = Provider.PortraitImage;
        var image = ghostObj.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.sprite = portrait != null ? portrait.sprite : null;
        image.color = new Color(1f, 1f, 1f, ghostAlpha);

        Vector2 size = portrait != null ? portrait.rectTransform.rect.size : new Vector2(maxGhostSize, maxGhostSize);
        float longest = Mathf.Max(size.x, size.y, 1f);
        if (longest > maxGhostSize) size *= maxGhostSize / longest;
        ghost.sizeDelta = size;

        MoveGhost(eventData);
    }

    private void MoveGhost(PointerEventData eventData)
    {
        if (ghost == null || rootCanvas == null) return;

        var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)rootCanvas.transform, eventData.position, camera, out var world))
            ghost.position = world;
    }

    private void DestroyGhost()
    {
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
    }
}
