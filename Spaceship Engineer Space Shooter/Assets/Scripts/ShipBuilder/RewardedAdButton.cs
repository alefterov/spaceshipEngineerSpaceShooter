using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A button that offers a rewarded video: press it, watch the video, get Reward Credits. It is pressable only while a video is ready,
/// and (optionally) limited to Max Uses Per Session. Use it anywhere you want a "watch an ad for credits" button; the "double the
/// reward" button of the battle result is a separate part of BattleResultPanel.
///
/// SETUP: put on the button object (or anywhere) and assign Button; Label is optional (shows "+amount").
/// </summary>
public class RewardedAdButton : MonoBehaviour
{
    public Button button;
    [Tooltip("Optional: shows the reward amount.")]
    public TMP_Text label;
    [Min(1)] public int rewardCredits = 100;
    [Tooltip("0 = unlimited. Otherwise the button stops working after this many videos in one play session.")]
    [Min(0)] public int maxUsesPerSession = 0;

    private static int usesThisSession;
    private bool waiting;

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(OnPressed);
        if (AdsManager.Instance != null) AdsManager.Instance.OnRewardedReadyChanged += Refresh;
        if (label != null) label.text = "+" + NumberFormat.Compact(rewardCredits);
        Refresh();
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnPressed);
        if (AdsManager.Instance != null) AdsManager.Instance.OnRewardedReadyChanged -= Refresh;
    }

    private void Update() => Refresh(); // the ad may become ready at any moment, and a callback can come from a stale instance

    private void Refresh()
    {
        if (button == null) return;
        bool limited = maxUsesPerSession > 0 && usesThisSession >= maxUsesPerSession;
        button.interactable = !waiting && !limited && AdsManager.Instance != null && AdsManager.Instance.IsRewardedReady;
    }

    private void OnPressed()
    {
        if (waiting || AdsManager.Instance == null) return;
        waiting = true;
        Refresh();

        AdsManager.Instance.ShowRewarded(
            onRewarded: () =>
            {
                waiting = false;
                usesThisSession++;
                GameDataManager.Instance?.AddCredits(rewardCredits);
                Refresh();
            },
            onFailed: () =>
            {
                waiting = false;
                Refresh();
            });
    }
}
