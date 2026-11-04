using UnityEngine;

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

    private void OnEnable() => appliedNoAds = null;

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
