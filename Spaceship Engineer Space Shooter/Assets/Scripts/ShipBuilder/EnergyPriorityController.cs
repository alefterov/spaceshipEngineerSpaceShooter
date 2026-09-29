using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The in-battle energy-priority window: opening it PAUSES the battle (Time.timeScale = 0) — a
/// deliberate exception to how the rest of combat works (see BattleOutcomeController's own doc comment
/// on why the RESULT screen deliberately does NOT pause; this is a different, player-initiated pause for
/// a tactical decision, not the automatic post-battle state). While open, drag the 4 PriorityDragItem
/// rows into whatever order you want (see that class) — each drop reorders the player's ShipEnergySystem
/// live via SetOrder, so the effect is visible immediately even before you close the window and unpause.
///
/// SETUP: put on the panel's root (starts inactive). Assign Player Energy (the player ship's
/// ShipEnergySystem) and Priority Items Parent (the layout-group container holding the 4 PriorityDragItem
/// rows). Wire whatever "open" button you have to Open()/Toggle(), and the panel's own close button to
/// Close().
/// </summary>
public class EnergyPriorityController : MonoBehaviour
{
    [Tooltip("The player ship's ShipEnergySystem — whose PriorityOrder this window edits.")]
    public ShipEnergySystem playerEnergy;
    [Tooltip("The layout-group container holding the 4 PriorityDragItem rows, top = highest priority.")]
    public Transform priorityItemsParent;

    private float previousTimeScale = 1f;

    public bool IsOpen { get; private set; }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        gameObject.SetActive(true);
        RefreshItemsFromEnergySystem();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        Time.timeScale = previousTimeScale;
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    /// <summary>Lays the 4 rows out to match the energy system's CURRENT order — call when opening, so
    /// the window always reflects reality rather than whatever order it was left in last time.</summary>
    private void RefreshItemsFromEnergySystem()
    {
        if (playerEnergy == null || priorityItemsParent == null) return;

        var order = playerEnergy.PriorityOrder;
        var itemsByGroup = new Dictionary<ShipEnergySystem.EnergyPriorityGroup, PriorityDragItem>();

        for (int i = 0; i < priorityItemsParent.childCount; i++)
            if (priorityItemsParent.GetChild(i).TryGetComponent<PriorityDragItem>(out var item))
                itemsByGroup[item.system] = item;

        for (int i = 0; i < order.Count; i++)
            if (itemsByGroup.TryGetValue(order[i], out var item))
                item.transform.SetSiblingIndex(i);
    }

    /// <summary>Called by a PriorityDragItem once it's dropped into a new slot — reads the current
    /// sibling order of Priority Items Parent and pushes it straight to the player's ShipEnergySystem.</summary>
    public void SetOrderFromSiblingOrder()
    {
        if (playerEnergy == null || priorityItemsParent == null) return;

        var order = new List<ShipEnergySystem.EnergyPriorityGroup>();
        for (int i = 0; i < priorityItemsParent.childCount; i++)
            if (priorityItemsParent.GetChild(i).TryGetComponent<PriorityDragItem>(out var item))
                order.Add(item.system);

        playerEnergy.SetPriorityOrder(order);
    }
}
