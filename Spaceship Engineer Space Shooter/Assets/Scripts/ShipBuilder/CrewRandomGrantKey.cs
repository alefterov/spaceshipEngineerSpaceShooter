using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TESTING HELPER: pressing the key (P by default) gives the player one random crew card — a rank 1, level 1
/// copy of a random character from the Crew Database (GameDataManager.Crew Database > All Members). Put it on
/// any active object that exists on the crew screen's scene (for example the CrewPanel's parent or the
/// GameDataManager object). Remove it before release — crew will come from rewards/shop.
/// </summary>
public class CrewRandomGrantKey : MonoBehaviour
{
    public Key key = Key.P;
    [Tooltip("Only react while this object (e.g. the crew panel) is shown. Leave empty to react always.")]
    public GameObject onlyWhileActive;

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current[key].wasPressedThisFrame) return;
        if (onlyWhileActive != null && !onlyWhileActive.activeInHierarchy) return;

        var data = GameDataManager.Instance;
        var database = data != null ? data.crewDatabase : null;
        if (database == null || database.allMembers == null || database.allMembers.Count == 0)
        {
            Debug.LogWarning("CrewRandomGrantKey: no Crew Database assigned on GameDataManager, or it has no members.", this);
            return;
        }

        var definition = database.allMembers[Random.Range(0, database.allMembers.Count)];
        if (definition == null) return;

        data.AddCrewMember(definition);
        Debug.Log($"Crew card granted: {definition.displayName}");
    }
}
