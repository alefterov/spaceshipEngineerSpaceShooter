using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuFlowController : MonoBehaviour
{
    [Header("Ship")]
    public ShipGrid playerShip;
    public BlockDatabase database;

    [Header("Screens (enable/disable whole panels)")]
    public GameObject mainMenuScreen;
    public GameObject builderScreen;

    [Header("Build screen internals")]
    public BuildModeController buildModeController;

    [Header("Camera")]
    [Tooltip("Centers and fits the ship on screen whenever the menu preview is shown.")]
    public BuildCameraController cameraController;

    [Header("Stats panel")]
    [Tooltip("Slides open automatically when entering the builder, closed when leaving it. A separate " +
             "button wired to SlideOutPanel.Toggle() lets the player open/close it manually at any time too.")]
    public SlideOutPanel statsPanel;

    [Header("Messages")]
    [Tooltip("Optional — shown when the player tries to save a ship with no cockpit.")]
    public MessageToast messageToast;
    [Tooltip("Text shown by messageToast when a save is blocked for missing a cockpit.")]
    public string cockpitRequiredMessage = "Кокпит обязателен для сохранения корабля";
    [Tooltip("Text shown by messageToast when a save is blocked because some blocks are not connected to the rest of the ship.")]
    public string disconnectedBlocksMessage = "Все блоки должны быть соединены с кокпитом";

    [Header("Game mode selection")]
    [Tooltip("Popup listing game modes (Campaign, Survival, ...). Shown when Play is pressed.")]
    public GameModeSelectPopup gameModePopup;
    [Tooltip("Popup with the campaign's level-select map (LevelButtonView buttons you lay out by hand).")]
    public LevelSelectPopup levelSelectPopup;

    private void Start()
    {
        if (buildModeController != null && buildModeController.palette != null) buildModeController.palette.ResetForExit();

        GameDataManager.Instance.ResetFocus();
        playerShip.SetHangarSize(GameDataManager.Instance.HangarColumns, GameDataManager.Instance.HangarRows);
        GameDataManager.Instance.LoadShip(playerShip, database);
        GameDataManager.Instance.BeginBuildSession();
        ShowMainMenu();
    }

    public void OnPlayPressed() => gameModePopup.Open();

    public void OnCampaignPressed()
    {
        gameModePopup.Close();
        levelSelectPopup.Open();
    }

    public void OnSurvivalPressed()
    {
        if (buildModeController != null && buildModeController.palette != null) buildModeController.palette.ResetForExit();

        gameModePopup.Close();
        PendingBattle.Level = null;
        SceneLoader.Load(PendingBattle.BattleSceneName);
    }

    public void OnBuildShipPressed()
    {
        if (buildModeController != null && buildModeController.palette != null) buildModeController.palette.ResetForExit();

        mainMenuScreen.SetActive(false);
        builderScreen.SetActive(true);

        buildModeController.ghost.gameObject.SetActive(true);
        buildModeController.SetCockpitBuildMode();

        playerShip.SetViewMode(ShipViewMode.Building);
        GameDataManager.Instance.BeginBuildSession();
        if (statsPanel != null) statsPanel.Show();
    }

    public void OnSaveShipPressed()
    {
        if (!playerShip.HasCockpit)
        {
            if (messageToast != null) messageToast.Show(cockpitRequiredMessage);
            return;
        }

        if (!playerShip.AreAllBlocksConnected())
        {
            if (messageToast != null) messageToast.Show(disconnectedBlocksMessage);
            return;
        }

        GameDataManager.Instance.SaveShip(playerShip);
    }

    public void OnExitBuilderPressed()
    {
        GameDataManager.Instance.RevertShip(playerShip, database);
        GameDataManager.Instance.RevertCredits();

        buildModeController.ResetForExit();
        buildModeController.ghost.gameObject.SetActive(false);

        ShowMainMenu();
    }

    public void ShowFleetShip(int index)
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        index = Mathf.Clamp(index, 0, data.Fleet.Count - 1);
        data.ShowFleetShip(playerShip, database, index);
        data.FocusShip(index);
        playerShip.SetViewMode(ShipViewMode.Preview);
        if (cameraController != null) cameraController.FrameShip();
    }

    private void ShowMainMenu()
    {
        var data = GameDataManager.Instance;
        if (data != null && data.FocusedShipIndex != data.SelectedShipIndex)
        {
            data.ShowFleetShip(playerShip, database, data.SelectedShipIndex);
            data.ResetFocus();
        }

        if (buildModeController != null && buildModeController.palette != null) buildModeController.palette.ResetForExit();

        builderScreen.SetActive(false);
        mainMenuScreen.SetActive(true);
        buildModeController.ghost.gameObject.SetActive(false);

        playerShip.SetViewMode(ShipViewMode.Preview);
        if (cameraController != null) cameraController.FrameShip();
        if (statsPanel != null) statsPanel.Hide();
    }
}
