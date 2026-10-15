using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One skill icon with its value underneath — the "SkillItem" (Icon + Coefficient) of a crew card or slot.
/// Shows one CrewPerkDefinition at the member's current level: the icon, and under it what the perk
/// currently gives (x1.25, or 12% for a chance perk). A perk the member's rank hasn't unlocked yet is shown
/// the same way but SEMI-TRANSPARENT (the whole SkillItem fades — Locked Alpha). A perk slot with nothing
/// assigned hides itself.
///
/// SETUP: put on each SkillItem, assign Icon and Value Label (the "Coefficient" text). Three per card/slot,
/// listed in order in the card's/slot's Perk Views — the Nth is the perk of rank N. The fade uses a
/// CanvasGroup, added automatically if the SkillItem has none.
/// </summary>
public class CrewPerkView : MonoBehaviour
{
    public Image icon;
    [Tooltip("The text under the icon — the perk's current value.")]
    public TMP_Text valueLabel;
    [Tooltip("Optional extra object (a padlock...) shown only while the perk is locked.")]
    public GameObject lockedOverlay;
    [Tooltip("Optional perk name text.")]
    public TMP_Text nameLabel;

    [Header("Text")]
    [Tooltip("{0} is the coefficient — \"x{0:0.00}\" gives x1.25.")]
    public string valueFormat = "x{0:0.00}";
    [Tooltip("For chance perks (dodge, zero damage...): {0} is the chance in percent — \"{0:0}%\" gives 12%.")]
    public string chanceFormat = "{0:0.#}%";

    [Header("Locked look")]
    [Range(0f, 1f), Tooltip("Opacity of the whole SkillItem while its rank hasn't been reached.")]
    public float lockedAlpha = 0.35f;

    private CanvasGroup group;

    public void Hide() => gameObject.SetActive(false);

    /// <summary>Shows one perk at `level`. A locked perk is drawn semi-transparent.</summary>
    public void Show(CrewPerkDefinition perk, int level, bool unlocked)
    {
        gameObject.SetActive(true);

        if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = unlocked ? 1f : lockedAlpha;

        if (icon != null) icon.sprite = perk.icon;

        if (valueLabel != null)
            valueLabel.text = perk.type.IsChance()
                ? string.Format(chanceFormat, perk.GetBonus(level) * 100f)
                : string.Format(valueFormat, perk.GetCoefficient(level));

        if (nameLabel != null) nameLabel.text = perk.displayName;
        if (lockedOverlay != null) lockedOverlay.SetActive(!unlocked);
    }

    /// <summary>Fills a row of perk views from a member: view i shows the definition's perk i, unlocked once
    /// the member's rank reaches i + 1. Views without a perk — or all of them, for a null member — hide.</summary>
    public static void Populate(CrewPerkView[] views, CrewMemberDefinition definition, CrewMemberData member)
    {
        if (views == null) return;

        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null) continue;

            var perk = member != null && definition != null && i < definition.perks.Length ? definition.perks[i] : null;
            if (perk == null || perk.type == CrewPerkType.None) views[i].Hide();
            else views[i].Show(perk, member.level, unlocked: i < member.rank);
        }
    }
}
