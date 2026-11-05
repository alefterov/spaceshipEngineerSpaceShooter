using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ShipBuilder/Daily Rewards", fileName = "DailyRewards")]
public class DailyRewards : ScriptableObject
{
    public const int DefaultDays = 30;

    [Tooltip("Every fifth day is a special one (its own look on the button, a bigger gift).")]
    [Min(1)] public int specialEvery = 5;
    [Tooltip("The gifts of each day, day 1 first. Up to three rewards a day.")]
    public List<LevelRewardSet> days = new();

    public int Count => days.Count;

    public bool IsSpecialDay(int day) => specialEvery > 0 && day % specialEvery == 0;

    public IReadOnlyList<RewardData> GetRewards(int day)
        => day >= 1 && day <= days.Count && days[day - 1] != null ? days[day - 1].rewards : Array.Empty<RewardData>();

    private void OnValidate()
    {
        bool changed = false;
        foreach (var set in days)
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

    [ContextMenu("Fill With Default Rewards (30 days)")]
    public void FillWithDefaults()
    {
        days.Clear();
        for (int day = 1; day <= DefaultDays; day++) days.Add(new LevelRewardSet { rewards = DefaultRewards(day) });
    }

    public static List<RewardData> DefaultRewards(int day)
    {
        var list = new List<RewardData>();
        int week = (day - 1) / 5;

        if (day % 5 == 0)
        {
            switch (day / 5)
            {
                case 1: list.Add(new RewardData(RewardType.Coins, 10)); list.Add(new RewardData(RewardType.RandomCrewCard, 1)); break;
                case 2: list.Add(new RewardData(RewardType.ResearchPoints, 15)); list.Add(new RewardData(RewardType.RandomCrewCard, 1)); break;
                case 3: list.Add(new RewardData(RewardType.Coins, 20)); list.Add(new RewardData(RewardType.RandomCrewCard, 2)); break;
                case 4: list.Add(new RewardData(RewardType.ResearchPoints, 25)); list.Add(new RewardData(RewardType.RandomCrewCard, 2)); break;
                case 5: list.Add(new RewardData(RewardType.Coins, 30)); list.Add(new RewardData(RewardType.RandomCrewCard, 1, 2)); break;
                default:
                    list.Add(new RewardData(RewardType.Coins, 50));
                    list.Add(new RewardData(RewardType.ResearchPoints, 30));
                    list.Add(new RewardData(RewardType.RandomCrewCard, 2, 2));
                    break;
            }

            return list;
        }

        if (day % 5 == 3) list.Add(new RewardData(RewardType.ResearchPoints, 5 + week * 2));
        else list.Add(new RewardData(RewardType.Credits, 1000 + day * 100));

        if (day % 5 == 4) list.Add(new RewardData(RewardType.Experience, 100 + day * 10));
        return list;
    }
}
