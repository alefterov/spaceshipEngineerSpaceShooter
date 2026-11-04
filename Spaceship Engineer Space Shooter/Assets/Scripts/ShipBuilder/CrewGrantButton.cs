using UnityEngine;
using UnityEngine.UI;

public class CrewGrantButton : MonoBehaviour
{
    public CrewMemberDefinition definition;
    [Min(1)] public int copiesPerPress = 1;

    private void Awake()
    {
        if (TryGetComponent<Button>(out var button)) button.onClick.AddListener(Grant);
    }

    public void Grant()
    {
        if (definition == null || GameDataManager.Instance == null) return;
        for (int i = 0; i < copiesPerPress; i++) GameDataManager.Instance.AddCrewMember(definition);
    }
}
