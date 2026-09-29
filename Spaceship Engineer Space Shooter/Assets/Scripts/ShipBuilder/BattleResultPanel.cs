using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The single panel shown at battle-end — same GameObject for both win and loss, just with different
/// content: animated star reveal (win) or empty stars (loss), a result message, the Next Level button's
/// enabled state, and the rewards earned this battle (see BattleOutcomeController for how those are
/// computed). BattleOutcomeController decides win/loss and calls ShowResult(...) — this component only
/// knows how to DISPLAY that outcome, not how to reach it.
///
/// SETUP: put on the panel's root (starts inactive in the scene). Wire the three buttons' own OnClick
/// directly to BattleOutcomeController.RetryLevel / GoHome / NextLevel — this component doesn't own
/// button routing, just Stars / Result Label / Next Level Button's interactable state / rewards.
/// </summary>
public class BattleResultPanel : MonoBehaviour
{
    [Header("Stars")]
    [Tooltip("Exactly 3, left to right.")]
    public Image[] stars = new Image[3];
    public Sprite filledStarSprite;
    public Sprite emptyStarSprite;
    [Tooltip("Seconds between each star revealing, on a win.")]
    public float starRevealDelay = 0.3f;

    [Header("Message")]
    [Tooltip("Set these to whatever wording you want — this component just swaps between them.")]
    public TMP_Text resultLabel;
    public string victoryMessage = "Поздравляем, победили!";
    public string defeatMessage = "Вы проиграли";

    [Header("Rewards")]
    [Tooltip("Total credits earned this battle (kills + level-completion bonus, already scaled) — " +
             "counts up from 0 rather than appearing instantly.")]
    public TMP_Text creditsEarnedLabel;
    public float creditsCountDuration = 1f;
    [Tooltip("The account-level progress bar — optional; leave unassigned if the panel doesn't show one.")]
    public ExperienceBarView experienceBar;

    [Header("Buttons")]
    [Tooltip("Disabled on a loss, and on a win with no Next Level set on this LevelDefinition. Retry " +
             "and Home always work regardless — wire their OnClick directly, nothing to configure here.")]
    public Button nextLevelButton;

    private Coroutine revealRoutine;
    private Coroutine creditsRoutine;

    /// <summary>experienceSteps drives the level-up bar — see GameDataManager.AddExperience. Always has
    /// at least one entry (a zero-length one if nothing was earned), so the bar is safe to always play.</summary>
    public void ShowResult(bool won, int earnedStars, bool hasNextLevel, int creditsEarned, List<GameDataManager.ExperienceGainStep> experienceSteps)
    {
        gameObject.SetActive(true);

        if (resultLabel != null) resultLabel.text = won ? victoryMessage : defeatMessage;
        if (nextLevelButton != null) nextLevelButton.interactable = won && hasNextLevel;

        if (revealRoutine != null) StopCoroutine(revealRoutine);
        revealRoutine = StartCoroutine(won ? RevealStars(earnedStars) : ShowEmptyStars());

        if (creditsEarnedLabel != null)
        {
            if (creditsRoutine != null) StopCoroutine(creditsRoutine);
            creditsRoutine = StartCoroutine(CountUpCredits(creditsEarned));
        }

        if (experienceBar != null && experienceSteps != null && experienceSteps.Count > 0)
            experienceBar.PlaySteps(experienceSteps);
    }

    private IEnumerator CountUpCredits(int target)
    {
        float duration = Mathf.Max(0.01f, creditsCountDuration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            creditsEarnedLabel.text = Mathf.RoundToInt(Mathf.Lerp(0, target, t / duration)).ToString();
            yield return null;
        }
        creditsEarnedLabel.text = target.ToString();
    }

    private IEnumerator ShowEmptyStars()
    {
        SetAllStars(emptyStarSprite);
        yield break;
    }

    private IEnumerator RevealStars(int earnedStars)
    {
        SetAllStars(emptyStarSprite);

        for (int i = 0; i < stars.Length; i++)
        {
            yield return new WaitForSeconds(starRevealDelay);
            if (i >= earnedStars || stars[i] == null) continue;

            stars[i].sprite = filledStarSprite;
            yield return StartCoroutine(PunchScale(stars[i].transform));
        }
    }

    private void SetAllStars(Sprite sprite)
    {
        foreach (var star in stars)
            if (star != null) star.sprite = sprite;
    }

    private static IEnumerator PunchScale(Transform target)
    {
        const float duration = 0.15f;
        Vector3 normal = Vector3.one;
        Vector3 peak = normal * 1.3f;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            target.localScale = Vector3.Lerp(normal, peak, t / duration);
            yield return null;
        }
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            target.localScale = Vector3.Lerp(peak, normal, t / duration);
            yield return null;
        }
        target.localScale = normal;
    }
}
