using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BlockInfoCard : MonoBehaviour
{
    [Header("Slide")]
    [Tooltip("The sliding panel the card sits on (a SlideOutPanel, e.g. BlockStatsPanel). It slides out when a block is selected " +
             "and back in when none is. Found on the card or its parents if left empty. Set that panel's Show Distance to the " +
             "distance it has to travel from its hidden position (e.g. -115 for a panel hidden at x = 115).")]
    public SlideOutPanel slidePanel;

    [Header("Header")]
    public TMP_Text nameLabel;
    [Tooltip("The block's picture. It turns together with the block when you rotate it.")]
    public Image spriteImage;

    [Header("Rows")]
    [Tooltip("The object with the Vertical Layout Group; the rows are created inside it.")]
    public Transform rowsParent;
    [Tooltip("A prefab with a BlockInfoRow (icon + number). One copy is made per parameter.")]
    public BlockInfoRow rowPrefab;

    [Header("Icons")]
    public Sprite healthIcon;
    [Tooltip("Armor blocks use this icon for their health.")]
    public Sprite armorHealthIcon;
    public Sprite damageIcon;
    public Sprite fireRateIcon;
    [Tooltip("Energy a weapon spends on one shot.")]
    public Sprite energyPerShotIcon;
    [Tooltip("Energy a shield spends on one absorbed hit.")]
    public Sprite energyPerHitIcon;
    [Tooltip("Energy spent every second (engines, repair blocks).")]
    public Sprite energyPerSecondIcon;
    public Sprite energyMadeIcon;
    public Sprite enginePowerIcon;
    public Sprite repairRateIcon;

    [Header("Numbers")]
    [Tooltip("How numbers are written: \"0.##\" gives 5, 2.5, 0.25.")]
    public string numberFormat = "0.##";
    [Tooltip("{0} = seconds between shots, e.g. \"{0:0.##} с\".")]
    public string fireRateFormat = "{0:0.##} с";

    private void Awake()
    {
        if (slidePanel == null) slidePanel = GetComponentInParent<SlideOutPanel>();
    }

    public void Hide()
    {
        if (slidePanel != null && slidePanel.isActiveAndEnabled)
        {
            slidePanel.Hide();
            return;
        }

        if (nameLabel != null) nameLabel.text = string.Empty;
        if (spriteImage != null)
        {
            spriteImage.sprite = null;
            spriteImage.enabled = false;
        }
        ClearRows();
    }

    public void HideImmediate()
    {
        if (slidePanel == null) slidePanel = GetComponentInParent<SlideOutPanel>();
        if (slidePanel != null) slidePanel.HideImmediate();

        if (nameLabel != null) nameLabel.text = string.Empty;
        if (spriteImage != null)
        {
            spriteImage.sprite = null;
            spriteImage.enabled = false;
        }
        ClearRows();
    }

    public void Show(BlockDefinition block)
    {
        if (block == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        if (slidePanel != null && slidePanel.isActiveAndEnabled) slidePanel.Show();

        if (nameLabel != null) nameLabel.text = block.displayName;
        if (spriteImage != null)
        {
            spriteImage.sprite = block.icon;
            spriteImage.enabled = block.icon != null;
            spriteImage.transform.localRotation = Quaternion.identity;
        }

        ClearRows();

        var module = block.prefab != null ? block.prefab.GetComponent<ShipModule>() : null;
        if (module == null)
        {
            Debug.LogWarning($"Block card: block '{block.id}' has no prefab (or the prefab is missing / has no ShipModule) — only its name and picture can be shown.", block);
            return;
        }

        bool armor = block.category == BlockCategory.Armor || module.type == ModuleType.Armor;
        AddRow(armor && armorHealthIcon != null ? armorHealthIcon : healthIcon, Format(module.maxHP));

        if (module is WeaponModule weapon)
        {
            AddRow(damageIcon, Format(weapon.NominalDamagePerShot));
            AddRow(fireRateIcon, string.Format(fireRateFormat, weapon.cooldownSeconds));
        }

        AddEnergyRows(module);

        if (module is EngineModule engine) AddRow(enginePowerIcon, Format(engine.thrustPower));
        if (module is RepairModule repair) AddRow(repairRateIcon, Format(repair.repairPerSecond));
    }

    public void SetSpriteRotation(float degrees)
    {
        if (spriteImage != null) spriteImage.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    private void AddEnergyRows(ShipModule module)
    {
        switch (module)
        {
            case GeneratorModule generator:
                AddRow(energyMadeIcon, Format(Mathf.Abs(generator.powerOutput)));
                break;
            case WeaponModule:
                AddEnergyUse(energyPerShotIcon, Mathf.Abs(module.energyDelta));
                break;
            case ShieldModule:
                AddEnergyUse(energyPerHitIcon, Mathf.Abs(module.energyDelta));
                break;
            case RepairModule repair:
                AddEnergyUse(energyPerSecondIcon, Mathf.Abs(repair.repairPerSecond * repair.energyPerHp));
                break;
            default:
                if (module.energyDelta < 0f) AddEnergyUse(energyPerSecondIcon, -module.energyDelta);
                else if (module.energyDelta > 0f) AddRow(energyMadeIcon, Format(module.energyDelta));
                break;
        }
    }

    private void AddEnergyUse(Sprite icon, float amount)
    {
        if (amount > 0f) AddRow(icon, Format(amount));
    }

    private void AddRow(Sprite icon, string value)
    {
        if (rowsParent == null || rowPrefab == null) return;

        var row = Instantiate(rowPrefab, rowsParent);
        row.Set(icon, value);
    }

    private void ClearRows()
    {
        if (rowsParent == null) return;

        for (int i = rowsParent.childCount - 1; i >= 0; i--)
        {
            var child = rowsParent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private string Format(float value) => value.ToString(numberFormat, System.Globalization.CultureInfo.InvariantCulture);
}
