using UnityEngine;

/// <summary>
/// Open/close panel for the campaign's level-select map. Wire the "Campaign" button (inside
/// GameModeSelectPopup) onClick to Open(), and this popup's own close button's onClick to Close().
///
/// Deliberately its own component rather than reusing TechTreePopup/GameModeSelectPopup — kept
/// separate so each screen's popup can evolve independently later without the others stepping on it,
/// even though right now they're all just a SetActive toggle.
/// </summary>
public class LevelSelectPopup : MonoBehaviour
{
    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);
}
