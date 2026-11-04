using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CrewMemberView : MonoBehaviour, ICrewMemberProvider, ICrewDropTarget, ICrewReleaseHandler
{
    [Header("Which slot")]
    public CrewRole role;
    [Tooltip("Shown in the name label while the slot is empty — e.g. \"Капитан\".")]
    public string roleDisplayName = "";

    [Header("UI (assign whichever exist)")]
    [Tooltip("The portrait — normally the slot's own root Image. Shows the member's picture for its rank; " +
             "goes back to its original sprite while the slot is empty.")]
    public Image avatarImage;
    public TMP_Text nameLabel;
    [Tooltip("Current level, formatted like \"3 LV\".")]
    public TMP_Text levelLabel;
    [Tooltip("Formatted with Rank Format.")]
    public TMP_Text rankLabel;
    public string rankFormat = "Ранг {0}";
    [Tooltip("The three SkillItems, in order — perk 1 first.")]
    public CrewPerkView[] perkViews;

    [Header("Level up")]
    [Tooltip("Credits cost of the next level — shown on the button.")]
    public TMP_Text costLabel;
    public Button upgradeButton;

    [Header("Rank up (optional)")]
    [Tooltip("Opens the merge popup for the member standing here. Hidden at max rank / on an empty slot; greyed out until two spare identical cards (same kind, same rank) are in the collection.")]
    public Button rankUpButton;
    public CrewMergePopup mergePopup;

    public CrewMemberData Member { get; private set; }
    public Image PortraitImage => avatarImage;
    public bool CanDrag => true;

    private Sprite emptySprite;
    private bool subscribed;

    private void Awake()
    {
        if (avatarImage != null) emptySprite = avatarImage.sprite;
        if (!TryGetComponent<CrewDragHandle>(out _)) gameObject.AddComponent<CrewDragHandle>();
    }

    private void OnEnable()
    {
        if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeClicked);
        if (rankUpButton != null) rankUpButton.onClick.AddListener(OnRankUpClicked);
        TrySubscribe();
    }

    private void Start() => TrySubscribe();

    private void OnDisable()
    {
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        if (rankUpButton != null) rankUpButton.onClick.RemoveListener(OnRankUpClicked);
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        var data = GameDataManager.Instance;
        if (data == null) return;

        data.OnCrewChanged += Refresh;
        data.OnCreditsChanged += Refresh;

        subscribed = true;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        var data = GameDataManager.Instance;
        if (data == null) return;

        data.OnCrewChanged -= Refresh;
        data.OnCreditsChanged -= Refresh;
    }

    public void Refresh()
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        var member = data.GetAssignedMember(role);
        var definition = data.GetCrewDefinition(member);
        bool filled = member != null && definition != null;
        Member = filled ? member : null;

        if (avatarImage != null)
        {
            var sprite = filled ? definition.GetRankSprite(member.rank) : null;
            avatarImage.sprite = sprite != null ? sprite : emptySprite;
        }

        if (nameLabel != null) nameLabel.text = filled ? definition.displayName : roleDisplayName;

        if (levelLabel != null)
        {
            levelLabel.gameObject.SetActive(filled);
            if (filled) levelLabel.text = $"{member.level} LV";
        }

        if (rankLabel != null)
        {
            rankLabel.gameObject.SetActive(filled);
            if (filled) rankLabel.text = string.Format(rankFormat, member.rank);
        }

        CrewPerkView.Populate(perkViews, definition, filled ? member : null);

        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(filled);
            if (filled) upgradeButton.interactable = data.CanAffordCrewLevelUp(member);
        }

        if (costLabel != null && filled) costLabel.text = NumberFormat.Compact(data.GetCrewLevelUpCost(member));

        if (rankUpButton != null)
        {
            rankUpButton.gameObject.SetActive(filled && mergePopup != null && data.CanRankUpCrew(member));
            if (filled) rankUpButton.interactable = data.CanMergeCrew(member);
        }
    }

    private void OnUpgradeClicked()
    {
        if (Member != null) GameDataManager.Instance.TryLevelUpCrew(Member.instanceId);
    }

    private void OnRankUpClicked()
    {
        if (Member != null && mergePopup != null) mergePopup.Open(Member.instanceId);
    }

    public bool CanAccept(CrewMemberData member)
    {
        var data = GameDataManager.Instance;
        var definition = data != null ? data.GetCrewDefinition(member) : null;
        return definition != null && definition.role == role;
    }

    public void Drop(CrewMemberData member) => GameDataManager.Instance.TryAssignToSlot(member.instanceId, role);

    public void OnReleasedOutside(CrewMemberData member) => GameDataManager.Instance.UnassignCrew(member.instanceId);
}
