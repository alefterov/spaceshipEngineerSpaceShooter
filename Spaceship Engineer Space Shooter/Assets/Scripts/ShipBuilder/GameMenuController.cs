using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Tooltip("Starts the tutorial over and closes the menu. Empty = a child named TutorialButton.")]
    public Button tutorialButton;
    [Tooltip("Back to the main scene. Shown ONLY in the battle scene; hidden everywhere else.")]
    public Button homeButton;
    [Tooltip("The scene Home leads to.")]
    public string homeSceneName = "SampleScene";

    [Header("Battle")]
    [Tooltip("In the battle scene the game stops while the menu is open.")]
    public bool pauseInBattle = true;

    private bool paused;

    private static bool InBattleScene => SceneManager.GetActiveScene().name == PendingBattle.BattleSceneName;

    public bool IsOpen => menuPanel != null && menuPanel.activeSelf;

    private void Awake()
    {
        if (menuPanel != null) menuPanel.SetActive(false);

        if (homeButton != null) homeButton.gameObject.SetActive(InBattleScene);

        if (tutorialButton == null && menuPanel != null)
        {
            var found = menuPanel.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "TutorialButton");
            if (found != null) tutorialButton = found;
        }
    }

    private void OnEnable()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (exitButton != null) exitButton.onClick.AddListener(ExitGame);
        if (homeButton != null) homeButton.onClick.AddListener(GoHome);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(RestartTutorial);
    }

    private void OnDisable()
    {
        if (openButton != null) openButton.onClick.RemoveListener(Open);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (exitButton != null) exitButton.onClick.RemoveListener(ExitGame);
        if (homeButton != null) homeButton.onClick.RemoveListener(GoHome);
        if (tutorialButton != null) tutorialButton.onClick.RemoveListener(RestartTutorial);

        Resume();
    }

    private void Update()
    {
        if (toggleOnEscape && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Toggle();
    }

    public void Open()
    {
        if (menuPanel == null || menuPanel.activeSelf) return;

        menuPanel.SetActive(true);

        if (pauseInBattle && InBattleScene && !paused && Time.timeScale > 0f)
        {
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

    public void ExitGame()
    {
        Resume();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void RestartTutorial()
    {
        var data = GameDataManager.Instance;
        if (data != null) data.ResetTutorial();
        TutorialController.AllowAgain();
        Close();
    }

    public void GoHome()
    {
        Resume();
        PendingBattle.Level = null;
        SceneLoader.Load(homeSceneName);
    }

    private void Resume()
    {
        if (!paused) return;

        Time.timeScale = GameSpeed.SceneScale;
        paused = false;
    }
}
