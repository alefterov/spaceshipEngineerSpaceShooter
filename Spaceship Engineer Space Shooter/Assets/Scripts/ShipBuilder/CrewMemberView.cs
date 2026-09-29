using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One crew member's card inside the crew popup: avatar, role name, current level, next level's cost,
/// and an upgrade button — one instance per CrewRole (Captain/Engineer/Gunner/Helmsman), each reading
/// and spending against GameDataManager's crew API. All 4 roles are treated identically — just an
/// upgrade, no naming or avatar-picking flow for any of them (Captain included).
///
/// SETUP: put one of these per role inside the crew popup, set Role and Role Display Name (e.g.
/// "Капитан", "Инженер", "Стрелок", "Рулевой"), and assign Avatar Image its own fixed sprite directly
/// in the Inspector like any other artwork — it never changes at runtime.
/// </summary>
public class CrewMemberView : MonoBehaviour
{
    [Header("Which crew member")]
    public CrewRole role;
    public string roleDisplayName = "";

    [Header("UI (assign whichever exist)")]
    public Image avatarImage;
    public TMP_Text nameLabel;
    [Tooltip("Current level, formatted like \"1 LV\".")]
    public TMP_Text levelLabel;
    [Tooltip("Credits cost of the next level.")]
    public TMP_Text costLabel;
    public Button upgradeButton;

    private bool subscribed;

    private void OnEnable()
    {
        if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeClicked);
        if (nameLabel != null) nameLabel.text = roleDisplayName; // fixed — never changes at runtime
        TrySubscribe();
    }

    // Start()-fallback, same reasoning as UpgradeButtonView/TechNodeView: GameDataManager.Instance may
    // not be set yet during this object's own OnEnable if Awake order puts it first.
    private void Start() => TrySubscribe();

    private void OnDisable()
    {
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        var data = GameDataManager.Instance;
        if (data == null) return;

        data.OnCrewChanged += HandleCrewChanged;
        data.OnCreditsChanged += Refresh; // affordability (upgradeButton.interactable) depends on this too

        subscribed = true;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        var data = GameDataManager.Instance;
        if (data == null) return;

        data.OnCrewChanged -= HandleCrewChanged;
        data.OnCreditsChanged -= Refresh;
    }

    private void HandleCrewChanged(CrewRole changedRole)
    {
        if (changedRole == role) Refresh();
    }

    public void Refresh()
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        int level = data.GetCrewLevel(role);
        int cost = data.GetCrewUpgradeCost(role);

        if (levelLabel != null) levelLabel.text = $"{level} LV";
        if (costLabel != null) costLabel.text = cost.ToString();
        if (upgradeButton != null) upgradeButton.interactable = data.CanUpgradeCrew(role);
    }

    private void OnUpgradeClicked() => GameDataManager.Instance.TryUpgradeCrew(role);
}
