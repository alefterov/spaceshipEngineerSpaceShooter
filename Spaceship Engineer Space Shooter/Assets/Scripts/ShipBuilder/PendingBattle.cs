/// <summary>
/// Hands off which level was picked across the scene load into the battle scene — a plain static
/// field, since a MonoBehaviour's own state doesn't survive SceneManager.LoadScene on its own. Set
/// right before loading the battle scene (see LevelButtonView.OnClicked), read once by
/// BattleSequenceController.Start().
/// </summary>
public static class PendingBattle
{
    /// <summary>The campaign level being played, or null for Survival (which doesn't pick one).
    /// Explicitly set on every entry path (campaign level click, Survival button) rather than only
    /// ever assigned — otherwise a stale value from an earlier campaign attempt this same session
    /// would silently carry over into a later Survival run.</summary>
    public static LevelDefinition Level;

    /// <summary>Name of the battle scene to load. Must exactly match the scene file AND be added to
    /// File > Build Settings > Scenes In Build, or SceneManager.LoadScene silently fails. Change this
    /// if your scene is named/located differently.</summary>
    public const string BattleSceneName = "BattleScene";
}
