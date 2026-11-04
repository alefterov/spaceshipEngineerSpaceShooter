using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelRewardButtonView : MonoBehaviour
{
    public Button button;
    public TMP_Text levelLabel;
    [Tooltip("The row of up to three rewards.")]
    public RewardRowView rewards;
    [Tooltip("Shown only once the reward has been taken (a check mark, a frame...).")]
    public GameObject claimedMark;
    [Tooltip("The whole button's opacity while the level is not reached yet / the reward is taken.")]
    [Range(0f, 1f)] public float lockedAlpha = 0.5f;
    [Range(0f, 1f)] public float claimedAlpha = 0.7f;

    public int Level { get; private set; }

    private Action<int> onClaim;
    private CanvasGroup group;

    private void Awake()
    {
        EnsureGroup();
    }

    private void EnsureGroup()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(OnPressed);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnPressed);
    }

    public void Setup(int level, IReadOnlyList<RewardData> data, RewardIcons icons, Action<int> claimCallback)
    {
        Level = level;
        onClaim = claimCallback;
        if (levelLabel != null) levelLabel.text = level.ToString();
        if (rewards != null) rewards.Show(data, icons);
    }

    public void Refresh(bool reached, bool claimed)
    {
        if (group == null) EnsureGroup();

        if (button != null) button.interactable = reached && !claimed;
        if (claimedMark != null) claimedMark.SetActive(claimed);
        group.alpha = claimed ? claimedAlpha : reached ? 1f : lockedAlpha;
    }

    private void OnPressed() => onClaim?.Invoke(Level);
}
