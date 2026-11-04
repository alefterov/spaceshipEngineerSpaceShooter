using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameSpeed
{
    private const string Key = "GameSpeed";

    private static float value = -1f;

    public static float Value
    {
        get
        {
            if (value < 0f) value = Mathf.Max(0.1f, PlayerPrefs.GetFloat(Key, 1f));
            return value;
        }
    }

    public static float SceneScale => InBattle ? Value : 1f;

    private static bool InBattle => SceneManager.GetActiveScene().name == PendingBattle.BattleSceneName;

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
