using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One draggable row in the energy-priority window — drag it above or below a sibling to swap places.
/// Reordering works by swapping SIBLING INDEX within Priority Items Parent, so put this on each of the
/// 4 rows inside a layout group (e.g. a Vertical Layout Group) there — the layout group is what actually
/// repositions everything once sibling order settles; this component only ever changes index and, while
/// dragging, a manual on-screen offset for live feedback.
///
/// SETUP: put on each of the 4 priority rows (Movement/Weapons/Defense/Repair), set System to whichever
/// one this row represents, and assign Controller to the scene's EnergyPriorityController. Needs a
/// CanvasGroup and a LayoutElement on the same object — both added automatically if missing.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(LayoutElement))]
public class PriorityDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ShipEnergySystem.EnergyPriorityGroup system;
    public EnergyPriorityController controller;

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
        canvasGroup.blocksRaycasts = false; // let drops land on whatever sibling is underneath, not this item itself
        layoutElement.ignoreLayout = true;  // detach from the layout group for the duration of the drag — see OnDrag
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Manual visual offset while dragging (the layout group is ignoring this item right now, so it
        // won't fight this) — purely cosmetic "follow the finger" feedback.
        rect.anchoredPosition += eventData.delta / (canvas != null ? canvas.scaleFactor : 1f);

        // Swap sibling index with whichever OTHER row the pointer is currently over, so the list visibly
        // reorders live as you drag, not just once you release.
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
        layoutElement.ignoreLayout = false; // hand control back to the layout group — it snaps this into its new slot
        rect.anchoredPosition = Vector2.zero;

        if (controller != null) controller.SetOrderFromSiblingOrder();
    }
}
