using UnityEngine;

/// <summary>
/// Owns the first scene's two screens: Main Menu (closed-hull ship preview + Play/Build buttons)
/// and Ship Builder (exposed-hull view + block palette). Both live in this one scene —
/// only the battle itself is a separate scene, loaded later once that flow exists.
/// </summary>
public class MainMenuFlowController : MonoBehaviour
{
    [Header("Ship")]
    public ShipGrid playerShip;
    public BlockDatabase database;

    [Header("Screens (enable/disable whole panels)")]
    public GameObject mainMenuScreen;   // Play / Build Ship / Settings buttons, ship preview camera framing
    public GameObject builderScreen;    // palette, mode tabs, rotate button — the existing build UI

    [Header("Build screen internals")]
    public BuildModeController buildModeController;

    private void Start()
    {
        // GameDataManager already loaded the save file in its own Awake (runs before this Start,
        // as long as it lives in the same scene or a bootstrap scene loaded earlier).
        GameDataManager.Instance.LoadShip(playerShip, database);
        ShowMainMenu();
    }

    /// <summary>Wire to a "Play" button — battle scene doesn't exist yet, so this is a stub for now.</summary>
    public void OnPlayPressed()
    {
        Debug.Log("TODO: load battle scene once it exists — e.g. SceneManager.LoadScene(\"Battle\")");
        // UnityEngine.SceneManagement.SceneManager.LoadScene("Battle");
    }

    /// <summary>Wire to a "Build Ship" / "Ангар" button on the main menu.</summary>
    public void OnBuildShipPressed()
    {
        mainMenuScreen.SetActive(false);
        builderScreen.SetActive(true);

        // The input catcher (GhostBlockController's full-screen UI Image) may live outside the
        // builderScreen hierarchy — explicitly (re)activate it and reset build state here rather
        // than relying only on SetActive(builderScreen), or taps would keep working after exit.
        buildModeController.ghost.gameObject.SetActive(true);
        buildModeController.SetHullBuildMode(); // fresh, predictable state every time we enter

        playerShip.SetViewMode(ShipViewMode.Building); // strip the roof so the grid/internals read clearly
    }

    /// <summary>Wire to a "Save & Exit" button inside the builder screen.</summary>
    public void OnExitBuilderPressed()
    {
        GameDataManager.Instance.SaveShip(playerShip);

        // Bug fix: without this, the input catcher stayed active and taps in the main menu
        // would still place/delete blocks. Clear all transient state AND disable the catcher.
        buildModeController.ResetForExit();
        buildModeController.ghost.gameObject.SetActive(false);

        ShowMainMenu();
    }

    private void ShowMainMenu()
    {
        builderScreen.SetActive(false);
        mainMenuScreen.SetActive(true);
        buildModeController.ghost.gameObject.SetActive(false); // safety: never listen for taps outside the builder

        playerShip.SetViewMode(ShipViewMode.Preview); // put the roof back on for the menu preview
    }
}