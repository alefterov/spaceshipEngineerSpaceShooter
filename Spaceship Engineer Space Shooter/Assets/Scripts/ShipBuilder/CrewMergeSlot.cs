using UnityEngine;
using UnityEngine.UI;

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
            card.draggable = true;
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
