using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            data.OnCreditsChanged += Refresh;
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

    public bool CanUseAsFodder(CrewMemberData member, CrewMergeSlot slot)
    {
        var data = GameDataManager.Instance;
        var baseMember = data != null ? data.GetCrewMember(baseId) : null;
        if (baseMember == null || !data.CanBeMergeFodder(baseMember, member)) return false;

        var other = slot == slotA ? slotB : slotA;
        return other == null || other.Selected == null || other.Selected.instanceId != member.instanceId;
    }

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
            Close();
            return;
        }

        DropStaleSelection(slotA, data, baseMember);
        DropStaleSelection(slotB, data, baseMember);

        if (baseCard != null) baseCard.Bind(baseMember);

        var definition = data.GetCrewDefinition(baseMember);
        if (hintLabel != null && definition != null)
            hintLabel.text = string.Format(hintFormat, definition.displayName, baseMember.rank);

        RebuildFodderList(data, baseMember);

        if (costLabel != null) costLabel.text = NumberFormat.Compact(data.GetCrewMergeCost(baseMember));
        if (confirmButton != null) confirmButton.interactable = BothSelected && data.CanAffordCrewMerge(baseMember);
    }

    private void RebuildFodderList(GameDataManager data, CrewMemberData baseMember)
    {
        if (fodderContent == null || fodderCardPrefab == null) return;

        for (int i = fodderContent.childCount - 1; i >= 0; i--)
        {
            var child = fodderContent.GetChild(i).gameObject;
            if (child == fodderCardPrefab.gameObject) continue;
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
