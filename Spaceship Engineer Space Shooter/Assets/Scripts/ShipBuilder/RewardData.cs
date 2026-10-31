using System;
using UnityEngine;

/// <summary>What a reward gives.</summary>
public enum RewardType
{
    Credits = 0,
    Coins = 1,
    Experience = 2,
    ResearchPoints = 3,
    /// <summary>Random crew member cards (rank 1, level 1); the amount is how many cards.</summary>
    RandomCrewCard = 4,
}

/// <summary>One reward: a kind and an amount. Used by the player-level rewards and by the tasks.</summary>
[Serializable]
public struct RewardData
{
    public RewardType type;
    [Min(0)] public int amount;
    [Tooltip("Random Crew Card only: the rank of the cards given (1-3). 0 counts as 1.")]
    [Range(0, CrewMemberDefinition.MaxRank)] public int crewRank;

    /// <summary>The rank of the crew cards this reward gives: Crew Rank, at least 1.</summary>
    public int CrewRank => Mathf.Clamp(crewRank, 1, CrewMemberDefinition.MaxRank);

    public RewardData(RewardType type, int amount, int crewRank = 1)
    {
        this.type = type;
        this.amount = amount;
        this.crewRank = crewRank;
    }
}

/// <summary>The picture for each kind of reward (credits, coins, experience, research points, crew card) — the rewards panel hands it to every
/// reward block it shows.</summary>
[Serializable]
public class RewardIcons
{
    public Sprite credits;
    public Sprite coins;
    public Sprite experience;
    public Sprite researchPoints;
    [Tooltip("The random crew card.")]
    public Sprite crewCard;

    public Sprite Get(RewardType type) => type switch
    {
        RewardType.Credits => credits,
        RewardType.Coins => coins,
        RewardType.Experience => experience,
        RewardType.ResearchPoints => researchPoints,
        RewardType.RandomCrewCard => crewCard,
        _ => null,
    };
}
