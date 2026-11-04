using System.Collections.Generic;
using UnityEngine;

public enum TaskKind
{
    DestroyBlocks = 0,
    CompleteCampaignLevel = 1,
    SurvivalWave = 2,
}

[CreateAssetMenu(menuName = "ShipBuilder/Task", fileName = "NewTask")]
public class TaskDefinition : ScriptableObject
{
    [Tooltip("Unique name the progress is saved under. Filled with the asset's name if left empty - do not change it later, or the saved progress is lost.")]
    public string id;
    [Tooltip("The task text. Empty = made up from the fields below.")]
    public string title;
    public TaskKind kind = TaskKind.DestroyBlocks;

    [Header("Destroy Blocks")]
    [Tooltip("On: every enemy block counts, whatever its type.")]
    public bool anyBlockType = true;
    [Tooltip("Which blocks count when Any Block Type is off (Weapon means every kind of weapon).")]
    public ModuleType blockType = ModuleType.Weapon;
    [Min(1)] public int targetCount = 50;

    [Header("Complete Campaign Level")]
    public LevelDefinition level;

    [Header("Survival Wave")]
    [Min(1)] public int targetWave = 10;

    [Header("Rewards (up to 3)")]
    [Tooltip("What the player gets for the task: up to three rewards, side by side. A random crew card is one of the reward kinds.")]
    public List<RewardData> rewards = new();

    [SerializeField, HideInInspector] private RewardData reward = new(RewardType.Credits, 100);

    public IReadOnlyList<RewardData> Rewards => rewards.Count == 0 && reward.amount > 0 ? new[] { reward } : rewards;

    public int TargetValue => kind switch
    {
        TaskKind.DestroyBlocks => Mathf.Max(1, targetCount),
        TaskKind.CompleteCampaignLevel => 1,
        TaskKind.SurvivalWave => Mathf.Max(1, targetWave),
        _ => 1,
    };

    public string Title => string.IsNullOrWhiteSpace(title) ? AutoTitle() : title;

    private string AutoTitle()
    {
        switch (kind)
        {
            case TaskKind.DestroyBlocks:
                return anyBlockType
                    ? $"Destroy {targetCount} enemy blocks"
                    : $"Destroy {targetCount} enemy {BlockTypeName(blockType)} blocks";
            case TaskKind.CompleteCampaignLevel:
                if (level == null) return "Complete a campaign level";
                int campaign = CampaignNumber(level);
                return campaign > 0 ? $"Complete level {level.displayNumber} of campaign {campaign}" : $"Complete campaign level {level.displayNumber}";
            default:
                return $"Reach wave {targetWave} in survival mode";
        }
    }

    private static int CampaignNumber(LevelDefinition level)
    {
        var match = System.Text.RegularExpressions.Regex.Match(level.id ?? "", @"campaign_(\d+)_");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static string BlockTypeName(ModuleType type) => type switch
    {
        ModuleType.Armor => "armor",
        ModuleType.Weapon => "weapon",
        ModuleType.Engine => "engine",
        ModuleType.Shield => "shield",
        ModuleType.Generator => "generator",
        ModuleType.Cockpit => "cockpit",
        ModuleType.Repair => "repair",
        _ => "",
    };

    private void OnValidate()
    {
        bool changed = false;

        if (string.IsNullOrEmpty(id)) id = name;

        if (rewards.Count == 0 && reward.amount > 0)
        {
            rewards.Add(reward);
            changed = true;
        }

        if (reward.amount != 0)
        {
            reward = default;
            changed = true;
        }

        if (rewards.Count > RewardRowView.MaxRewards)
        {
            rewards.RemoveRange(RewardRowView.MaxRewards, rewards.Count - RewardRowView.MaxRewards);
            changed = true;
        }

#if UNITY_EDITOR
        if (changed) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
