using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One tab of the priorities window: its button, the icon on the button and the panel that is shown for it.</summary>
[Serializable]
public class PriorityCategory
{
    [Tooltip("The category button. Pressing it shows this tab.")]
    public Button button;
    [Tooltip("The icon inside the button. It is fully opaque on the active tab and dimmed on the others.")]
    public Image icon;
    [Tooltip("The tab's content: an object with an EnergyPriorityController or a MissilePriorityController on it (it holds the rows). " +
             "Shown while this tab is active, hidden otherwise.")]
    public GameObject panel;

    [NonSerialized] public CanvasGroup group;
    [NonSerialized] public IPriorityPanel content;
    [NonSerialized] public float panelAlpha;
    [NonSerialized] public float iconAlpha;
}

/// <summary>
/// The ship-settings window of the battle: one window, opened by the Open Button (ShipSettingPriorityButton), with a tab for each kind of
/// priority — energy distribution, missile targets, tower-gun targets. Pressing a category button switches the content of the window;
/// the active category's icon is fully opaque, the others are dimmed. Everything fades smoothly: the window opening and closing, the
/// tab content crossfading, the icons brightening and dimming.
///
/// Opening the window PAUSES the battle (Time.timeScale = 0) — a deliberate tactical pause, as the separate windows used to do; closing it
/// restores the previous speed. Each tab's rows save their order on every drop, so the changes work at once, even while paused.
///
/// SETUP: put on an object that is always active (the canvas, say) — NOT on the window itself. Assign Window (the window's root object,
/// started inactive or not — it is hidden at start), Open Button and Close Button, and fill Categories with one entry per tab (Button,
/// Icon, Panel). A panel carries an EnergyPriorityController or a MissilePriorityController (Kind = Missiles or Tower Guns); the panels
/// all sit in the same place inside the window.
/// </summary>
public class ShipPrioritiesWindow : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("The window's root object. Hidden at start; shown by the Open Button.")]
    public GameObject window;
    [Tooltip("The ShipSettingPriorityButton: opens the window (and closes it if pressed again).")]
    public Button openButton;
    public Button closeButton;

    [Header("Categories")]
    public List<PriorityCategory> categories = new();
    [Tooltip("The tab that is shown first, and again each time the window is reopened (-1 = the one that was open last).")]
    public int startCategory = 0;

    [Header("Smoothness")]
    [Tooltip("Seconds for the window to fade in or out.")]
    [Min(0.01f)] public float windowFadeSeconds = 0.2f;
    [Tooltip("Seconds for a tab's content to fade in or out when the category changes.")]
    [Min(0.01f)] public float panelFadeSeconds = 0.2f;
    [Tooltip("Seconds for a category icon to change between active and dimmed.")]
    [Min(0.01f)] public float iconFadeSeconds = 0.15f;
    [Range(0f, 1f)] public float activeIconAlpha = 1f;
    [Range(0f, 1f)] public float inactiveIconAlpha = 0.5f;

    private CanvasGroup windowGroup;
    private readonly List<UnityEngine.Events.UnityAction> handlers = new();
    private int selected;
    private bool wantOpen;
    private float windowAlpha;
    private float savedTimeScale = 1f;
    private bool paused;

    public bool IsOpen => wantOpen;

    private void Awake()
    {
        if (window != null)
        {
            windowGroup = window.GetComponent<CanvasGroup>();
            if (windowGroup == null) windowGroup = window.AddComponent<CanvasGroup>();
            windowGroup.alpha = 0f;
            window.SetActive(false);
        }

        selected = Mathf.Clamp(startCategory, 0, Mathf.Max(0, categories.Count - 1));

        foreach (var category in categories)
        {
            if (category.panel == null) continue;

            category.group = category.panel.GetComponent<CanvasGroup>();
            if (category.group == null) category.group = category.panel.AddComponent<CanvasGroup>();
            category.content = category.panel.GetComponent<IPriorityPanel>();
            category.panelAlpha = 0f;
            category.group.alpha = 0f;
            category.panel.SetActive(false);
        }

        for (int i = 0; i < categories.Count; i++)
            categories[i].iconAlpha = i == selected ? activeIconAlpha : inactiveIconAlpha;
        ApplyIconAlphas();
    }

    private void OnEnable()
    {
        handlers.Clear();
        handlerButtons.Clear();

        if (openButton != null) AddHandler(openButton, Toggle);
        if (closeButton != null) AddHandler(closeButton, Close);

        for (int i = 0; i < categories.Count; i++)
        {
            int index = i;
            if (categories[i].button != null) AddHandler(categories[i].button, () => Select(index));
        }
    }

    private readonly List<Button> handlerButtons = new();

    private void AddHandler(Button button, UnityEngine.Events.UnityAction action)
    {
        handlers.Add(action);
        handlerButtons.Add(button);
        button.onClick.AddListener(action);
    }

    private void OnDisable()
    {
        for (int i = 0; i < handlers.Count; i++)
            if (handlerButtons[i] != null) handlerButtons[i].onClick.RemoveListener(handlers[i]);

        handlers.Clear();
        handlerButtons.Clear();
        Resume();
    }

    public void Toggle()
    {
        if (wantOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (wantOpen || window == null) return;
        wantOpen = true;

        if (startCategory >= 0) SelectImmediately(Mathf.Clamp(startCategory, 0, Mathf.Max(0, categories.Count - 1)));

        window.SetActive(true);
        foreach (var category in categories) category.content?.RefreshPanel(); // every tab shows the real current order

        if (!paused)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            paused = true;
        }
    }

    public void Close()
    {
        if (!wantOpen) return;
        wantOpen = false;
        Resume(); // the battle goes on while the window fades away
    }

    /// <summary>Shows another tab (the content crossfades).</summary>
    public void Select(int index)
    {
        if (index < 0 || index >= categories.Count || index == selected) return;
        selected = index;
        categories[index].content?.RefreshPanel();
    }

    private void SelectImmediately(int index)
    {
        selected = index;
        for (int i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            category.panelAlpha = i == index ? 1f : 0f;
            category.iconAlpha = i == index ? activeIconAlpha : inactiveIconAlpha;
            if (category.group != null) category.group.alpha = category.panelAlpha;
            if (category.panel != null) category.panel.SetActive(i == index);
        }

        ApplyIconAlphas();
    }

    private void Resume()
    {
        if (!paused) return;
        Time.timeScale = savedTimeScale;
        paused = false;
    }

    private void Update()
    {
        if (window == null) return;

        float dt = Time.unscaledDeltaTime; // the battle is paused, the window still has to move

        // The window itself.
        windowAlpha = Mathf.MoveTowards(windowAlpha, wantOpen ? 1f : 0f, dt / windowFadeSeconds);
        windowGroup.alpha = windowAlpha;
        windowGroup.blocksRaycasts = wantOpen;
        windowGroup.interactable = wantOpen;
        if (!wantOpen && windowAlpha <= 0f && window.activeSelf) window.SetActive(false);

        if (!window.activeSelf) return;

        // The tabs: the chosen one fades in, the others out, so the content changes smoothly.
        for (int i = 0; i < categories.Count; i++)
        {
            var category = categories[i];
            bool isSelected = i == selected;

            category.panelAlpha = Mathf.MoveTowards(category.panelAlpha, isSelected ? 1f : 0f, dt / panelFadeSeconds);
            category.iconAlpha = Mathf.MoveTowards(category.iconAlpha, isSelected ? activeIconAlpha : inactiveIconAlpha,
                Mathf.Abs(activeIconAlpha - inactiveIconAlpha) * dt / iconFadeSeconds);

            if (category.panel != null)
            {
                bool visible = isSelected || category.panelAlpha > 0f;
                if (category.panel.activeSelf != visible) category.panel.SetActive(visible);

                category.group.alpha = category.panelAlpha;
                category.group.blocksRaycasts = isSelected; // only the active tab takes touches
                category.group.interactable = isSelected;
            }
        }

        ApplyIconAlphas();
    }

    private void ApplyIconAlphas()
    {
        foreach (var category in categories)
        {
            if (category.icon == null) continue;
            Color color = category.icon.color;
            color.a = category.iconAlpha;
            category.icon.color = color;
        }
    }
}
