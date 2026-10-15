using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The rank-up ("crossing") popup. Opens from a crew SLOT's Rank Up Button (Open) for the member standing
/// there — shown in Base Card. Its own scroll view lists ONLY the cards that can be used: spare copies of the
/// exact same kind at the exact same rank. Drag two of them into the two windows (CrewMergeSlot) and confirm:
/// both are consumed and the merge price is paid in credits (shown on the Confirm Button), the member goes up one rank, its level resets to 1, its next skill unlocks and its
/// picture changes to the new rank's. The member STAYS in its slot, and the popup closes (the slot already
/// shows the new card behind it).
///
/// Lay the popup out so the slot being ranked up stays visible if you like — it is not needed for the
/// dragging, which happens entirely inside the popup. Closes itself if the member can no longer be ranked
/// up (taken off its slot, or already at the top rank).
///
/// SETUP: put on the popup's root (it starts INACTIVE). Assign Base Card, the two Slots, Confirm Button, and
/// the fodder scroll view's Fodder Content + Fodder Card Prefab; Hint Label is optional. Wire whatever close
/// button you have to Close().
/// </summary>
public class CrewMergePopup : MonoBehaviour
{
    [Header("Windows")]
    [Tooltip("Shows the member being ranked up.")]
    public CrewCardView baseCard;
    public CrewMergeSlot slotA;
    public CrewMergeSlot slotB;

    [Header("Available cards (own scroll view)")]
    [Tooltip("The popup scroll view's Content — a card per usable spare copy is created inside it.")]
    public Transform fodderContent;
    [Tooltip("The card to copy for each usable copy: needs a CrewCardView. May be a prefab asset or a template " +
             "card sitting in Fodder Content in the scene (switched off, never destroyed).")]
    public CrewCardView fodderCardPrefab;

    [Header("UI")]
    [Tooltip("Enabled only when both windows hold a valid partner AND the player can pay the merge price.")]
    public Button confirmButton;
    [Tooltip("The merge price in credits — normally the text on the Confirm Button. The price is set by " +
             "GameDataManager.Crew Merge Costs (by the rank being merged).")]
    public TMP_Text costLabel;
    [Tooltip("Optional. {0} = the member's name, {1} = the rank the partners must have.")]
    public TMP_Text hintLabel;
    public string hintFormat = "Перетащите 2 карточки «{0}» ранга {1}";

    [Header("Behaviour")]
    [Tooltip("Merge the moment both windows are filled, with no Confirm press.")]
    public bool mergeAutomatically;

    private string baseId;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        if (baseCard != null) baseCard.draggable = false;
        if (fodderCardPrefab != null && fodderCardPrefab.gameObject.scene.IsValid()) fodderCardPrefab.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);

        var data = GameDataManager.Instance;
        if (data != null)
        {
            data.OnCrewChanged += Refresh;
            data.OnCreditsChanged += Refresh; // affordability of the Confirm Button depends on it
        }
    }

    private void OnDisable()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);

        var data = GameDataManager.Instance;
        if (data != null)
        {
            data.OnCrewChanged -= Refresh;
            data.OnCreditsChanged -= Refresh;
        }
    }

    /// <summary>Opens the popup to rank up this member (who must be standing in a slot).</summary>
    public void Open(string instanceId)
    {
        baseId = instanceId;
        gameObject.SetActive(true);

        if (slotA != null) slotA.Clear();
        if (slotB != null) slotB.Clear();
        Refresh();
    }

    public void Close()
    {
        baseId = null;
        gameObject.SetActive(false);
    }

    /// <summary>Whether `member` may be dropped into `slot`: a valid partner for the base member, and not
    /// already chosen in the other window.</summary>
    public bool CanUseAsFodder(CrewMemberData member, CrewMergeSlot slot)
    {
        var data = GameDataManager.Instance;
        var baseMember = data != null ? data.GetCrewMember(baseId) : null;
        if (baseMember == null || !data.CanBeMergeFodder(baseMember, member)) return false;

        var other = slot == slotA ? slotB : slotA;
        return other == null || other.Selected == null || other.Selected.instanceId != member.instanceId;
    }

    /// <summary>Called by a window after its selection changed.</summary>
    public void NotifySelectionChanged()
    {
        Refresh();
        if (mergeAutomatically && BothSelected) Confirm();
    }

    private bool BothSelected => slotA != null && slotB != null && slotA.Selected != null && slotB.Selected != null;

    private bool IsSelected(CrewMemberData member)
        => (slotA != null && slotA.Selected != null && slotA.Selected.instanceId == member.instanceId)
        || (slotB != null && slotB.Selected != null && slotB.Selected.instanceId == member.instanceId);

    private void Refresh()
    {
        if (!IsOpen) return;

        var data = GameDataManager.Instance;
        var baseMember = data != null ? data.GetCrewMember(baseId) : null;
        if (baseMember == null || !data.CanRankUpCrew(baseMember) || !data.IsCrewAssigned(baseId))
        {
            Close(); // gone, off its slot, or already at the top rank — nothing left to do here
            return;
        }

        // A selection that stopped being valid (the member vanished, or the base changed rank) is dropped.
        DropStaleSelection(slotA, data, baseMember);
        DropStaleSelection(slotB, data, baseMember);

        if (baseCard != null) baseCard.Bind(baseMember);

        var definition = data.GetCrewDefinition(baseMember);
        if (hintLabel != null && definition != null)
            hintLabel.text = string.Format(hintFormat, definition.displayName, baseMember.rank);

        RebuildFodderList(data, baseMember);

        if (costLabel != null) costLabel.text = data.GetCrewMergeCost(baseMember).ToString();
        if (confirmButton != null) confirmButton.interactable = BothSelected && data.CanAffordCrewMerge(baseMember);
    }

    /// <summary>The popup's own scroll view: one card per usable spare copy, minus the ones already sitting in
    /// a window. Nothing else is shown.</summary>
    private void RebuildFodderList(GameDataManager data, CrewMemberData baseMember)
    {
        if (fodderContent == null || fodderCardPrefab == null) return;

        for (int i = fodderContent.childCount - 1; i >= 0; i--)
        {
            var child = fodderContent.GetChild(i).gameObject;
            if (child == fodderCardPrefab.gameObject) continue; // the scene template stays
            Destroy(child);
        }

        foreach (var candidate in data.GetMergeCandidates(baseMember).Where(c => !IsSelected(c)))
        {
            var card = Instantiate(fodderCardPrefab, fodderContent);
            card.Bind(candidate);
            card.gameObject.SetActive(true);
        }
    }

    private static void DropStaleSelection(CrewMergeSlot slot, GameDataManager data, CrewMemberData baseMember)
    {
        if (slot == null || slot.Selected == null) return;

        bool stillThere = data.GetCrewMember(slot.Selected.instanceId) != null;
        if (!stillThere || !data.CanBeMergeFodder(baseMember, slot.Selected)) slot.Clear();
    }

    public void Confirm()
    {
        if (!BothSelected) return;

        var data = GameDataManager.Instance;
        if (data.TryMergeCrew(baseId, slotA.Selected.instanceId, slotB.Selected.instanceId)) Close();
    }
}
