/// <summary>The 4 crew SPECIALTIES — also the 4 fixed slots (places) on the crew screen. A crew member
/// (CrewMemberDefinition) belongs to exactly one specialty and can only be placed in the slot of that
/// same specialty. What a member actually DOES is decided by its own perks (CrewPerk), not by its
/// specialty — the specialty only says where it may stand. Values are explicit so saves survive new
/// roles being added; Navigator keeps the value the old "Helmsman" had.</summary>
public enum CrewRole
{
    Captain = 1,
    Engineer = 2,
    Gunner = 3,
    Navigator = 4
}
