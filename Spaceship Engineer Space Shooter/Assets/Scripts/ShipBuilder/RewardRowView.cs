using System.Collections.Generic;
using UnityEngine;

public class RewardRowView : MonoBehaviour
{
    public const int MaxRewards = 3;

    [Tooltip("The three reward slots, left to right.")]
    public RewardView[] slots = new RewardView[MaxRewards];

    public void Show(IReadOnlyList<RewardData> rewards, RewardIcons icons)
    {
        int shown = 0;
        if (rewards != null)
        {
            foreach (var reward in rewards)
            {
                if (reward.amount <= 0 || shown >= slots.Length || shown >= MaxRewards) continue;
                if (slots[shown] != null)
                {
                    slots[shown].gameObject.SetActive(true);
                    slots[shown].Show(reward, icons);
                }

                shown++;
            }
        }

        for (int i = shown; i < slots.Length; i++)
            if (slots[i] != null) slots[i].gameObject.SetActive(false);
    }
}
