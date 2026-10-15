using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One skill icon with its coefficient underneath — the "SkillItem" (Icon + Coefficient) of a crew card or
/// slot. Shows one CrewPerk at the member's current level: the icon, and under it the coefficient the perk
/// currently gives (x1.25...). A perk the member's rank hasn't unlocked yet shows dimmed with the rank it
/// needs instead of a coefficient. A perk slot with nothing configured hides itself.
///
/// SETUP: put on each SkillItem, assign Icon and Value Label (the "Coefficient" text). Four per card/slot
/// (rank 3 unlocks all four; each perk has its own Unlock Rank); list them in order in the card's/slot's Perk Views.
/// </summary>
public class CrewPerkView : MonoBehaviour
{
    public Image icon;
    [Tooltip("The text under the icon — the coefficient, or the rank needed while the perk is locked.")]
    public TMP_Text valueLabel;
    [Tooltip("Optional extra object (a padlock, a dark veil) shown only while the perk is locked.")]
    public GameObject lockedOverlay;
    [Tooltip("Optional perk name text.")]
    public TMP_Text nameLabel;

    [Header("Text")]
    [Tooltip("{0} is the coefficient — \"x{0:0.00}\" gives x1.25.")]
    public string valueFormat = "x{0:0.00}";
    [Tooltip("For chance perks (dodge, zero damage...): {0} is the chance in percent — \"{0:0}%\" gives 12%.")]
    public string chanceFormat = "{0:0.#}%";
    [Tooltip("{0} is the rank that unlocks the perk.")]
    public string lockedFormat = "Ранг {0}";
    public Color lockedIconColor = new(1f, 1f, 1f, 0.35f);

    public void Hide() => gameObject.SetActive(false);

    /// <summary>Shows one perk. unlocked = the member's rank has reached requiredRank.</summary>
    public void Show(CrewPerk perk, int level, bool unlocked, int requiredRank)
    {
        gameObject.SetActive(true);

        if (icon != null)
        {
            icon.sprite = perk.icon;
            icon.color = unlocked ? Color.white : lockedIconColor;
        }

        if (valueLabel != null)
            valueLabel.text = !unlocked ? string.Format(lockedFormat, requiredRank)
                : perk.type.IsChance() ? string.Format(chanceFormat, perk.GetBonus(level) * 100f)
                : string.Format(valueFormat, perk.GetCoefficient(level));

        if (nameLabel != null) nameLabel.text = perk.displayName;
        if (lockedOverlay != null) lockedOverlay.SetActive(!unlocked);
    }

    /// <summary>Fills a row of perk views from a member: a perk is unlocked once the rank reaches its Unlock Rank. Views
    /// beyond the perks the definition actually has — or all of them, for a null member — hide.</summary>
    public static void Populate(CrewPerkView[] views, CrewMemberDefinition definition, CrewMemberData member)
    {
        if (views == null) return;

        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null) continue;

            var perk = member != null && definition != null && i < definition.perks.Length ? definition.perks[i] : null;
            if (perk == null || perk.type == CrewPerkType.None) views[i].Hide();
            else views[i].Show(perk, member.level, unlocked: perk.IsUnlocked(member.rank), requiredRank: perk.unlockRank);
        }
    }
}
