using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The rewards for one player level: up to three (they are shown side by side).</summary>
[Serializable]
public class LevelRewardSet
{
    public List<RewardData> rewards = new();
}

/// <summary>
/// The rewards for reaching each player level (1 to 100): element 0 is level 1, element 1 is level 2, and so on. Every level can give up to
/// three rewards (credits, coins, experience, research points, random crew cards). The player takes them from the level bar of the rewards
/// panel once the level is reached. Levels without an entry give nothing.
///
/// "Fill With Default Rewards" in this asset's menu (the three dots) writes a starting set: credits on odd levels, experience on even ones,
/// coins every 10th level, research points every 25th - and a random crew card every 5th (two every 20th).
/// </summary>
[CreateAssetMenu(menuName = "ShipBuilder/Player Level Rewards", fileName = "PlayerLevelRewards")]
public class PlayerLevelRewards : ScriptableObject
{
    public List<LevelRewardSet> levels = new();

    // The old format: one reward per level. Moved into Levels when the asset loads.
    [SerializeField, HideInInspector] private List<RewardData> rewards = new();

    public int Count => levels.Count > 0 ? levels.Count : rewards.Count;

    /// <summary>The rewards for a level (1-based); an empty list if there are none.</summary>
    public IReadOnlyList<RewardData> GetRewards(int level)
    {
        if (levels.Count > 0)
            return level >= 1 && level <= levels.Count && levels[level - 1] != null ? levels[level - 1].rewards : Array.Empty<RewardData>();

        // not converted yet: the old one-reward-per-level list
        return level >= 1 && level <= rewards.Count ? new[] { rewards[level - 1] } : Array.Empty<RewardData>();
    }

    private void OnValidate()
    {
        bool changed = false;

        if (levels.Count == 0 && rewards.Count > 0)
        {
            foreach (var old in rewards) levels.Add(new LevelRewardSet { rewards = new List<RewardData> { old } });
            rewards.Clear();
            changed = true;
        }

        foreach (var set in levels)
        {
            if (set != null && set.rewards.Count > RewardRowView.MaxRewards)
            {
                set.rewards.RemoveRange(RewardRowView.MaxRewards, set.rewards.Count - RewardRowView.MaxRewards);
                changed = true;
            }
        }

#if UNITY_EDITOR
        if (changed) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Fill With Default Rewards (100 levels)")]
    public void FillWithDefaults()
    {
        levels.Clear();
        rewards.Clear();
        for (int level = 1; level <= 100; level++) levels.Add(new LevelRewardSet { rewards = DefaultRewards(level) });
    }

    public static List<RewardData> DefaultRewards(int level)
    {
        var list = new List<RewardData>();

        if (level % 25 == 0) list.Add(new RewardData(RewardType.ResearchPoints, 10 + level / 5));
        else if (level % 10 == 0) list.Add(new RewardData(RewardType.Coins, 5 + level / 10 * 2));
        else if (level % 2 == 0) list.Add(new RewardData(RewardType.Experience, 50 + level * 10));
        else list.Add(new RewardData(RewardType.Credits, 100 + level * 20));

        if (level % 5 == 0) list.Add(new RewardData(RewardType.RandomCrewCard, level % 20 == 0 ? 2 : 1));
        return list;
    }
}
