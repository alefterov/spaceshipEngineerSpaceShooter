using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ExperienceBarView : MonoBehaviour
{
    public Image fillImage;
    public TMP_Text currentLevelLabel;
    public TMP_Text nextLevelLabel;
    [Tooltip("Seconds each step (one level's worth of the bar) takes to fill.")]
    public float secondsPerStep = 0.8f;

    private Coroutine routine;

    public void SetImmediate(int level, int currentXp, int requiredXp)
    {
        SetLabels(level);
        SetFill(requiredXp > 0 ? (float)currentXp / requiredXp : 0f);
    }

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

            for (float t = 0f; t < secondsPerStep; t += Time.unscaledDeltaTime)
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
