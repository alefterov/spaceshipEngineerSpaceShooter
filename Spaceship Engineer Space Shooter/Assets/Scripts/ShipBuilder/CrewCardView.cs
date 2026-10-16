using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One crew member shown as a card: portrait (of its current rank), name, level, rank and its 3 skills
/// with their coefficients. Used for the cards in the crew collection (the scroll view), and for the
/// "base" and "fodder" windows of the merge popup. Draggable (see CrewDragHandle, added automatically) —
/// unless Draggable is off.
///
/// Bind(member) fills it; Bind(null) puts it in its empty look (portrait back to whatever sprite the Image
/// had in the scene, Empty Root shown, Filled Root hidden).
///
/// SETUP (collection card prefab): the card's root Image is the Avatar Image; assign Name Label, the three
/// SkillItems (each with a CrewPerkView) as Perk Views, and optionally Level Label / Rank Label / Rank Up
/// Button. Make it a prefab and give it to CrewCollectionView.
/// </summary>
public class CrewCardView : MonoBehaviour, ICrewMemberProvider, ICrewReleaseHandler
{
    [Header("UI (assign whichever exist)")]
    [Tooltip("The portrait. Its sprite is swapped to the member's picture for its current rank.")]
    public Image avatarImage;
    public TMP_Text nameLabel;
    [Tooltip("Formatted like \"3 LV\".")]
    public TMP_Text levelLabel;
    [Tooltip("Formatted with Rank Format.")]
    public TMP_Text rankLabel;
    public string rankFormat = "Ранг {0}";
    [Tooltip("Optional: how many identical copies this card stands for (collection cards are grouped by kind and " +
             "rank). Hidden for a single copy.")]
    public TMP_Text countLabel;
    public string countFormat = "x{0}";
    [Tooltip("The three SkillItems, in order — perk 1 first.")]
    public CrewPerkView[] perkViews;
    [Tooltip("Optional: opens the merge (rank-up) popup for this member. Hidden at max rank, or when no " +
             "merge popup is assigned.")]
    public Button rankUpButton;

    [Header("Empty / filled looks (optional)")]
    [Tooltip("Shown only while no member is bound (e.g. \"drop a card here\").")]
    public GameObject emptyRoot;
    [Tooltip("Hidden while no member is bound.")]
    public GameObject filledRoot;

    [Header("Dragging")]
    public bool draggable = true;

    public CrewMemberData Member { get; private set; }
    public Image PortraitImage => avatarImage;
    public bool CanDrag => draggable;

    /// <summary>The popup the Rank Up Button opens. Set by whoever creates/owns the card.</summary>
    public CrewMergePopup MergePopup { get; set; }

    private Sprite emptySprite;
    private bool initialized;

    private void Awake() => Init();

    // Idempotent — Bind can run before Awake does (a card cloned from an inactive template is bound
    // before it's switched on).
    private void Init()
    {
        if (initialized) return;
        initialized = true;

        if (avatarImage != null) emptySprite = avatarImage.sprite;
        if (rankUpButton != null) rankUpButton.onClick.AddListener(OnRankUpClicked);
        if (!TryGetComponent<CrewDragHandle>(out _)) gameObject.AddComponent<CrewDragHandle>();
    }

    /// <summary>Shows `member`. `count` = how many identical copies this card represents (1 = just this one).</summary>
    public void Bind(CrewMemberData member, int count = 1)
    {
        Init();

        var data = GameDataManager.Instance;
        var definition = member != null && data != null ? data.GetCrewDefinition(member) : null;
        bool filled = member != null && definition != null;
        Member = filled ? member : null;

        if (UsableRoot(filledRoot)) filledRoot.SetActive(filled);
        if (UsableRoot(emptyRoot)) emptyRoot.SetActive(!filled);

        if (avatarImage != null)
        {
            var sprite = filled ? definition.GetRankSprite(member.rank) : null;
            avatarImage.sprite = sprite != null ? sprite : emptySprite;
        }

        // An empty card shows nothing but its portrait icon (whatever sprite the Avatar Image has in the editor).
        SetLabel(nameLabel, filled, filled ? definition.displayName : "");
        SetLabel(levelLabel, filled, filled ? $"{member.level} LV" : "");
        SetLabel(rankLabel, filled, filled ? string.Format(rankFormat, member.rank) : "");

        CrewPerkView.Populate(perkViews, definition, filled ? member : null);

        if (countLabel != null)
        {
            countLabel.gameObject.SetActive(filled && count > 1);
            if (filled && count > 1) countLabel.text = string.Format(countFormat, count);
        }

        if (rankUpButton != null)
            rankUpButton.gameObject.SetActive(filled && MergePopup != null && data.CanRankUpCrew(member));
    }

    /// <summary>What happens when this card is dragged and released over nothing (away from itself). Set by
    /// whoever owns the card — the merge popup's windows use it to empty themselves. Left unset, the member
    /// is taken off its crew slot if it stands in one, and nothing happens otherwise.</summary>
    public System.Action<CrewMemberData> ReleasedOutside { get; set; }

    public void OnReleasedOutside(CrewMemberData member)
    {
        if (ReleasedOutside != null)
        {
            ReleasedOutside(member);
            return;
        }

        var data = GameDataManager.Instance;
        if (data != null && member != null && data.IsCrewAssigned(member.instanceId)) data.UnassignCrew(member.instanceId);
    }

    private static void SetLabel(TMP_Text label, bool visible, string text)
    {
        if (label == null) return;
        label.gameObject.SetActive(visible);
        if (visible) label.text = text;
    }

    // Empty Root / Filled Root are switched on and off, so they must never be this card's own object or one
    // of its parents — that would switch the card itself off (and it could never come back). Such a
    // misassignment is ignored, with a one-time warning.
    private bool warnedBadRoot;
    private bool UsableRoot(GameObject root)
    {
        if (root == null) return false;
        if (!transform.IsChildOf(root.transform)) return true;

        if (!warnedBadRoot)
        {
            warnedBadRoot = true;
            Debug.LogWarning($"CrewCardView on '{name}': Empty Root / Filled Root points at the card itself (or a parent) — ignored. " +
                             "Assign a CHILD object, or leave it empty.", this);
        }
        return false;
    }

    private void OnRankUpClicked()
    {
        if (Member != null && MergePopup != null) MergePopup.Open(Member.instanceId);
    }
}
