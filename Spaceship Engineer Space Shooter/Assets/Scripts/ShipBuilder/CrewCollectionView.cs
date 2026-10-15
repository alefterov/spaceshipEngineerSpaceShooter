using System.Linq;
using UnityEngine;

/// <summary>
/// The scroll view full of crew cards: every crew member the player owns who is NOT standing in a slot.
/// Rebuilds itself whenever crew changes. Also a drop target — dragging a member from a slot onto the
/// scroll view takes it off the slot.
///
/// SETUP: put on the "Scroll View" object (not on Content — drops are detected walking UP from whatever
/// is under the finger, and the scroll view's Image/Viewport must be a raycast target). Assign Content (the
/// ScrollRect's content) and Card Prefab. The card prefab may be either a prefab asset or the template card
/// sitting in Content in the scene — a scene template is switched off and used as the model, never destroyed.
/// </summary>
public class CrewCollectionView : MonoBehaviour, ICrewDropTarget
{
    [Tooltip("The ScrollRect's Content — cards are created inside it.")]
    public Transform content;
    [Tooltip("The card to copy for each member: needs a CrewCardView.")]
    public CrewCardView cardPrefab;
    [Tooltip("Handed to every card so its Rank Up Button can open it. Optional.")]
    public CrewMergePopup mergePopup;

    private bool subscribed;
    private bool warnedNoDatabase;

    private void Awake()
    {
        // A card placed in the scene as a template must not show up as a real card.
        if (cardPrefab != null && cardPrefab.gameObject.scene.IsValid()) cardPrefab.gameObject.SetActive(false);
    }

    private void OnEnable() => TrySubscribe();
    private void Start() => TrySubscribe();
    private void OnDisable() => Unsubscribe();

    private void TrySubscribe()
    {
        if (subscribed) return;
        var data = GameDataManager.Instance;
        if (data == null) return;

        data.OnCrewChanged += Rebuild;
        subscribed = true;
        Rebuild();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        var data = GameDataManager.Instance;
        if (data != null) data.OnCrewChanged -= Rebuild;
    }

    public void Rebuild()
    {
        var data = GameDataManager.Instance;
        if (data == null || content == null || cardPrefab == null) return;

        if (data.crewDatabase == null && !warnedNoDatabase)
        {
            warnedNoDatabase = true;
            Debug.LogWarning("Crew collection is empty because GameDataManager has no Crew Database assigned.", this);
        }

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i).gameObject;
            if (child == cardPrefab.gameObject) continue; // the scene template stays
            Destroy(child);
        }

        var members = data.CrewMembers
            .Where(m => !data.IsCrewAssigned(m.instanceId))
            .Select(m => (member: m, definition: data.GetCrewDefinition(m)))
            .Where(x => x.definition != null)
            .OrderBy(x => (int)x.definition.role)
            .ThenByDescending(x => x.member.rank)
            .ThenByDescending(x => x.member.level)
            .ThenBy(x => x.definition.displayName);

        foreach (var (member, _) in members)
        {
            var card = Instantiate(cardPrefab, content);
            card.MergePopup = mergePopup;
            card.Bind(member);
            card.gameObject.SetActive(true);
        }
    }

    // ---------- Drop target: only a member standing in a slot can be dropped here ----------

    public bool CanAccept(CrewMemberData member)
        => GameDataManager.Instance != null && GameDataManager.Instance.IsCrewAssigned(member.instanceId);

    public void Drop(CrewMemberData member) => GameDataManager.Instance.UnassignCrew(member.instanceId);
}
