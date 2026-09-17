/// <summary>
/// Hands off which level was picked across the scene load into the battle scene — a plain static
/// field, since a MonoBehaviour's own state doesn't survive SceneManager.LoadScene on its own. Set
/// right before loading the battle scene (see LevelButtonView.OnClicked), read once by
/// BattleSequenceController.Start().
/// </summary>
public static class PendingBattle
{
    public static LevelDefinition Level;
}
