using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Binds a TextMeshPro label to one of the player's currencies (credits or coins), keeping it in
/// sync with GameDataManager. Plays a brief "can't afford it" pulse — grows, tints red, eases back
/// over PulseDuration seconds — when a spend attempt for that specific currency fails
/// (GameDataManager.OnInsufficientCredits / OnInsufficientCoins).
///
/// SETUP: put one of these on each currency's TextMeshPro label (e.g. "CreditsText" and "CoinsText"),
/// set Currency accordingly, and assign Label to the TMP_Text on the same object.
/// </summary>
public class CurrencyDisplay : MonoBehaviour
{
    public enum Currency { Credits, Coins, ResearchPoints }

    [Header("Binding")]
    public Currency currency = Currency.Credits;
    public TMP_Text label;

    [Header("Insufficient-funds pulse")]
    public Color insufficientColor = Color.red;
    [Tooltip("How large the text grows at the peak of the pulse (1 = no change).")]
    public float pulseScale = 1.3f;
    [Tooltip("Total time for the pulse — half growing/reddening, half easing back.")]
    public float pulseDuration = 1f;

    private Color normalColor;
    private Vector3 normalScale;
    private Coroutine pulseRoutine;
    private bool subscribed;

    private void Awake()
    {
        if (label == null) return;
        normalColor = label.color;
        normalScale = label.transform.localScale;
    }

    // Subscribing needs GameDataManager.Instance to already be set (its own Awake). Unity doesn't
    // guarantee Awake order BETWEEN objects, so OnEnable alone could run before GameDataManager's
    // Awake if this object happens to sit earlier in the scene — Start() is used as a fallback since
    // every object's Awake is guaranteed to finish before any object's Start runs.
    private void OnEnable() => TrySubscribe();
    private void Start() => TrySubscribe();
    private void OnDisable() => Unsubscribe();

    private void TrySubscribe()
    {
        if (subscribed) return;
        var data = GameDataManager.Instance;
        if (data == null) return;

        switch (currency)
        {
            case Currency.Credits:
                data.OnCreditsChanged += Refresh;
                data.OnInsufficientCredits += Pulse;
                break;
            case Currency.Coins:
                data.OnCoinsChanged += Refresh;
                data.OnInsufficientCoins += Pulse;
                break;
            case Currency.ResearchPoints:
                data.OnResearchPointsChanged += Refresh;
                data.OnInsufficientResearchPoints += Pulse;
                break;
        }

        subscribed = true;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        var data = GameDataManager.Instance;
        if (data == null) return;

        switch (currency)
        {
            case Currency.Credits:
                data.OnCreditsChanged -= Refresh;
                data.OnInsufficientCredits -= Pulse;
                break;
            case Currency.Coins:
                data.OnCoinsChanged -= Refresh;
                data.OnInsufficientCoins -= Pulse;
                break;
            case Currency.ResearchPoints:
                data.OnResearchPointsChanged -= Refresh;
                data.OnInsufficientResearchPoints -= Pulse;
                break;
        }
    }

    private void Refresh()
    {
        if (label == null) return;
        int amount = currency switch
        {
            Currency.Credits => GameDataManager.Instance.Credits,
            Currency.Coins => GameDataManager.Instance.Coins,
            _ => GameDataManager.Instance.ResearchPoints,
        };
        label.text = amount.ToString();
    }

    /// <summary>Briefly grows and tints the text red, then eases back. Also callable directly if
    /// something wants to flash this display outside the normal insufficient-funds event.</summary>
    public void Pulse()
    {
        if (label == null) return;
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        float half = Mathf.Max(0.01f, pulseDuration * 0.5f);
        Vector3 peakScale = normalScale * pulseScale;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            float p = t / half;
            label.transform.localScale = Vector3.Lerp(normalScale, peakScale, p);
            label.color = Color.Lerp(normalColor, insufficientColor, p);
            yield return null;
        }

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            float p = t / half;
            label.transform.localScale = Vector3.Lerp(peakScale, normalScale, p);
            label.color = Color.Lerp(insufficientColor, normalColor, p);
            yield return null;
        }

        label.transform.localScale = normalScale;
        label.color = normalColor;
        pulseRoutine = null;
    }
}
