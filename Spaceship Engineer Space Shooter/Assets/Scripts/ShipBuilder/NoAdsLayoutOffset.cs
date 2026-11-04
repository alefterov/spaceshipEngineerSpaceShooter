using UnityEngine;

/// <summary>
/// Moves a UI element to make room for the ad banner — or not: its Y position is With Ads Y while ads are shown, and No Ads Y once the
/// player has bought "No ads" / the premium (GameDataManager.AdsRemoved), since the banner's space is not needed any more. Switches by
/// itself the moment the purchase is made.
///
/// SETUP: put on the element (e.g. BuilderPanel) and enter both positions (its Rect Transform's Pos Y).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class NoAdsLayoutOffset : MonoBehaviour
{
    [Tooltip("Pos Y while ads are shown (room left for the banner).")]
    public float withAdsY = 370f;
    [Tooltip("Pos Y once ads are removed.")]
    public float noAdsY = 270f;

    private RectTransform rect;
    private bool? appliedNoAds;

    private void Awake() => rect = (RectTransform)transform;

    private void OnEnable() => appliedNoAds = null; // place it again whenever it is shown

    // A cheap check every frame: "No ads" can be bought from several places (shop, premium, restored purchase).
    private void Update()
    {
        var data = GameDataManager.Instance;
        bool noAds = data != null && data.AdsRemoved;
        if (appliedNoAds == noAds) return;

        appliedNoAds = noAds;
        var position = rect.anchoredPosition;
        position.y = noAds ? noAdsY : withAdsY;
        rect.anchoredPosition = position;
    }
}
