using UnityEngine;

/// <summary>
/// Spawns one ManualWeaponButton per manually-fired weapon type the player actually built — same
/// pattern as BuildPaletteUI does for block buttons. A weapon type the ship doesn't carry simply
/// produces no button, so "the button only exists if that weapon is on the ship" falls out for free.
///
/// SETUP: put this on the battle HUD's button container and enable that HUD only in battle mode —
/// this bar knows nothing about combat state, it just mirrors whatever the ship carries.
/// </summary>
public class ManualWeaponBar : MonoBehaviour
{
    [Header("References")]
    public ManualWeaponController weapons;
    [Tooltip("Used to look up each weapon's icon by its BlockDefinition id.")]
    public BlockDatabase database;

    [Header("UI wiring")]
    [Tooltip("Parent the buttons are spawned under — typically this object's own layout group.")]
    public Transform buttonContainer;
    [Tooltip("Prefab with a Button + ManualWeaponButton component.")]
    public GameObject buttonPrefab;

    private void OnEnable()
    {
        weapons.OnGroupsChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        weapons.OnGroupsChanged -= Rebuild;
    }

    private void Rebuild()
    {
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        foreach (var group in weapons.Groups)
        {
            var buttonObj = Instantiate(buttonPrefab, buttonContainer);
            buttonObj.name = $"FireBtn_{group.WeaponId}";

            var view = buttonObj.GetComponent<ManualWeaponButton>();
            if (view == null)
            {
                Debug.LogError($"Button prefab is missing a ManualWeaponButton component ('{group.WeaponId}').");
                continue;
            }

            var definition = database != null ? database.GetById(group.WeaponId) : null;
            view.Bind(group, definition != null ? definition.icon : null);
        }
    }
}
