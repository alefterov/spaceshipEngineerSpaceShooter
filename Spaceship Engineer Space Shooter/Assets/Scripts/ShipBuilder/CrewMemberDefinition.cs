using UnityEngine;

/// <summary>
/// One KIND of crew member (a character) — one asset per character. Holds everything that never changes
/// per copy: which specialty/slot it fits, its portrait for each of the 3 ranks, its 4 perks and what a
/// level-up costs. The player's actual copies (with their own rank and level) are CrewMemberData in the
/// save file, pointing back here by Id.
///
/// Two copies are "the same kind" for merging (rank-up) only if they share this exact asset.
/// </summary>
[CreateAssetMenu(menuName = "ShipBuilder/Crew Member Definition", fileName = "NewCrewMember")]
public class CrewMemberDefinition : ScriptableObject
{
    public const int MaxRank = 3;
    public const int PerkCount = 4;

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
    [Tooltip("Exactly 4. Each perk has its own Unlock Rank (default 1, 1, 2, 3: two perks at rank 1, one " +
             "more at rank 2 and one more at rank 3). Leave a perk's Type at None to leave that slot unused.")]
    public CrewPerk[] perks = DefaultPerks();

    private static CrewPerk[] DefaultPerks()
    {
        var result = new CrewPerk[PerkCount];
        for (int i = 0; i < result.Length; i++)
            result[i] = new CrewPerk { unlockRank = Mathf.Clamp(i, 1, MaxRank) };
        return result;
    }

    // Keeps an asset created back when members had 3 perks usable: grows the array to 4 (new entries get
    // the default unlock rank) and clamps every unlock rank into 1..MaxRank.
    private void OnValidate()
    {
        if (perks == null) perks = DefaultPerks();
        if (perks.Length != PerkCount)
        {
            int oldLength = perks.Length;
            System.Array.Resize(ref perks, PerkCount);
            for (int i = oldLength; i < PerkCount; i++)
                perks[i] = new CrewPerk { unlockRank = Mathf.Clamp(i, 1, MaxRank) };
        }

        foreach (var perk in perks)
            if (perk != null) perk.unlockRank = Mathf.Clamp(perk.unlockRank, 1, MaxRank);
    }

    [Header("Level-up cost")]
    [Tooltip("Starting price. The price of the next level is Level Up Base Cost x current level + current " +
             "level x GameDataManager.Crew Level Cost Extra Per Level.")]
    public int levelUpBaseCost = 100;

    /// <summary>The portrait for this rank (1-3). Falls back to the nearest lower rank that has a
    /// sprite, so an unfinished asset with only rank 1 drawn still shows something.</summary>
    public Sprite GetRankSprite(int rank)
    {
        if (rankSprites == null || rankSprites.Length == 0) return null;

        for (int i = Mathf.Clamp(rank - 1, 0, rankSprites.Length - 1); i >= 0; i--)
            if (rankSprites[i] != null) return rankSprites[i];
        return null;
    }
}
