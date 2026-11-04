using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CrewRandomGrantKey : MonoBehaviour
{
    public Key key = Key.P;
    [Tooltip("How many cards one press gives (to try the fan of several cards).")]
    [Range(1, 9)] public int cardsPerPress = 1;
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

        var members = new List<CrewMemberData>();
        for (int i = 0; i < cardsPerPress; i++)
        {
            var definition = database.allMembers[Random.Range(0, database.allMembers.Count)];
            if (definition == null) continue;

            var member = data.GetCrewMember(data.AddCrewMember(definition));
            if (member != null) members.Add(member);
        }

        var popup = FindAnyObjectByType<CrewCardRewardPopup>(FindObjectsInactive.Include);
        if (popup != null && members.Count > 0) popup.Show(members);
        else if (popup == null) Debug.LogWarning("CrewRandomGrantKey: no CrewCardRewardPopup in the scene (Tools > Ship Builder > Create Crew Card Popup).", this);
    }
}
