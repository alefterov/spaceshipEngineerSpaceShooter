using UnityEngine;

/// <summary>
/// Open/close panel for the campaign's level-select map. Wire the "Campaign" button (inside
/// GameModeSelectPopup) onClick to Open(), and this popup's own close button's onClick to Close().
///
/// The level info panel (LevelInfoPanel) belongs to the map: assign it to Info Panel and it is shown whenever the map
/// is visible and hidden whenever the map is hidden, however the map was switched. (Putting the panel inside this
/// popup's hierarchy does the same by itself — then leave the field empty.)
///
/// Deliberately its own component rather than reusing TechTreePopup/GameModeSelectPopup — kept
/// separate so each screen's popup can evolve independently later without the others stepping on it,
/// even though right now they're all just a SetActive toggle.
/// </summary>
public class LevelSelectPopup : MonoBehaviour
{
    [Tooltip("The level info panel that is shown together with the map. Leave empty if it already sits inside this popup.")]
    public LevelInfoPanel infoPanel;

    [Tooltip("The game-mode screen (Campaign / Survival) this map was opened from. Back() closes the map and shows it again.")]
    public GameModeSelectPopup gameModePopup;

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    /// <summary>Wire the map's BACK button to this: closes the level map and returns to the game-mode selection.</summary>
    public void Back()
    {
        Close();
        if (gameModePopup != null) gameModePopup.Open();
    }
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

    private void OnEnable()
    {
        if (TryGetComponent<CampaignSelector>(out _)) return; // it shows the panel only on a campaign's map
        if (infoPanel != null && !infoPanel.transform.IsChildOf(transform)) infoPanel.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (infoPanel != null && !infoPanel.transform.IsChildOf(transform)) infoPanel.gameObject.SetActive(false);
    }
}
