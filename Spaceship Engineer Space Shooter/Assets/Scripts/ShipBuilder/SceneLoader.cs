using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public interface ISceneReady
{
    bool IsSceneReady { get; }
}

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Screen")]
    [Tooltip("The loading screen (a CanvasGroup on a canvas drawn over everything). Empty = a plain one is made.")]
    public CanvasGroup screen;
    [Tooltip("Optional: fills from 0 to 1 while loading (Image Type = Filled).")]
    public Image progressFill;
    [Tooltip("Optional: \"57%\".")]
    public TMP_Text percentLabel;

    [Header("Timing")]
    [Tooltip("The screen stays at least this long (seconds).")]
    [Min(0f)] public float minSeconds = 2f;
    [Tooltip("The longest the screen waits for the new scene's ISceneReady objects (seconds).")]
    [Min(0f)] public float maxWaitSeconds = 10f;
    [Tooltip("Fade in / out (seconds).")]
    [Min(0f)] public float fadeSeconds = 0.25f;

    public static bool IsLoading { get; private set; }

    public static void Load(string sceneName)
    {
        if (IsLoading) return;
        Ensure().StartCoroutine(Instance.LoadRoutine(sceneName));
    }

    private static SceneLoader Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("SceneLoader");
        return go.AddComponent<SceneLoader>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (screen == null) BuildPlainScreen();
        SetScreen(0f, false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        IsLoading = true;
        float started = Time.unscaledTime;
        SetProgress(0f);

        yield return Fade(1f);

        var operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            Debug.LogError($"SceneLoader: scene '{sceneName}' cannot be loaded (is it in Build Settings > Scenes In Build?).", this);
            yield return Fade(0f);
            IsLoading = false;
            yield break;
        }

        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            ShowProgress(started, operation.progress / 0.9f * 0.8f);
            yield return null;
        }

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            ShowProgress(started, 0.85f);
            yield return null;
        }

        yield return null;
        yield return null;

        float waitStarted = Time.unscaledTime;
        while (!NewSceneReady() && Time.unscaledTime - waitStarted < maxWaitSeconds)
        {
            ShowProgress(started, 0.95f);
            yield return null;
        }

        while (Time.unscaledTime - started < minSeconds)
        {
            ShowProgress(started, 1f);
            yield return null;
        }

        SetProgress(1f);
        yield return Fade(0f);
        IsLoading = false;
    }

    private static bool NewSceneReady()
    {
        var waiting = FindObjectsByType<MonoBehaviour>().OfType<ISceneReady>();
        return waiting.All(w => w.IsSceneReady);
    }

    private void ShowProgress(float started, float loaded)
    {
        float byTime = minSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - started) / minSeconds) : 1f;
        SetProgress(Mathf.Min(loaded, byTime));
    }

    private void SetProgress(float value)
    {
        if (progressFill != null) progressFill.fillAmount = value;
        if (percentLabel != null) percentLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private IEnumerator Fade(float to)
    {
        if (screen == null) yield break;
        SetScreen(screen.alpha, true);

        float from = screen.alpha;
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            screen.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
            yield return null;
        }

        SetScreen(to, to > 0f);
    }

    private void SetScreen(float alpha, bool shown)
    {
        if (screen == null) return;
        screen.alpha = alpha;
        screen.blocksRaycasts = shown;
        screen.interactable = shown;
        if (screen.gameObject != gameObject) screen.gameObject.SetActive(shown || alpha > 0f);
    }

    private void BuildPlainScreen()
    {
        var canvasObject = new GameObject("LoadingScreen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        screen = canvasObject.GetComponent<CanvasGroup>();

        var background = NewChild<Image>("Background", canvasObject.transform);
        Stretch(background.rectTransform);
        background.color = new Color(0.02f, 0.04f, 0.07f, 1f);

        var label = NewChild<TextMeshProUGUI>("Label", canvasObject.transform);
        label.text = L10n.Tr("Loading...");
        label.fontSize = 56;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.7294f, 0.949f, 0.984f);
        label.raycastTarget = false;
        label.rectTransform.sizeDelta = new Vector2(800, 100);
        label.rectTransform.anchoredPosition = new Vector2(0, 40);

        var barBack = NewChild<Image>("BarBack", canvasObject.transform);
        barBack.color = new Color(1f, 1f, 1f, 0.12f);
        barBack.rectTransform.sizeDelta = new Vector2(600, 12);
        barBack.rectTransform.anchoredPosition = new Vector2(0, -40);

        progressFill = NewChild<Image>("BarFill", barBack.transform);
        Stretch(progressFill.rectTransform);
        progressFill.color = new Color(0.7294f, 0.949f, 0.984f);
        progressFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillAmount = 0f;
    }

    private static T NewChild<T>(string name, Transform parent) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(T));
        go.transform.SetParent(parent, false);
        return go.GetComponent<T>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
