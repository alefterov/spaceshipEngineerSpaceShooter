using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        if (cooldownRing != null) cooldownRing.fillAmount = group.CooldownRemaining01;
        if (button != null) button.interactable = group.IsReady;
    }

    private void OnFireClicked() => group?.Fire();
}
