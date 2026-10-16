using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One draggable row of the missile target-priority window — drag it above or below another row to swap
/// places. Works like PriorityDragItem (the energy window's rows): reordering swaps the SIBLING INDEX inside
/// the controller's Priority Items Parent, which should be a layout group.
///
/// SETUP: put on each row, set Target Type to the block type this row stands for, and assign Controller to the
/// scene's MissilePriorityController. A CanvasGroup and a LayoutElement are required on the same object
/// (added automatically if missing).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(LayoutElement))]
public class MissilePriorityDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ModuleType targetType = ModuleType.Cockpit;
    public MissilePriorityController controller;

    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private LayoutElement layoutElement;
    private Canvas canvas;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        layoutElement = GetComponent<LayoutElement>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = false; // let drops land on whatever sibling is underneath
        layoutElement.ignoreLayout = true;  // detach from the layout group while dragging
    }

    public void OnDrag(PointerEventData eventData)
    {
        rect.anchoredPosition += eventData.delta / (canvas != null ? canvas.scaleFactor : 1f);

        // Swap with whichever other row the pointer is over, so the list reorders live as you drag.
        Transform parent = transform.parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var sibling = parent.GetChild(i);
            if (sibling == transform) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)sibling, eventData.position, eventData.pressEventCamera))
            {
                transform.SetSiblingIndex(i);
                break;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        layoutElement.ignoreLayout = false; // the layout group snaps the row into its new slot
        rect.anchoredPosition = Vector2.zero;

        if (controller != null) controller.SetOrderFromSiblingOrder();
    }
}
