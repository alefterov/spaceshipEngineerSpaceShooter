using System;
using UnityEngine;

public enum RewardType
{
    Credits = 0,
    Coins = 1,
    Experience = 2,
    ResearchPoints = 3,
    RandomCrewCard = 4,
}

[Serializable]
public struct RewardData
{
    public RewardType type;
    [Min(0)] public int amount;
    [Tooltip("Random Crew Card only: the rank of the cards given (1-3). 0 counts as 1.")]
    [Range(0, CrewMemberDefinition.MaxRank)] public int crewRank;

    public int CrewRank => Mathf.Clamp(crewRank, 1, CrewMemberDefinition.MaxRank);

    public RewardData(RewardType type, int amount, int crewRank = 1)
    {
        this.type = type;
        this.amount = amount;
        this.crewRank = crewRank;
    }
}

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
