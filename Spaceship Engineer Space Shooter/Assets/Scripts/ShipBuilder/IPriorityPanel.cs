/// <summary>A tab of the priorities window (ShipPrioritiesWindow): something that can lay its rows out to match the game's current state.</summary>
public interface IPriorityPanel
{
    /// <summary>Called whenever the tab is about to be shown: set the rows to the real, current order.</summary>
    void RefreshPanel();
}
