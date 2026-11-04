using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        canvasGroup.blocksRaycasts = false;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out var pointer);
        grabOffset = (Vector2)rect.localPosition - pointer;

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
        if (dragging) Drop();
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
                rect.SetSiblingIndex(placeholder.GetSiblingIndex());
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
