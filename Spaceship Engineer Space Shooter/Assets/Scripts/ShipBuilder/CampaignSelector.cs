using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
            var target = campaign;
            if (target.selectButton != null) target.selectButton.onClick.AddListener(() => ShowCampaign(target));
        }

        if (backButton != null) backButton.onClick.AddListener(Back);
    }

    private void OnEnable() => ShowSelection();

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

        SetInfoPanel(true);
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
