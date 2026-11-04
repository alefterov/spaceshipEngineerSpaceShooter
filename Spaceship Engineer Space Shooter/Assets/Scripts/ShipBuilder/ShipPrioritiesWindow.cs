using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        foreach (var category in categories) category.content?.RefreshPanel();

        if (!paused && Time.timeScale > 0f)
        {
            Time.timeScale = 0f;
            paused = true;
        }
    }

    public void Close()
    {
        if (!wantOpen) return;
        wantOpen = false;
        Resume();
    }

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
        Time.timeScale = GameSpeed.SceneScale;
        paused = false;
    }

    private void Update()
    {
        if (window == null) return;

        float dt = Time.unscaledDeltaTime;

        windowAlpha = Mathf.MoveTowards(windowAlpha, wantOpen ? 1f : 0f, dt / windowFadeSeconds);
        windowGroup.alpha = windowAlpha;
        windowGroup.blocksRaycasts = wantOpen;
        windowGroup.interactable = wantOpen;
        if (!wantOpen && windowAlpha <= 0f && window.activeSelf) window.SetActive(false);

        if (!window.activeSelf) return;

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
                category.group.blocksRaycasts = isSelected;
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
