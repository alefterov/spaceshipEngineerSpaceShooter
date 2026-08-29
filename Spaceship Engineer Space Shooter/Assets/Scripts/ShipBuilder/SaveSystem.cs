using System.IO;
using UnityEngine;

/// <summary>
/// Raw JSON read/write for GameData. No game logic here — GameDataManager
/// is the one that decides when to call these.
/// </summary>
public static class SaveSystem
{
    private const string FileName = "gamedata.json";

    private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public static void Save(GameData data)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
    }

    /// <summary>Returns null if no save file exists yet (fresh install / first launch).</summary>
    public static GameData Load()
    {
        if (!File.Exists(SavePath)) return null;

        try
        {
            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<GameData>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save file corrupted, starting fresh. {e}");
            return null;
        }
    }

    public static bool HasSave() => File.Exists(SavePath);

    public static void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
    }
}
