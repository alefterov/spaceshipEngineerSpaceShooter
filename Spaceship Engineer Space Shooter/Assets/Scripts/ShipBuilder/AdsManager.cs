using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ADMOB_SDK && !UNITY_EDITOR
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

/// <summary>
/// All the game's advertising in one place (Google AdMob): a banner, an interstitial between battles and a rewarded video.
///
///  - BANNER: shown at the bottom (or top) of the screen, but ONLY while at least one BannerSlot is active in the scene — put a BannerSlot
///    on the panels that may carry a banner (main menu, level map) and nowhere else (the builder and the battle need the whole screen).
///  - INTERSTITIAL: a full-screen ad shown when the next scene opens after a battle (the result panel's Retry / Next / Home). It is
///    rationed: not before Skip First Battles battles have been played, then every Every N Battles battles, and never closer together than
///    Min Seconds Between Ads.
///  - REWARDED: the player chooses to watch a video for a reward — ShowRewarded(onRewarded) from a button (see RewardedAdButton, and the
///    "double the reward" button of BattleResultPanel). The reward is given only if the video was watched to the end.
///  - A player who bought "No ads" (GameDataManager.AdsRemoved) sees no banner and no interstitial; rewarded videos stay available.
///  - CONSENT: in the EEA / UK a consent form (Google's UMP) is shown first; configure its text in AdMob → Privacy & messaging.
///
/// IN THE EDITOR (and in Development Builds without the SDK) nothing real is shown: the log says what would happen and a rewarded video
/// "plays" for a second and pays. A RELEASE build without the SDK shows no ads and pays nothing.
///
/// SETUP: install the Google Mobile Ads Unity plugin and enter the Android App ID (Assets → Google Mobile Ads → Settings) — the
/// ADMOB_SDK symbol is then added by itself (see AdsDefineSetter). Put ONE AdsManager on the bootstrap object of the first scene (next to
/// GameDataManager): it survives scene changes. Keep Use Test Ads ON until the very last build, then paste the real ad unit IDs and turn it
/// off — clicking your own live ads can get the AdMob account banned.
/// </summary>
public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

    // Google's own sample ad units: always safe to show and click.
    private const string TestBanner = "ca-app-pub-3940256099942544/6300978111";
    private const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";
    private const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";

    [Header("Mode")]
    [Tooltip("ON: Google's sample ads (safe). OFF: your real ad units below. Turn it off only for the final release build.")]
    public bool useTestAds = true;
    [Tooltip("Ask for the user's consent first where the law requires it (EEA / UK). Needs a message set up in AdMob → Privacy & messaging.")]
    public bool useConsentForm = true;

    [Header("Android ad unit IDs (from AdMob, the real ones)")]
    public string bannerUnitId = "";
    public string interstitialUnitId = "";
    public string rewardedUnitId = "";

    [Header("Banner")]
    public bool bannerEnabled = true;

    [Header("Interstitial (between battles)")]
    public bool interstitialEnabled = true;
    [Tooltip("The first ad comes only after this many battles have been played.")]
    [Min(0)] public int skipFirstBattles = 2;
    [Tooltip("After that, an ad after every this many battles (1 = after every battle).")]
    [Min(1)] public int everyNBattles = 2;
    [Tooltip("Never two full-screen ads closer together than this (seconds).")]
    [Min(0f)] public float minSecondsBetweenAds = 120f;

    [Header("Rewarded")]
    public bool rewardedEnabled = true;

    /// <summary>Fired when a rewarded video becomes ready or stops being ready — buttons refresh on this.</summary>
    public event Action OnRewardedReadyChanged;

    private readonly System.Collections.Generic.List<BannerSlot> bannerSlots = new();
    private int battlesFinished;
    private int battlesSinceInterstitial;
    private bool interstitialPending;
    private float lastFullScreenAdTime = -9999f;
    private bool initialized;
    private float savedTimeScale = 1f;
    private bool pausedForAd;

    private bool AdsRemoved => GameDataManager.Instance != null && GameDataManager.Instance.AdsRemoved;

    private string BannerId => useTestAds || string.IsNullOrEmpty(bannerUnitId) ? TestBanner : bannerUnitId;
    private string InterstitialId => useTestAds || string.IsNullOrEmpty(interstitialUnitId) ? TestInterstitial : interstitialUnitId;
    private string RewardedId => useTestAds || string.IsNullOrEmpty(rewardedUnitId) ? TestRewarded : rewardedUnitId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Start() => StartAds();

    // =====================================================================================================
    //  Public API
    // =====================================================================================================

    /// <summary>Whether a rewarded video can be shown right now.</summary>
    public bool IsRewardedReady
    {
        get
        {
            if (!rewardedEnabled) return false;
#if ADMOB_SDK && !UNITY_EDITOR
            return rewardedAd != null && rewardedAd.CanShowAd();
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            return true; // pretend
#else
            return false;
#endif
        }
    }

    /// <summary>Shows a rewarded video. onRewarded runs only if it was watched to the end; onFailed if it could not be shown or was skipped.</summary>
    public void ShowRewarded(Action onRewarded, Action onFailed = null)
    {
        if (!IsRewardedReady)
        {
            onFailed?.Invoke();
            return;
        }

#if ADMOB_SDK && !UNITY_EDITOR
        ShowRewardedReal(onRewarded, onFailed);
#else
        StartCoroutine(FakeRewarded(onRewarded));
#endif
    }

    /// <summary>A BannerSlot became active: the banner may be shown, over that slot.</summary>
    public void AddBannerSlot(BannerSlot slot)
    {
        if (!bannerSlots.Contains(slot)) bannerSlots.Add(slot);
        RefreshBanner();
    }

    /// <summary>A BannerSlot went away: with none left the banner is hidden.</summary>
    public void RemoveBannerSlot(BannerSlot slot)
    {
        bannerSlots.Remove(slot);
        RefreshBanner();
    }

    /// <summary>Called by BattleOutcomeController when a battle ends (won or lost): the interstitial is shown when the next scene opens.</summary>
    public void NotifyBattleFinished()
    {
        battlesFinished++;
        battlesSinceInterstitial++;

        interstitialPending = interstitialEnabled && !AdsRemoved
            && battlesFinished > skipFirstBattles
            && battlesSinceInterstitial >= everyNBattles
            && Time.realtimeSinceStartup - lastFullScreenAdTime >= minSecondsBetweenAds;
    }

    /// <summary>Whether the player must be offered a "Privacy settings" button (the EEA / UK rule). Wire the button to ShowPrivacyOptions().</summary>
    public bool PrivacyOptionsRequired
    {
        get
        {
#if ADMOB_SDK && !UNITY_EDITOR
            return ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
#else
            return false;
#endif
        }
    }

    /// <summary>Opens the consent settings form, where the player can change their choice.</summary>
    public void ShowPrivacyOptions()
    {
#if ADMOB_SDK && !UNITY_EDITOR
        ConsentForm.ShowPrivacyOptionsForm((FormError error) =>
        {
            if (error != null) Debug.LogWarning($"[Ads] Privacy options form: {error.Message}");
        });
#endif
    }

    // =====================================================================================================
    //  Scenes and the banner
    // =====================================================================================================

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshBanner(); // the slots keep their own count: a closed scene's slots have already left (OnDisable), the new ones have joined

        if (interstitialPending) StartCoroutine(ShowInterstitialSoon());
    }

    private IEnumerator ShowInterstitialSoon()
    {
        yield return null; // let the new scene finish loading, then cover it
        if (!interstitialPending) yield break;
        interstitialPending = false;

        if (AdsRemoved || !interstitialEnabled) yield break;
        ShowInterstitial();
    }

    private void RefreshBanner()
    {
        bool show = bannerEnabled && bannerSlots.Count > 0 && !AdsRemoved;
#if ADMOB_SDK && !UNITY_EDITOR
        if (!initialized) return;
        if (show)
        {
            if (bannerView == null) CreateBanner(bannerSlots[bannerSlots.Count - 1]);
            else PlaceBanner(bannerSlots[bannerSlots.Count - 1]);
            bannerView?.Show();
        }
        else
        {
            bannerView?.Hide();
        }
#else
        if (show != bannerShownLog)
            Debug.Log(show ? $"[Ads] (fake) banner shown over '{bannerSlots[bannerSlots.Count - 1].name}' at {bannerSlots[bannerSlots.Count - 1].GetScreenRect()}" : "[Ads] (fake) banner hidden");
        bannerShownLog = show;
#endif
    }

    private bool bannerShownLog;

    // =====================================================================================================
    //  Pausing the game while a full-screen ad is up
    // =====================================================================================================

    private void PauseForAd()
    {
        if (pausedForAd) return;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        pausedForAd = true;
    }

    private void ResumeAfterAd()
    {
        if (!pausedForAd) return;
        Time.timeScale = savedTimeScale;
        AudioListener.pause = false;
        pausedForAd = false;
        lastFullScreenAdTime = Time.realtimeSinceStartup;
    }

    // =====================================================================================================
    //  Editor / development stand-ins
    // =====================================================================================================

