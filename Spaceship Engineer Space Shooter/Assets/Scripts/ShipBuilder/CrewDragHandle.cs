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

    private ICrewMemberProvider Provider => provider ??= GetComponent<ICrewMemberProvider>();

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        var parentScroll = GetComponentInParent<ScrollRect>();
        if (parentScroll != null) ExecuteEvents.Execute(parentScroll.gameObject, eventData, ExecuteEvents.initializePotentialDrag);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        forwardingToScroll = false;
        scroll = GetComponentInParent<ScrollRect>();

        bool canDragCard = Provider != null && Provider.Member != null && Provider.CanDrag;
        if (!canDragCard || (scroll != null && IsAlongScrollAxis(eventData)))
        {
            if (scroll == null) return; // nothing to scroll and nothing to drag — ignore the gesture

            forwardingToScroll = true;
            ExecuteEvents.Execute(scroll.gameObject, eventData, ExecuteEvents.beginDragHandler);
            return;
        }

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

        var member = Provider?.Member;
        DestroyGhost(); // first — so the ghost can never be what gets hit, and never outlives a data rebuild

        if (member != null) DropOnTarget(eventData, member);
    }

    private void OnDisable() => DestroyGhost();

    private bool IsAlongScrollAxis(PointerEventData eventData)
    {
        Vector2 moved = eventData.position - eventData.pressPosition;

        if (scroll.horizontal && !scroll.vertical) return Mathf.Abs(moved.x) > Mathf.Abs(moved.y);
        if (scroll.vertical && !scroll.horizontal) return Mathf.Abs(moved.y) > Mathf.Abs(moved.x);
        return true;
    }

    private void DropOnTarget(PointerEventData eventData, CrewMemberData member)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits); // sorted topmost first

        foreach (var hit in hits)
        {
            var target = hit.gameObject.GetComponentInParent<ICrewDropTarget>();
            if (target == null) continue;

            if (target.CanAccept(member)) target.Drop(member);
            return; // the topmost target decides — accepted or not, nothing underneath gets a second chance
        }

        // Released over nothing at all.
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
