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
    [Tooltip("The level info panel. Shown only while a campaign's level map is open — never on the campaign-selection screen. " +
             "Found from the Level Select popup's Info Panel field if left empty.")]
    public LevelInfoPanel infoPanel;

    private bool inCampaign;

    private void Awake()
    {
        foreach (var campaign in campaigns)
        {
            var target = campaign; // capture this specific entry, not the loop variable
            if (target.selectButton != null) target.selectButton.onClick.AddListener(() => ShowCampaign(target));
        }

        if (backButton != null) backButton.onClick.AddListener(Back);
    }

    // Always land back on campaign selection when the popup (re)opens — a player returning later
    // shouldn't be dropped straight into whichever campaign they happened to leave open last time.
    private void OnEnable() => ShowSelection();

    /// <summary>One step back, wherever the player is: from a campaign's level map to the campaign selection; from the
    /// campaign selection out of this popup to the game-mode selection (Campaign / Survival). Wire the BACK button
    /// to this.</summary>
    public void Back()
    {
        if (inCampaign)
        {
            ShowSelection();
            return;
        }

        var popup = GetComponent<LevelSelectPopup>();
        if (popup != null) popup.Back();
    }

    public void ShowSelection()
    {
        inCampaign = false;
        SetInfoPanel(false);

        if (selectionRoot != null) selectionRoot.SetActive(true);
        foreach (var c in campaigns)
            if (c.panel != null) c.panel.SetActive(false);
    }

    private void ShowCampaign(Campaign campaign)
    {
        inCampaign = true;

        if (selectionRoot != null) selectionRoot.SetActive(false);
        foreach (var c in campaigns)
            if (c.panel != null) c.panel.SetActive(c == campaign);

        SetInfoPanel(true); // after the map is up, so the panel picks its default level from THIS campaign's buttons
    }

    private void SetInfoPanel(bool visible)
    {
        if (infoPanel == null)
        {
            var popup = GetComponent<LevelSelectPopup>();
            if (popup != null) infoPanel = popup.infoPanel;
        }

        if (infoPanel != null) infoPanel.gameObject.SetActive(visible);
    }
}
