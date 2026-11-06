using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ADMOB_SDK && !UNITY_EDITOR
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

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

    public bool IsRewardedReady
    {
        get
        {
            if (!rewardedEnabled) return false;
#if ADMOB_SDK && !UNITY_EDITOR
            return rewardedAd != null && rewardedAd.CanShowAd();
#elif UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return false;
#endif
        }
    }

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

    public void AddBannerSlot(BannerSlot slot)
    {
        if (!bannerSlots.Contains(slot)) bannerSlots.Add(slot);
        RefreshBanner();
    }

    public void RemoveBannerSlot(BannerSlot slot)
    {
        bannerSlots.Remove(slot);
        RefreshBanner();
    }

    public bool IsFullScreenAdBusy => interstitialPending || pausedForAd;

    public void NotifyBattleFinished()
    {
        battlesFinished++;
        battlesSinceInterstitial++;

        interstitialPending = interstitialEnabled && !AdsRemoved
            && battlesFinished > skipFirstBattles
            && battlesSinceInterstitial >= everyNBattles
            && Time.realtimeSinceStartup - lastFullScreenAdTime >= minSecondsBetweenAds;
    }

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

    public void ShowPrivacyOptions()
    {
#if ADMOB_SDK && !UNITY_EDITOR
        ConsentForm.ShowPrivacyOptionsForm((FormError error) =>
        {
            if (error != null) Debug.LogWarning($"[Ads] Privacy options form: {error.Message}");
        });
#endif
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshBanner();

        if (interstitialPending) StartCoroutine(ShowInterstitialSoon());
    }

    private IEnumerator ShowInterstitialSoon()
    {
        yield return null;
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

        bannerShownLog = show;
#endif
    }

    private bool bannerShownLog;

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

#if !(ADMOB_SDK && !UNITY_EDITOR)
    private void StartAds()
    {
        initialized = true;
    }

    private void ShowInterstitial()
    {
        battlesSinceInterstitial = 0;
        lastFullScreenAdTime = Time.realtimeSinceStartup;
    }

    private IEnumerator FakeRewarded(Action onRewarded)
    {
        PauseForAd();
        yield return new WaitForSecondsRealtime(1f);
        ResumeAfterAd();

        onRewarded?.Invoke();
    }
#endif

#if ADMOB_SDK && !UNITY_EDITOR
    private BannerView bannerView;
    private InterstitialAd interstitialAd;
    private RewardedAd rewardedAd;

    private void StartAds()
    {
        MobileAds.RaiseAdEventsOnUnityMainThread = true;

        if (!useConsentForm)
        {
            InitializeSdk();
            return;
        }

        ConsentInformation.Update(new ConsentRequestParameters(), (FormError updateError) =>
        {
            if (updateError != null) Debug.LogWarning($"[Ads] Consent update: {updateError.Message}");

            ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) =>
            {
                if (formError != null) Debug.LogWarning($"[Ads] Consent form: {formError.Message}");
                if (ConsentInformation.CanRequestAds()) InitializeSdk();
            });
        });

        if (ConsentInformation.CanRequestAds()) InitializeSdk();
    }

    private void InitializeSdk()
    {
        if (initialized) return;
        initialized = true;

        MobileAds.Initialize((InitializationStatus status) =>
        {
            LoadInterstitial();
            LoadRewarded();
            RefreshBanner();
        });
    }

    private int bannerWidthDp;
    private int bannerHeightDp;
    private float nextBannerCheck;

    private static float Density => Screen.dpi > 0f ? Screen.dpi / 160f : 2.5f;

    private static int WidthDpOf(BannerSlot slot)
        => Mathf.Max(32, Mathf.RoundToInt(Mathf.Min(slot.GetScreenRect().width, Screen.width) / Density));

    private void CreateBanner(BannerSlot slot)
    {
        bannerWidthDp = WidthDpOf(slot);
        AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(bannerWidthDp);
        bannerHeightDp = size.Height > 0 ? size.Height : 60;

        bannerView = new BannerView(BannerId, size, 0, 0);

        bannerView.OnBannerAdLoadFailed += (LoadAdError error) => Debug.LogWarning($"[Ads] Banner failed to load: {error}");
        PlaceBanner(slot);

        bannerView.LoadAd(new AdRequest());
    }

    private void PlaceBanner(BannerSlot slot)
    {
        if (bannerView == null) return;

        Rect rect = slot.GetScreenRect();
        float left = rect.center.x / Density - bannerWidthDp * 0.5f;
        float top = (Screen.height - rect.center.y) / Density - bannerHeightDp * 0.5f;
        bannerView.SetPosition(Mathf.RoundToInt(left), Mathf.RoundToInt(top));
    }

    private void Update()
    {
        if (bannerView == null || bannerSlots.Count == 0 || Time.unscaledTime < nextBannerCheck) return;
        nextBannerCheck = Time.unscaledTime + 0.5f;

        var slot = bannerSlots[bannerSlots.Count - 1];
        if (Mathf.Abs(WidthDpOf(slot) - bannerWidthDp) > 4)
        {
            bannerView.Destroy();
            bannerView = null;
            RefreshBanner();
        }
        else
        {
            PlaceBanner(slot);
        }
    }

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
        if (interstitialAd == null || !interstitialAd.CanShowAd()) return;

        battlesSinceInterstitial = 0;
        PauseForAd();
        interstitialAd.Show();
    }

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
