using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The rank-up ("crossing") popup. Opens for one crew member (Open) — shown in Base Card — plus two empty
/// windows (CrewMergeSlot) into which you drag two OTHER copies of the exact same kind at the exact same
/// rank (from the collection scroll view, or from a slot). Confirm consumes both: the member goes up one
/// rank, its level resets to 1, its next skill unlocks and its picture changes to the new rank's. It then
/// sits in the collection, not in a slot (GameDataManager.TryMergeCrew).
///
/// The collection scroll view has to stay reachable while this is open (the cards are dragged out of it)
/// — lay the popup out so it doesn't cover it. Closes itself if the member can no longer be ranked up.
///
/// SETUP: put on the popup's root (it starts INACTIVE; slots' "Rank Up" buttons call Open through their
/// Merge Popup field). Assign Base Card, the two Slots, and Confirm Button; Hint Label is optional. Wire
/// whatever close button you have to Close().
/// </summary>
public class CrewMergePopup : MonoBehaviour
{
    [Header("Windows")]
    [Tooltip("Shows the member being ranked up.")]
    public CrewCardView baseCard;
    public CrewMergeSlot slotA;
    public CrewMergeSlot slotB;

    [Header("UI")]
    [Tooltip("Enabled only when both windows hold a valid partner.")]
    public Button confirmButton;
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
    }

    private void OnEnable()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);

        var data = GameDataManager.Instance;
        if (data != null) data.OnCrewChanged += Refresh;
    }

    private void OnDisable()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);

        var data = GameDataManager.Instance;
        if (data != null) data.OnCrewChanged -= Refresh;
    }

    /// <summary>Opens the popup to rank up this member.</summary>
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

    private void Refresh()
    {
        if (!IsOpen) return;

        var data = GameDataManager.Instance;
        var baseMember = data != null ? data.GetCrewMember(baseId) : null;
        if (baseMember == null || !data.CanRankUpCrew(baseMember))
        {
            Close(); // gone, or already at the top rank — nothing left to do here
            return;
        }

        // A selection that stopped being valid (the member vanished, or the base changed rank) is dropped.
        DropStaleSelection(slotA, data, baseMember);
        DropStaleSelection(slotB, data, baseMember);

        if (baseCard != null) baseCard.Bind(baseMember);

        var definition = data.GetCrewDefinition(baseMember);
        if (hintLabel != null && definition != null)
            hintLabel.text = string.Format(hintFormat, definition.displayName, baseMember.rank);

        if (confirmButton != null) confirmButton.interactable = BothSelected;
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
