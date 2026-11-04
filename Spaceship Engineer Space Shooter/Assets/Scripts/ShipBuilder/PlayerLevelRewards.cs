using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LevelRewardSet
{
    public List<RewardData> rewards = new();
}

[CreateAssetMenu(menuName = "ShipBuilder/Player Level Rewards", fileName = "PlayerLevelRewards")]
public class PlayerLevelRewards : ScriptableObject
{
    public List<LevelRewardSet> levels = new();

    [SerializeField, HideInInspector] private List<RewardData> rewards = new();

    public int Count => levels.Count > 0 ? levels.Count : rewards.Count;

    public IReadOnlyList<RewardData> GetRewards(int level)
    {
        if (levels.Count > 0)
            return level >= 1 && level <= levels.Count && levels[level - 1] != null ? levels[level - 1].rewards : Array.Empty<RewardData>();

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
        else list.Add(new RewardData(RewardType.Credits, (100 + level * 20) * 10));

        if (level % 5 == 0) list.Add(new RewardData(RewardType.RandomCrewCard, level % 20 == 0 ? 2 : 1));
        return list;
    }
}
