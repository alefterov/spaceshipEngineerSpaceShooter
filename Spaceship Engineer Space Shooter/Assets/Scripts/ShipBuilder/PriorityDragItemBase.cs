using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The dragging behaviour shared by every row of every priority list (energy, missiles, tower guns). The row follows the pointer; an
/// empty place the size of the row (a placeholder) marks where it will land, and the OTHER rows glide aside to make room for it as you
/// move over them (see PriorityListMotion). On release the row glides into the place. The container must be a layout group, e.g. a
/// Vertical Layout Group; the order of the rows is simply their order inside it.
///
/// A CanvasGroup and a LayoutElement are required on the same object (added automatically if missing).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(LayoutElement))]
public abstract class PriorityDragItemBase : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rect;
    private RectTransform parentRect;
    private CanvasGroup canvasGroup;
    private LayoutElement layoutElement;
    private PriorityListMotion motion;
    private RectTransform placeholder;
    private Vector2 grabOffset;
    private bool dragging;

    /// <summary>Called once the row has been dropped in its new place: save / apply the new order.</summary>
    protected abstract void OnOrderChanged();

    protected virtual void Awake()
    {
        rect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        layoutElement = GetComponent<LayoutElement>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        parentRect = transform.parent as RectTransform;
        if (parentRect == null) return;

        motion = PriorityListMotion.For(parentRect);
        motion.Release(rect);
        dragging = true;
        canvasGroup.blocksRaycasts = false; // the pointer should see what is under the row

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out var pointer);
        grabOffset = (Vector2)rect.localPosition - pointer;

        // An empty place the same size takes the row's slot in the list; the row itself steps out of the layout and rides on top.
        motion.Rearrange(() =>
        {
            var go = new GameObject(name + " (place)", typeof(RectTransform), typeof(LayoutElement));
            placeholder = (RectTransform)go.transform;
            placeholder.SetParent(parentRect, false);
            placeholder.sizeDelta = rect.sizeDelta;
            placeholder.localScale = Vector3.one;
            var element = go.GetComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = rect.rect.width;
            element.minHeight = element.preferredHeight = rect.rect.height;
            placeholder.SetSiblingIndex(rect.GetSiblingIndex());

            layoutElement.ignoreLayout = true;
            rect.SetAsLastSibling();
        });
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || parentRect == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out var pointer);
        Vector2 position = pointer + grabOffset;
        rect.localPosition = new Vector3(position.x, position.y, rect.localPosition.z);

        // Over another row (judged by where that row is HEADED, not where it is gliding through): the empty place moves there and the
        // row makes way.
        for (int i = 0; i < parentRect.childCount; i++)
        {
            var other = parentRect.GetChild(i) as RectTransform;
            if (other == null || other == rect || other == placeholder || !other.gameObject.activeSelf) continue;

            Vector2 resting = motion.RestingPosition(other);
            var area = new Rect(resting + other.rect.min, other.rect.size);
            if (!area.Contains(pointer)) continue;

            int index = other.GetSiblingIndex();
            if (index == placeholder.GetSiblingIndex()) break;

            motion.Rearrange(() => placeholder.SetSiblingIndex(index));
            break;
        }
    }

    public void OnEndDrag(PointerEventData eventData) => Drop();

    private void OnDisable()
    {
        if (dragging) Drop(); // the window was closed in the middle of a drag
    }

    private void Drop()
    {
        if (!dragging) return;
        dragging = false;
        canvasGroup.blocksRaycasts = true;

        if (placeholder != null && motion != null)
        {
            motion.Rearrange(() =>
            {
                layoutElement.ignoreLayout = false;
                rect.SetSiblingIndex(placeholder.GetSiblingIndex()); // the row takes the place of the empty slot
                placeholder.gameObject.SetActive(false);
                Destroy(placeholder.gameObject);
            });
        }
        else
        {
            layoutElement.ignoreLayout = false;
        }

        placeholder = null;
        OnOrderChanged();
    }
}
