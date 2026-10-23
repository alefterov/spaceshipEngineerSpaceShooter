using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The game menu — the same component in the main scene and in the battle scene. A button opens the menu panel, a
/// button closes it, and from the panel the player can quit the game or (in a battle only) leave to the main scene.
/// The settings sliders (AudioSettingsPanel) can sit inside this panel.
///
///  - Open / Close: Open Button shows the Menu Panel, Close Button hides it (the Escape key — the Back button on
///    Android — toggles it too, if Toggle On Escape is on). In a battle the game is PAUSED while the menu is open
///    (Pause In Battle), and the previous speed is restored when it closes.
///  - Exit Button: quits the game (stops Play mode in the editor).
///  - Home Button: back to the main scene. It exists only in the battle scene — in any other scene the button is
///    hidden. Leaving a battle this way pays no rewards.
///
/// SETUP: put on any always-active object of the scene (e.g. the canvas), assign the Menu Panel and whichever buttons
/// the scene has (leave the others empty — the main scene has no Home button, say). The buttons are wired in code;
/// Open()/Close()/Toggle()/ExitGame()/GoHome() are also public if you prefer wiring OnClick by hand.
/// </summary>
public class GameMenuController : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("The menu panel. Starts hidden — Open Button shows it.")]
    public GameObject menuPanel;
    public Button openButton;
    public Button closeButton;
    [Tooltip("The Escape key (the Back button on Android) opens and closes the menu.")]
    public bool toggleOnEscape = true;

    [Header("Leaving")]
    [Tooltip("Quits the game.")]
    public Button exitButton;
    [Tooltip("Back to the main scene. Shown ONLY in the battle scene; hidden everywhere else.")]
    public Button homeButton;
    [Tooltip("The scene Home leads to.")]
    public string homeSceneName = "SampleScene";

    [Header("Battle")]
    [Tooltip("In the battle scene the game stops while the menu is open.")]
    public bool pauseInBattle = true;

    private float savedTimeScale = 1f;
    private bool paused;

    /// <summary>Whether this is the battle scene (the one PendingBattle loads).</summary>
    private static bool InBattleScene => SceneManager.GetActiveScene().name == PendingBattle.BattleSceneName;

    public bool IsOpen => menuPanel != null && menuPanel.activeSelf;

    private void Awake()
    {
        if (menuPanel != null) menuPanel.SetActive(false);

        // The Home button only makes sense in a battle.
        if (homeButton != null) homeButton.gameObject.SetActive(InBattleScene);
    }

    private void OnEnable()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (exitButton != null) exitButton.onClick.AddListener(ExitGame);
        if (homeButton != null) homeButton.onClick.AddListener(GoHome);
    }

    private void OnDisable()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Open);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (exitButton != null) exitButton.onClick.RemoveListener(ExitGame);
        if (homeButton != null) homeButton.onClick.RemoveListener(GoHome);

        Resume(); // never leave the game paused behind a menu that no longer exists
    }

    private void Update()
    {
        if (toggleOnEscape && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Toggle();
    }

    public void Open()
    {
        if (menuPanel == null || menuPanel.activeSelf) return;

        menuPanel.SetActive(true);

        if (pauseInBattle && InBattleScene && !paused)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            paused = true;
        }
    }

    public void Close()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        Resume();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    /// <summary>Quits the game (in the editor: stops Play mode).</summary>
    public void ExitGame()
    {
        Resume();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Leaves the battle for the main scene.</summary>
    public void GoHome()
    {
        Resume();
        PendingBattle.Level = null;
        SceneManager.LoadScene(homeSceneName);
    }

    private void Resume()
    {
        if (!paused) return;

        Time.timeScale = savedTimeScale;
        paused = false;
    }
}
