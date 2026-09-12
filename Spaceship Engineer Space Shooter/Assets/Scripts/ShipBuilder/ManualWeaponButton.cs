using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One manual-fire button, bound to a ManualWeaponGroup. Fires the whole group on click and shows the
/// shared cooldown as a radial ring around itself.
///
/// PREFAB SETUP: the cooldown ring Image must have Image Type = Filled, Fill Method = Radial 360 —
/// this only drives its fillAmount.
/// </summary>
public class ManualWeaponButton : MonoBehaviour
{
    [Header("UI (assign whichever exist on this prefab)")]
    public Button button;
    [Tooltip("Weapon icon, taken from the BlockDefinition.")]
    public Image icon;
    [Tooltip("Radial cooldown ring — needs Image Type = Filled, Fill Method = Radial 360.")]
    public Image cooldownRing;
    [Tooltip("Optional — shows how many weapons of this type share the button (e.g. \"x2\"). Hidden when there's only one.")]
    public TMP_Text countLabel;

    private ManualWeaponGroup group;

    /// <summary>Called by ManualWeaponBar right after instantiating this button.</summary>
    public void Bind(ManualWeaponGroup weaponGroup, Sprite iconSprite)
    {
        group = weaponGroup;

        if (icon != null && iconSprite != null) icon.sprite = iconSprite;

        if (countLabel != null)
        {
            countLabel.text = $"x{group.Count}";
            countLabel.gameObject.SetActive(group.Count > 1);
        }
    }

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(OnFireClicked);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnFireClicked);
    }

    private void Update()
    {
        if (group == null) return;

        // Polled rather than event-driven: a cooldown is a continuously changing value, so there's no
        // discrete moment to fire an event on.
        if (cooldownRing != null) cooldownRing.fillAmount = group.CooldownRemaining01;
        if (button != null) button.interactable = group.IsReady;
    }

    private void OnFireClicked() => group?.Fire();
}
