#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DailyRewardsSetup
{
    private const string AssetPath = "Assets/Scripts/ScriptableObject/Rewards/DailyRewards.asset";

    [MenuItem("Tools/Ship Builder/Create Daily Rewards")]
    public static void Create()
    {
        var rewards = AssetDatabase.LoadAssetAtPath<DailyRewards>(AssetPath);
        if (rewards == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scripts/ScriptableObject/Rewards"))
                AssetDatabase.CreateFolder("Assets/Scripts/ScriptableObject", "Rewards");

            rewards = ScriptableObject.CreateInstance<DailyRewards>();
            rewards.FillWithDefaults();
            AssetDatabase.CreateAsset(rewards, AssetPath);
            AssetDatabase.SaveAssets();
        }
        else if (rewards.Count == 0)
        {
            rewards.FillWithDefaults();
            EditorUtility.SetDirty(rewards);
            AssetDatabase.SaveAssets();
        }

        var gameData = Object.FindFirstObjectByType<GameDataManager>(FindObjectsInactive.Include);
        if (gameData == null)
        {
            Debug.LogWarning("DailyRewardsSetup: no GameDataManager in the open scene - assign Daily Rewards on it by hand.", rewards);
        }
        else if (gameData.dailyRewards == null)
        {
            Undo.RecordObject(gameData, "Assign daily rewards");
            gameData.dailyRewards = rewards;
            EditorUtility.SetDirty(gameData);
            EditorSceneManager.MarkSceneDirty(gameData.gameObject.scene);
        }

        Selection.activeObject = rewards;
        EditorGUIUtility.PingObject(rewards);
    }

    [MenuItem("Tools/Ship Builder/Daily Rewards: Simulate Next Day")]
    public static void SimulateNextDay()
    {
        var data = Application.isPlaying ? GameDataManager.Instance : null;
        if (data == null || data.Current == null)
        {
            Debug.LogWarning("DailyRewardsSetup: enter Play mode first.");
            return;
        }

        var last = System.DateTime.Now.Date.AddDays(-1);
        data.Current.dailyLastVisit = last.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        data.RecheckDailyVisit();
    }

    [MenuItem("Tools/Ship Builder/Daily Rewards: Simulate Missed Day")]
    public static void SimulateMissedDay()
    {
        var data = Application.isPlaying ? GameDataManager.Instance : null;
        if (data == null || data.Current == null)
        {
            Debug.LogWarning("DailyRewardsSetup: enter Play mode first.");
            return;
        }

        var last = System.DateTime.Now.Date.AddDays(-2);
        data.Current.dailyLastVisit = last.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        data.RecheckDailyVisit();
    }
}
#endif
