using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    public static event System.Action<CrewMemberData> DragStarted;
    public static event System.Action DragEnded;

    private CrewMemberData draggedMember;

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
            if (scroll == null) return;

            forwardingToScroll = true;
            ExecuteEvents.Execute(scroll.gameObject, eventData, ExecuteEvents.beginDragHandler);
            return;
        }

        draggedMember = Provider.Member;
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

        var member = draggedMember;
        draggedMember = null;
        DestroyGhost();
        if (member != null) DragEnded?.Invoke();

        if (member != null) DropOnTarget(eventData, member);
    }

    private void OnDisable()
    {
        DestroyGhost();

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

    private void DropOnTarget(PointerEventData eventData, CrewMemberData member)
    {
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, hits);

        if (hits.Count > 0)
        {
            var topmost = hits[0].gameObject;
            var target = topmost.GetComponentInParent<ICrewDropTarget>();
            if (target != null)
            {
                if (target.CanAccept(member)) target.Drop(member);
                return;
            }

            if (topmost.transform.IsChildOf(transform)) return;
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
