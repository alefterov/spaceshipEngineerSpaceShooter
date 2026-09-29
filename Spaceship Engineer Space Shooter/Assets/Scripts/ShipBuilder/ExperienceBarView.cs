using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The player's account-level progress bar shown on the battle result panel: a horizontal fill between
/// two level numbers — Current Level Label on the left (the level the bar's empty end represents) and
/// Next Level Label on the right (Current + 1) — filling to show progress toward the next level.
///
/// Animates through however many GameDataManager.ExperienceGainStep entries a gain produced (usually
/// one; more than one if the gain leveled the player up more than once in the same battle) — each step
/// fills from its own startXp to endXp over Seconds Per Step, then the bar resets to empty and both
/// labels increment by one before the next step plays, the same "roll over" most RPGs use for a
/// multi-level gain.
///
/// PREFAB SETUP: Fill Image needs Image Type = Filled, Fill Method = Horizontal.
/// </summary>
public class ExperienceBarView : MonoBehaviour
{
    public Image fillImage;
    public TMP_Text currentLevelLabel;
    public TMP_Text nextLevelLabel;
    [Tooltip("Seconds each step (one level's worth of the bar) takes to fill.")]
    public float secondsPerStep = 0.8f;

    private Coroutine routine;

    /// <summary>Sets the bar to a fixed position with no animation — e.g. the player's standing before
    /// any gain plays, or when there's nothing to animate.</summary>
    public void SetImmediate(int level, int currentXp, int requiredXp)
    {
        SetLabels(level);
        SetFill(requiredXp > 0 ? (float)currentXp / requiredXp : 0f);
    }

    /// <summary>Plays the level-up animation for a gain — see GameDataManager.AddExperience.</summary>
    public void PlaySteps(List<GameDataManager.ExperienceGainStep> steps)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(PlayStepsRoutine(steps));
    }

    private IEnumerator PlayStepsRoutine(List<GameDataManager.ExperienceGainStep> steps)
    {
        foreach (var step in steps)
        {
            SetLabels(step.level);
            float from = step.xpRequired > 0 ? (float)step.startXp / step.xpRequired : 0f;
            float to = step.xpRequired > 0 ? (float)step.endXp / step.xpRequired : 0f;
            SetFill(from);

            for (float t = 0f; t < secondsPerStep; t += Time.deltaTime)
            {
                SetFill(Mathf.Lerp(from, to, t / secondsPerStep));
                yield return null;
            }
            SetFill(to);
        }

        routine = null;
    }

    private void SetLabels(int level)
    {
        if (currentLevelLabel != null) currentLevelLabel.text = level.ToString();
        if (nextLevelLabel != null) nextLevelLabel.text = (level + 1).ToString();
    }

    private void SetFill(float value)
    {
        if (fillImage != null) fillImage.fillAmount = Mathf.Clamp01(value);
    }
}
