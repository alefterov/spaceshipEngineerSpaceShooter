using UnityEngine;

[CreateAssetMenu(menuName = "ShipBuilder/Crew Member Definition", fileName = "NewCrewMember")]
public class CrewMemberDefinition : ScriptableObject
{
    public const int MaxRank = 3;

    [Header("Identity")]
    [Tooltip("Unique — saved copies point back here by this id, so don't change it after release.")]
    public string id = "crew_captain_1";
    public string displayName = "Captain";
    [Tooltip("Which of the 4 slots this member may stand in.")]
    public CrewRole role = CrewRole.Captain;

    [Header("Portrait per rank")]
    [Tooltip("Index 0 = rank 1, 1 = rank 2, 2 = rank 3. The pictures differ only by background, one per " +
             "rank — it swaps automatically when the member is merged up.")]
    public Sprite[] rankSprites = new Sprite[MaxRank];

    [Header("Perks")]
    [Tooltip("Exactly 3 perk assets, in order: the first is available at rank 1, the second at rank 2, the " +
             "third at rank 3. Locked ones show semi-transparent on the card. Leave an entry empty for no perk.")]
    public CrewPerkDefinition[] perks = new CrewPerkDefinition[MaxRank];

    private void OnValidate()
    {
        if (perks == null) perks = new CrewPerkDefinition[MaxRank];
        else if (perks.Length != MaxRank) System.Array.Resize(ref perks, MaxRank);
    }

    [Header("Level-up cost")]
    [Tooltip("Starting price. The price of the next level is (Level Up Base Cost x current level + current " +
             "level x GameDataManager.Crew Level Cost Extra Per Level) x the member's rank.")]
    public int levelUpBaseCost = 200;

    public Sprite GetRankSprite(int rank)
    {
        if (rankSprites == null || rankSprites.Length == 0) return null;

        for (int i = Mathf.Clamp(rank - 1, 0, rankSprites.Length - 1); i >= 0; i--)
            if (rankSprites[i] != null) return rankSprites[i];
        return null;
    }
}
