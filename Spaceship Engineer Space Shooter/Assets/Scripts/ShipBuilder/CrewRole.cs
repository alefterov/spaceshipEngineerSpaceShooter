/// <summary>The 4 ship's-crew roles, each upgraded separately with credits (see GameDataManager.
/// TryUpgradeCrew) and each boosting a different pair of the player's ship stats. All 4 are treated
/// identically in the UI — just an upgrade, no naming or avatar-picking flow for any of them:
///  - Captain: experience gain from battle (see GameDataManager.GetCaptainExperienceMultiplier).
///  - Engineer: shield power and max HP (ShieldModule.capacity, ShipModule.maxHP) — and the repair rate
///    of Repair blocks (RepairModule).
///  - Gunner: turret aim speed and fire rate (WeaponModule).
///  - Helmsman: movement speed and engine energy efficiency (ShipMovement).
/// Values are explicit so this survives new roles being added without renumbering old saves.</summary>
public enum CrewRole
{
    Captain = 1,
    Engineer = 2,
    Gunner = 3,
    Helmsman = 4
}
