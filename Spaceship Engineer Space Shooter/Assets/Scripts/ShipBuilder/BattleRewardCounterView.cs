using TMPro;
using UnityEngine;

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
