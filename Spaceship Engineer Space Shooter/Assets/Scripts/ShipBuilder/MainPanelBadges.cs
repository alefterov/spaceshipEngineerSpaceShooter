using UnityEngine;

/// <summary>
/// The "something is waiting for you" marks (yellow dots) in the corner of the main panel's buttons:
///  - Rewards: a reached player level's reward or a finished task can be taken.
///  - Technology: some technology can be researched right now (its prerequisite is done and there are enough points).
///  - Crew: a crew member was received and not looked at yet — goes out once the crew screen has been opened.
/// Each dot is shown while its reason is there and hidden otherwise. Checks a few times a second, so it follows every change by itself
/// (a task finished in battle, research points earned, a card received).
///
/// SETUP: put on MainPanel. Make each dot picture a child of its button (RewardButton, TechnologyButton, CrewButton), anchored to the
/// top-right corner, and drag the dots into the fields. Drag the crew screen (CrewPanel) into Crew Screen. A field left empty is simply skipped.
/// </summary>
public class MainPanelBadges : MonoBehaviour
{
    [Header("Dots")]
    [Tooltip("The dot on the rewards button (RewardButton).")]
    public GameObject rewardsBadge;
    [Tooltip("The dot on the technology button (TechnologyButton).")]
    public GameObject technologyBadge;
    [Tooltip("The dot on the crew button (CrewButton).")]
    public GameObject crewBadge;

    [Header("Sources")]
    [Tooltip("The technologies to check. Empty = the Tech Database of the builder's BuildPaletteUI.")]
    public TechDatabase techDatabase;
    [Tooltip("The crew screen. Once it is shown, the new crew members count as seen and the crew dot goes out.")]
    public GameObject crewScreen;

    [Header("Look")]
    [Tooltip("How often (seconds) to check.")]
    [Min(0.05f)] public float checkInterval = 0.25f;
    [Tooltip("Gentle size pulse of a shown dot: how much bigger at the peak (0.1 = 10%). 0 = no pulse.")]
    [Min(0f)] public float pulseAmount = 0.1f;
    [Tooltip("Pulses per second.")]
    [Min(0f)] public float pulseSpeed = 1.2f;

    private GameObject[] badges;
    private Vector3[] baseScales;
    private float nextCheck;

    private void Awake()
    {
        badges = new[] { rewardsBadge, technologyBadge, crewBadge };
        baseScales = new Vector3[badges.Length];
        for (int i = 0; i < badges.Length; i++)
        {
            if (badges[i] != null) baseScales[i] = badges[i].transform.localScale;
        }

        if (techDatabase == null)
        {
            var palette = FindAnyObjectByType<BuildPaletteUI>(FindObjectsInactive.Include);
            if (palette != null) techDatabase = palette.techDatabase;
        }
    }

    private void OnEnable() => nextCheck = 0f;

    private void Update()
    {
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + checkInterval;
            Refresh();
        }

        Pulse();
    }

    private void Refresh()
    {
        var data = GameDataManager.Instance;
        bool ready = data != null && data.Current != null;

        if (ready && crewScreen != null && crewScreen.activeInHierarchy) data.MarkCrewSeen();

        Show(rewardsBadge, ready && data.HasClaimableRewards());
        Show(technologyBadge, ready && data.CanResearchAny(techDatabase));
        Show(crewBadge, ready && data.HasNewCrew);
    }

    private static void Show(GameObject badge, bool value)
    {
        if (badge != null && badge.activeSelf != value) badge.SetActive(value);
    }

    private void Pulse()
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f);
        for (int i = 0; i < badges.Length; i++)
        {
            if (badges[i] == null || !badges[i].activeSelf) continue;
            badges[i].transform.localScale = baseScales[i] * (1f + pulseAmount * wave);
        }
    }
}
