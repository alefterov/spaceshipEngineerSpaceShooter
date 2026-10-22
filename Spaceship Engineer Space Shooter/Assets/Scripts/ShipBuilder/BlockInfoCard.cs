using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "block card": a separate UI panel in the build screen describing the block selected in the palette. At the top
/// the block's NAME and its SPRITE; below them, in a vertical layout group, one ROW per parameter the block has — each
/// row is a prefab (BlockInfoRow) with an ICON and a number, so a parameter is told by its picture, not by a word.
///
/// Parameters (a row appears only when the block has it):
///   health        — every block; armor uses its own icon
///   damage        — weapons: damage of one shot (a laser: the whole beam)
///   fire rate     — weapons: seconds between shots
///   energy use    — blocks that spend energy; the ICON says when: per shot (weapons), per hit (shields), per second
///                   (engines, repair blocks)
///   energy made   — generators, per second
///   engine power  — engines: thrust
///   repair rate   — repair blocks: health restored per second
/// Numbers are read from the block's prefab, so nothing is entered twice.
///
/// SETUP: assign Name Label, Sprite Image, Rows Parent (an object with a Vertical Layout Group) and Row Prefab (a prefab
/// with BlockInfoRow). Assign the icons below for the parameters you want shown — a parameter with no icon assigned is
/// still shown, just without a picture. BuildPaletteUI shows the card on selection (its Info Card field).
/// </summary>
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

    /// <summary>No block selected: the panel slides away (keeping what it shows while it goes). With no sliding panel
    /// the card stays where it is and only its contents are emptied.</summary>
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

    /// <summary>Hides the panel right away, no sliding, and empties the card — used when the build screen is closed.</summary>
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

    /// <summary>Shows the card for this block.</summary>
    public void Show(BlockDefinition block)
    {
        if (block == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true); // normally already on — it is never switched off
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

    /// <summary>Turns the picture with the block (the palette calls this when it is rotated).</summary>
    public void SetSpriteRotation(float degrees)
    {
        if (spriteImage != null) spriteImage.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    // ---------- Rows ----------

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
            default: // engines and anything else: what the block says, per second
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
            child.SetActive(false); // Destroy is deferred — the layout must stop counting the old row right now
            Destroy(child);
        }
    }

    private string Format(float value) => value.ToString(numberFormat, System.Globalization.CultureInfo.InvariantCulture);
}
