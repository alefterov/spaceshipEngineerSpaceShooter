using UnityEngine;

public class LevelSelectPopup : MonoBehaviour
{
    [Tooltip("The level info panel that is shown together with the map. Leave empty if it already sits inside this popup.")]
    public LevelInfoPanel infoPanel;

    [Tooltip("The game-mode screen (Campaign / Survival) this map was opened from. Back() closes the map and shows it again.")]
    public GameModeSelectPopup gameModePopup;

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);

    public void Back()
    {
        Close();
        if (gameModePopup != null) gameModePopup.Open();
    }
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

    private void OnEnable()
    {
        if (TryGetComponent<CampaignSelector>(out _)) return;
        if (infoPanel != null && !infoPanel.transform.IsChildOf(transform)) infoPanel.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (infoPanel != null && !infoPanel.transform.IsChildOf(transform)) infoPanel.gameObject.SetActive(false);
    }
}
