using TMPro;
using UnityEngine;

/// <summary>
/// Live HUD counter of credits earned from kills so far THIS battle (BattleRewards.Credits) — separate
/// from the player's account Credits shown in the ship builder (CurrencyDisplay). Whenever a kill adds
/// to the total, the displayed number doesn't jump: it counts up smoothly to the new total over Count
/// Duration seconds, so a burst of several kills in a row reads as one continuous climb rather than a
/// snap. If another kill lands mid-count, the animation just re-targets from wherever it currently is.
///
/// SETUP: put on the battle HUD's "coins earned" label; assign Label. Shows 0 for the whole intro
/// (BattleRewards is reset by BattleOutcomeController.BeginTracking, called once the intro ends).
/// </summary>
public class BattleRewardCounterView : MonoBehaviour
{
    public TMP_Text label;
    [Tooltip("Seconds the displayed number takes to catch up to a new total.")]
    public float countDuration = 1f;

    private float displayed;
    private float animFrom;
    private float animTarget;
    private float animElapsed;
    private bool animating;

    private void OnEnable()
    {
        BattleRewards.OnKillRewardChanged += HandleChanged;
        displayed = animFrom = animTarget = BattleRewards.Credits;
        animating = false;
        Refresh();
    }

    private void OnDisable() => BattleRewards.OnKillRewardChanged -= HandleChanged;

    private void HandleChanged()
    {
        animFrom = displayed;
        animTarget = BattleRewards.Credits;
        animElapsed = 0f;
        animating = true;
    }

    private void Update()
    {
        if (!animating) return;

        animElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(animElapsed / Mathf.Max(0.01f, countDuration));
        displayed = Mathf.Lerp(animFrom, animTarget, t);
        Refresh();

        if (t >= 1f) animating = false;
    }

    private void Refresh()
    {
        if (label != null) label.text = Mathf.RoundToInt(displayed).ToString();
    }
}
