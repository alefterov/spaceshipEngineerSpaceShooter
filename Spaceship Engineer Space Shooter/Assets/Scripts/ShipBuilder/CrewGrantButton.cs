using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TESTING HELPER: a button that hands the player copies of one crew member, so you can try dragging,
/// levelling and merging without a shop. Put on any Button, assign Definition, press it as many times as you
/// need copies. Remove the button (or this component) before release — crew will come from rewards/shop.
/// </summary>
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