#if !(ADMOB_SDK && !UNITY_EDITOR)
    private void StartAds()
    {
        initialized = true;
        Debug.Log("[Ads] (fake) AdMob is not active here — ads are only logged. Real ads: install the plugin and build for Android.");
    }

    private void ShowInterstitial()
    {
        Debug.Log("[Ads] (fake) interstitial");
        battlesSinceInterstitial = 0;
        lastFullScreenAdTime = Time.realtimeSinceStartup;
    }

    private IEnumerator FakeRewarded(Action onRewarded)
    {
        Debug.Log("[Ads] (fake) rewarded video playing for 1 second...");
        PauseForAd();
        yield return new WaitForSecondsRealtime(1f);
        ResumeAfterAd();
        Debug.Log("[Ads] (fake) rewarded video finished — reward given");
        onRewarded?.Invoke();
    }
#endif

    // =====================================================================================================
    //  The real thing (Android, with the SDK)
    // =====================================================================================================

#if ADMOB_SDK && !UNITY_EDITOR
    private BannerView bannerView;
    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;

    private void StartAds()
    {
        MobileAds.RaiseAdEventsOnUnityMainThread = true; // the ad callbacks come back on Unity's main thread

        if (!useConsentForm)
        {
            InitializeSdk();
            return;
        }

        // Ask for consent where the law requires it, then start the SDK (Google's standard order).
        ConsentInformation.Update(new ConsentRequestParameters(), (FormError updateError) =>
        {
            if (updateError != null) Debug.LogWarning($"[Ads] Consent update: {updateError.Message}");

            ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) =>
            {
                if (formError != null) Debug.LogWarning($"[Ads] Consent form: {formError.Message}");
                if (ConsentInformation.CanRequestAds()) InitializeSdk();
            });
        });

        if (ConsentInformation.CanRequestAds()) InitializeSdk(); // consent from an earlier session
    }

    private void InitializeSdk()
    {
        if (initialized) return;
        initialized = true;

        Debug.Log("[Ads] Initialising AdMob...");
        MobileAds.Initialize((InitializationStatus status) =>
        {
            Debug.Log($"[Ads] AdMob initialised; banner slots waiting: {bannerSlots.Count}");
            LoadInterstitial();
            LoadRewarded();
            RefreshBanner();
        });
    }

    // ----- banner -----

    private int bannerWidthDp;
    private int bannerHeightDp;
    private float nextBannerCheck;

    // Pixels of the screen -> the density-independent units the banner is placed in.
    private static float Density => Screen.dpi > 0f ? Screen.dpi / 160f : 2.5f;

    private static int WidthDpOf(BannerSlot slot)
        => Mathf.Max(32, Mathf.RoundToInt(Mathf.Min(slot.GetScreenRect().width, Screen.width) / Density));

    /// <summary>The banner is a native view floating over the game, not part of the canvas: it takes the width of the slot's rectangle and sits
    /// in the middle of it.</summary>
    private void CreateBanner(BannerSlot slot)
    {
        bannerWidthDp = WidthDpOf(slot);
        AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(bannerWidthDp);
        bannerHeightDp = size.Height > 0 ? size.Height : 60;

        bannerView = new BannerView(BannerId, size, 0, 0);
        bannerView.OnBannerAdLoaded += () => Debug.Log($"[Ads] Banner loaded ({bannerWidthDp}x{bannerHeightDp} dp)");
        bannerView.OnBannerAdLoadFailed += (LoadAdError error) => Debug.LogWarning($"[Ads] Banner failed to load: {error}");
        PlaceBanner(slot);
        Debug.Log($"[Ads] Banner requested: {bannerWidthDp}x{bannerHeightDp} dp over slot '{slot.name}', ad unit {BannerId}");
        bannerView.LoadAd(new AdRequest());
    }

    private void PlaceBanner(BannerSlot slot)
    {
        if (bannerView == null) return;

        Rect rect = slot.GetScreenRect();
        float left = rect.center.x / Density - bannerWidthDp * 0.5f;
        float top = (Screen.height - rect.center.y) / Density - bannerHeightDp * 0.5f; // the screen's y grows upward, the banner's grows downward
        bannerView.SetPosition(Mathf.RoundToInt(left), Mathf.RoundToInt(top));
    }

    private void Update()
    {
        if (bannerView == null || bannerSlots.Count == 0 || Time.unscaledTime < nextBannerCheck) return;
        nextBannerCheck = Time.unscaledTime + 0.5f; // the slot may move or change size (rotation, layout)

        var slot = bannerSlots[bannerSlots.Count - 1];
        if (Mathf.Abs(WidthDpOf(slot) - bannerWidthDp) > 4)
        {
            bannerView.Destroy(); // the slot got wider or narrower: a banner of the right width is needed
            bannerView = null;
            RefreshBanner();
        }
        else
        {
            PlaceBanner(slot);
        }
    }

    // ----- interstitial -----

    private void LoadInterstitial()
    {
        if (!interstitialEnabled) return;

        interstitialAd?.Destroy();
        interstitialAd = null;

        InterstitialAd.Load(InterstitialId, new AdRequest(), (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[Ads] Interstitial failed to load: {error}");
                return;
            }

            interstitialAd = ad;
            ad.OnAdFullScreenContentClosed += () => { ResumeAfterAd(); LoadInterstitial(); };
            ad.OnAdFullScreenContentFailed += (AdError e) => { Debug.LogWarning($"[Ads] Interstitial failed to show: {e}"); ResumeAfterAd(); LoadInterstitial(); };
        });
    }

    private void ShowInterstitial()
    {
        if (interstitialAd == null || !interstitialAd.CanShowAd()) return; // not loaded in time: skipped, not delayed

        battlesSinceInterstitial = 0;
        PauseForAd();
        interstitialAd.Show();
    }

    // ----- rewarded -----

    private void LoadRewarded()
    {
        if (!rewardedEnabled) return;

        rewardedAd?.Destroy();
        rewardedAd = null;

        RewardedAd.Load(RewardedId, new AdRequest(), (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[Ads] Rewarded failed to load: {error}");
                OnRewardedReadyChanged?.Invoke();
                return;
            }

            rewardedAd = ad;
            OnRewardedReadyChanged?.Invoke();
        });
    }

    private void ShowRewardedReal(Action onRewarded, Action onFailed)
    {
        var ad = rewardedAd;
        bool earned = false;

        ad.OnAdFullScreenContentClosed += () =>
        {
            ResumeAfterAd();
            rewardedAd = null;
            OnRewardedReadyChanged?.Invoke();
            LoadRewarded();

            if (earned) onRewarded?.Invoke();
            else onFailed?.Invoke();
        };
        ad.OnAdFullScreenContentFailed += (AdError e) =>
        {
            Debug.LogWarning($"[Ads] Rewarded failed to show: {e}");
            ResumeAfterAd();
            rewardedAd = null;
            OnRewardedReadyChanged?.Invoke();
            LoadRewarded();
            onFailed?.Invoke();
        };

        PauseForAd();
        ad.Show((Reward reward) => earned = true);
    }
#endif
}
