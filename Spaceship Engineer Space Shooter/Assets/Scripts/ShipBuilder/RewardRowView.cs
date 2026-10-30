using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A row of up to three rewards (RewardView slots) on a level button or a task. The slots the reward list does not need are switched off, so
/// the visible ones sit side by side (give the row a Horizontal Layout Group to keep them centred).
/// </summary>
public class RewardRowView : MonoBehaviour
{
    /// <summary>The most rewards one block can show.</summary>
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
