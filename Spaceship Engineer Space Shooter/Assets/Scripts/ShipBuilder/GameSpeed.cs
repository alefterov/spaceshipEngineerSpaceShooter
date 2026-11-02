using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The speed of the battle (0.5x, 1x, 2x...), chosen in the settings (GameSpeedSlider) and saved on the device. It is applied only in the
/// battle scene: whenever a scene loads, Time.timeScale becomes this speed in the battle and 1 everywhere else (the menus never run fast).
/// The pauses (game menu, priorities window) resume to SceneScale, so a speed changed while paused is used right after.
/// </summary>
public static class GameSpeed
{
    private const string Key = "GameSpeed";

    private static float value = -1f;

    /// <summary>The chosen battle speed (1 = normal).</summary>
    public static float Value
    {
        get
        {
            if (value < 0f) value = Mathf.Max(0.1f, PlayerPrefs.GetFloat(Key, 1f));
            return value;
        }
    }

    /// <summary>The time scale the current scene should run at: Value in the battle, 1 anywhere else.</summary>
    public static float SceneScale => InBattle ? Value : 1f;

    private static bool InBattle => SceneManager.GetActiveScene().name == PendingBattle.BattleSceneName;

    /// <summary>Chooses and saves a new battle speed. In a running (not paused) battle it takes effect at once.</summary>
    public static void Set(float speed)
    {
        value = Mathf.Max(0.1f, speed);
        PlayerPrefs.SetFloat(Key, value);
        PlayerPrefs.Save();

        if (InBattle && Time.timeScale > 0f) Time.timeScale = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Time.timeScale = SceneScale;
    }
}
