using UnityEngine;

/// <summary>
/// Open/close panel for the game-mode selection screen (Campaign, Survival, more later). Wire the
/// main menu's "Play" button onClick to Open(), and this popup's own close button's onClick to
/// Close().
///
/// Deliberately its own component rather than reusing TechTreePopup — kept separate so each screen's
/// popup can evolve independently later without the two stepping on each other, even though right now
/// they're both just a SetActive toggle.
/// </summary>
public class GameModeSelectPopup : MonoBehaviour
{
    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);
}
