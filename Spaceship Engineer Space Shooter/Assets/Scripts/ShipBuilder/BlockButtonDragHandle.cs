using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockButtonDragHandle : MonoBehaviour,
    IPointerClickHandler, IPointerUpHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public event Action OnTap;
    public event Action<Vector2> OnDragStarted;
    public event Action<Vector2> OnDragMoved;
    public event Action<Vector2> OnDragReleased;

    [Tooltip("Swipe directions that drag the block out onto the grid, in degrees: 0 = right, 90 = straight up, 180 = left, 270 = down. " +
             "A swipe in any other direction scrolls the list instead.")]
    [Range(0f, 360f)] public float dragOutMinAngle = 45f;
    [Range(0f, 360f)] public float dragOutMaxAngle = 135f;

    private RectTransform rt;
    private bool leftButtonRect;
    private ScrollRect scroll;
    private bool scrolling;

    private void Awake()
    {
        rt = (RectTransform)transform;
        scroll = GetComponentInParent<ScrollRect>(true);
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (scroll != null) scroll.OnInitializePotentialDrag(eventData);
    }

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

    public void OnPointerUp(PointerEventData eventData)
    {
        if (EndScroll(eventData)) return;
        if (leftButtonRect) ReleaseDrag(eventData.position);
    }

    private bool EndScroll(PointerEventData eventData)
    {
        if (!scrolling) return false;

        scrolling = false;
        scroll.OnEndDrag(eventData);
        return true;
    }

    private void ReleaseDrag(Vector2 screenPos)
    {
        leftButtonRect = false;
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
