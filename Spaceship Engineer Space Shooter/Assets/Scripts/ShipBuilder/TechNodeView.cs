using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One node in a tech-tree path UI — attach to a per-technology prefab, assign Tech, and wire up
/// whichever of the optional UI fields the prefab actually has. Shows locked/researchable/researched
/// state and researches the tech on button click. Refreshes itself whenever research points change
/// or ANY tech gets researched (a prerequisite finishing might make this node researchable).
/// </summary>
public class TechNodeView : MonoBehaviour
{
    [Header("Data")]
    public TechDefinition tech;

    [Header("UI (assign whichever exist on this prefab)")]
    public Image icon;
    public TMP_Text nameLabel;
    public TMP_Text costLabel;
    public Button researchButton;
    [Tooltip("Shown once the tech is researched.")]
    public GameObject researchedIndicator;
    [Tooltip("Shown while the prerequisite isn't researched yet, regardless of points on hand.")]
    public GameObject lockedIndicator;

    private bool subscribed;

    private void Awake()
    {
        // A RectTransform's Z can drift far from 0 in the Editor (e.g. an accidental Scene-view drag
        // or a stray Layout Group recalculation) without anything looking wrong there — Scene/Game
        // view render UI without strictly enforcing the camera's clip planes. At runtime the UI
        // camera does enforce them, so a large enough Z pushes the node outside near/far clip and it
        // silently disappears in Play mode / the build only. Force it back to 0 defensively.
        var rt = (RectTransform)transform;
        var pos = rt.localPosition;
        if (pos.z != 0f) rt.localPosition = new Vector3(pos.x, pos.y, 0f);
    }

    private void OnEnable()
    {
        if (researchButton != null) researchButton.onClick.AddListener(OnResearchClicked);
        TrySubscribe();
    }

    // Same Start()-fallback pattern as CurrencyDisplay — GameDataManager.Instance may not be set
    // yet during this object's own OnEnable if Awake order puts it first.
    private void Start() => TrySubscribe();

    private void OnDisable()
    {
        if (researchButton != null) researchButton.onClick.RemoveListener(OnResearchClicked);
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        var data = GameDataManager.Instance;
        if (data == null) return;

        data.OnResearchPointsChanged += Refresh;
        data.OnTechResearched += HandleTechResearched;
        subscribed = true;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        var data = GameDataManager.Instance;
        if (data == null) return;

        data.OnResearchPointsChanged -= Refresh;
        data.OnTechResearched -= HandleTechResearched;
    }

    private void HandleTechResearched(string researchedTechId) => Refresh();

    public void Refresh()
    {
        if (tech == null) return;
        var data = GameDataManager.Instance;
        if (data == null) return;

        bool researched = data.IsTechResearched(tech);
        bool prerequisiteMet = tech.prerequisite == null || data.IsTechResearched(tech.prerequisite);
        bool canResearch = !researched && prerequisiteMet && data.ResearchPoints >= tech.researchCost;

        if (icon != null && tech.icon != null) icon.sprite = tech.icon;
        if (nameLabel != null) nameLabel.text = tech.displayName;
        if (costLabel != null) costLabel.text = tech.researchCost.ToString();

        if (researchedIndicator != null) researchedIndicator.SetActive(researched);
        if (lockedIndicator != null) lockedIndicator.SetActive(!researched && !prerequisiteMet);

        if (researchButton != null)
        {
            researchButton.gameObject.SetActive(!researched);
            researchButton.interactable = canResearch;
        }
    }

    private void OnResearchClicked() => GameDataManager.Instance?.TryResearch(tech);
}
