using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// TESTING HELPER: pressing the key (P by default) gives the player Cards Per Press random crew cards — rank 1, level 1
/// copies of random characters from the Crew Database (GameDataManager.Crew Database > All Members) — and shows
/// them in the crew card window (CrewCardRewardPopup) exactly as a reward would, for the window's own Visible Seconds. Put it on
/// any active object that exists on the crew screen's scene (for example the CrewPanel's parent or the
/// GameDataManager object). Remove it before release — crew will come from rewards/shop.
/// </summary>
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
            Debug.Log($"Crew card granted: {definition.displayName}");
        }

        // Show them the way a reward is shown: in the crew card window, with its animation.
        var popup = FindAnyObjectByType<CrewCardRewardPopup>(FindObjectsInactive.Include);
        if (popup != null && members.Count > 0) popup.Show(members);
        else if (popup == null) Debug.LogWarning("CrewRandomGrantKey: no CrewCardRewardPopup in the scene (Tools > Ship Builder > Create Crew Card Popup).", this);
    }
}
