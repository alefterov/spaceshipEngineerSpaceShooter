using UnityEngine;

/// <summary>
/// Open/close panel for the ship's-crew screen — an avatar per role (Captain/Engineer/Gunner/Helmsman)
/// with an upgrade button under each, bought with credits (see CrewMemberView, one per role, and
/// GameDataManager's crew API). Same plain SetActive pattern as TechTreePopup — each CrewMemberView
/// re-subscribes and refreshes itself automatically whenever this panel is (re)activated.
/// </summary>
public class CrewPopup : MonoBehaviour
{
    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);
}
