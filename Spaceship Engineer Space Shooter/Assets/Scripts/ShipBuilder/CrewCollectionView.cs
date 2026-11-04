using System.Linq;
using UnityEngine;

public class CrewCollectionView : MonoBehaviour, ICrewDropTarget
{
    [Tooltip("The ScrollRect's Content — cards are created inside it.")]
    public Transform content;
    [Tooltip("The card to copy for each member: needs a CrewCardView.")]
    public CrewCardView cardPrefab;

    private bool subscribed;
    private bool warnedNoDatabase;

    private void Awake()
    {
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
            if (child == cardPrefab.gameObject) continue;
            Destroy(child);
        }

        var groups = data.CrewMembers
            .Where(m => !data.IsCrewAssigned(m.instanceId))
            .Select(m => (member: m, definition: data.GetCrewDefinition(m)))
            .Where(x => x.definition != null)
            .GroupBy(x => (x.member.definitionId, x.member.rank, x.member.level))
            .Select(g => (best: g.First(), count: g.Count()))
            .OrderBy(g => (int)g.best.definition.role)
            .ThenBy(g => g.best.definition.displayName)
            .ThenByDescending(g => g.best.member.rank)
            .ThenByDescending(g => g.best.member.level);

        foreach (var (best, count) in groups)
        {
            var card = Instantiate(cardPrefab, content);
            card.Bind(best.member, count);
            card.gameObject.SetActive(true);
        }
    }

    public bool CanAccept(CrewMemberData member)
        => GameDataManager.Instance != null && GameDataManager.Instance.IsCrewAssigned(member.instanceId);

    public void Drop(CrewMemberData member) => GameDataManager.Instance.UnassignCrew(member.instanceId);
}
