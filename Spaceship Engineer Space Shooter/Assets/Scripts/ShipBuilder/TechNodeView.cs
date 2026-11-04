using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        var rt = (RectTransform)transform;
        var pos = rt.localPosition;
        if (pos.z != 0f) rt.localPosition = new Vector3(pos.x, pos.y, 0f);
    }

    private void OnEnable()
    {
        if (researchButton != null) researchButton.onClick.AddListener(OnResearchClicked);
        TrySubscribe();
    }

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
        if (costLabel != null) costLabel.text = NumberFormat.Compact(tech.researchCost);

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
