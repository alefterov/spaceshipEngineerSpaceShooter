using UnityEngine;

/// <summary>
/// Open/close panel for the ship's-crew screen (the 4 slots + the collection scroll view). Same plain
/// SetActive pattern as TechTreePopup — everything inside (CrewMemberView slots, CrewCollectionView)
/// re-subscribes and refreshes itself automatically whenever this panel is (re)activated.
/// </summary>
public class CrewPopup : MonoBehaviour
{
    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);
}
