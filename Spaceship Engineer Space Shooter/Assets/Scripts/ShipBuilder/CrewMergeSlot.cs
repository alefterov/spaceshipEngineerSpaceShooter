using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One of the two empty windows of the merge popup. Accepts a dragged crew member only if it is a valid
/// partner for the member being ranked up: the exact same kind (same CrewMemberDefinition, therefore the
/// same specialty) at the exact same rank, and not the member itself or the one already in the other
/// window. Dropping only SELECTS the member — nothing is consumed until the merge is confirmed.
///
/// SETUP: two of these inside the merge popup. Assign Card (a CrewCardView showing the selection; its
/// Empty Root can hold a "drop a card here" hint) and optionally a Clear Button. Popup is found
/// automatically in the parents. The Card's own Image / the window's background must be a raycast target.
/// </summary>
public class CrewMergeSlot : MonoBehaviour, ICrewDropTarget
{
    public CrewCardView card;
    [Tooltip("Optional: empties this window again. Only shown while a member is selected.")]
    public Button clearButton;
    [Tooltip("Found automatically in the parents if left empty.")]
    public CrewMergePopup popup;

    public CrewMemberData Selected { get; private set; }

    private void Awake()
    {
        if (popup == null) popup = GetComponentInParent<CrewMergePopup>(true);
        if (card == null) card = GetComponentInChildren<CrewCardView>(true);
        if (card == null) Debug.LogWarning($"CrewMergeSlot on '{name}': no Card assigned and no CrewCardView found in its children — the window will show nothing.", this);
        if (card != null)
        {
            card.draggable = true; // drag the selected card out of the window (and release it away from it) to empty it
            card.ReleasedOutside = _ => OnClearClicked();
        }
        if (clearButton != null) clearButton.onClick.AddListener(OnClearClicked);
    }

    public bool CanAccept(CrewMemberData member) => popup != null && popup.CanUseAsFodder(member, this);

    public void Drop(CrewMemberData member)
    {
        Selected = member;
        Refresh();
        popup.NotifySelectionChanged();
    }

    public void Clear()
    {
        Selected = null;
        Refresh();
    }

    public void Refresh()
    {
        if (card != null) card.Bind(Selected);
        if (clearButton != null) clearButton.gameObject.SetActive(Selected != null);
    }

    private void OnClearClicked()
    {
        Clear();
        if (popup != null) popup.NotifySelectionChanged();
    }
}
