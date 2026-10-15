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
public class CrewCardView : MonoBehaviour, ICrewMemberProvider
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

    public void Bind(CrewMemberData member)
    {
        Init();

        var data = GameDataManager.Instance;
        var definition = member != null && data != null ? data.GetCrewDefinition(member) : null;
        bool filled = member != null && definition != null;
        Member = filled ? member : null;

        if (filledRoot != null) filledRoot.SetActive(filled);
        if (emptyRoot != null) emptyRoot.SetActive(!filled);

        if (avatarImage != null)
        {
            var sprite = filled ? definition.GetRankSprite(member.rank) : null;
            avatarImage.sprite = sprite != null ? sprite : emptySprite;
        }

        if (nameLabel != null) nameLabel.text = filled ? definition.displayName : "";
        if (levelLabel != null) levelLabel.text = filled ? $"{member.level} LV" : "";
        if (rankLabel != null) rankLabel.text = filled ? string.Format(rankFormat, member.rank) : "";

        CrewPerkView.Populate(perkViews, definition, filled ? member : null);

        if (rankUpButton != null)
            rankUpButton.gameObject.SetActive(filled && MergePopup != null && data.CanRankUpCrew(member));
    }

    private void OnRankUpClicked()
    {
        if (Member != null && MergePopup != null) MergePopup.Open(Member.instanceId);
    }
}
