using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Switches between the campaign-select screen and one of several per-campaign level maps inside the
/// Level Select popup. Each campaign is its own panel — typically a ScrollView with its own
/// LevelMapScroller and hand-placed level buttons/connecting lines — and exactly one of
/// (selectionRoot, one campaign's panel) is visible at a time.
///
/// SETUP: put this on the Level Select popup (alongside LevelSelectPopup). Selection buttons wire
/// themselves in Awake — nothing to hook up in their own OnClick().
/// </summary>
public class CampaignSelector : MonoBehaviour
{
    [Serializable]
    public class Campaign
    {
        [Tooltip("Button on the selection screen that opens this campaign.")]
        public Button selectButton;
        [Tooltip("This campaign's own panel — its ScrollView + LevelMapScroller + hand-placed level buttons.")]
        public GameObject panel;
    }

    [Tooltip("Shown first whenever this popup opens — the row of campaign-select buttons.")]
    public GameObject selectionRoot;
    public List<Campaign> campaigns = new();
    [Tooltip("Returns from whichever campaign panel is open back to the selection screen.")]
    public Button backButton;

    private void Awake()
    {
        foreach (var campaign in campaigns)
        {
            var target = campaign; // capture this specific entry, not the loop variable
            if (target.selectButton != null) target.selectButton.onClick.AddListener(() => ShowCampaign(target));
        }

        if (backButton != null) backButton.onClick.AddListener(ShowSelection);
    }

    // Always land back on campaign selection when the popup (re)opens — a player returning later
    // shouldn't be dropped straight into whichever campaign they happened to leave open last time.
    private void OnEnable() => ShowSelection();

    public void ShowSelection()
    {
        if (selectionRoot != null) selectionRoot.SetActive(true);
        foreach (var c in campaigns)
            if (c.panel != null) c.panel.SetActive(false);
    }

    private void ShowCampaign(Campaign campaign)
    {
        if (selectionRoot != null) selectionRoot.SetActive(false);
        foreach (var c in campaigns)
            if (c.panel != null) c.panel.SetActive(c == campaign);
    }
}
