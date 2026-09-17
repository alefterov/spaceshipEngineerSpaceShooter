using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One button on the campaign level-select map. Shows the level number, a lock icon + darkened
/// button while locked, and a 3-star rating row once unlocked (empty stars if not completed yet).
/// Lay these out and connect them with lines by hand — this component only handles one button's own
/// state, nothing about the map layout.
/// </summary>
public class LevelButtonView : MonoBehaviour
{
    [Header("Data")]
    public LevelDefinition level;

    [Header("UI (assign whichever exist on this prefab)")]
    public Button button;
    public TMP_Text numberLabel;
    [Tooltip("Shown only while this level is locked. The button itself also darkens automatically via " +
             "its own Disabled Color, since Button.interactable is set to false while locked.")]
    public GameObject lockIcon;
    [Tooltip("Parent of the 3 star images — hidden entirely while locked (nothing to rate yet).")]
    public GameObject starRow;
    [Tooltip("Exactly 3, left to right.")]
    public Image[] stars = new Image[3];
    public Sprite filledStarSprite;
    public Sprite emptyStarSprite;

    /// <summary>Fired when a player taps an unlocked level. No battle scene exists yet — hook this up
    /// once one does, e.g. LevelButtonView.OnLevelSelected += level => SceneManager.LoadScene(...).</summary>
    public static event Action<LevelDefinition> OnLevelSelected;

    private bool subscribed;

    private void Awake()
    {
        // Same defensive Z-reset as TechNodeView — see that file's Awake for why this matters.
        var rt = (RectTransform)transform;
        var pos = rt.localPosition;
        if (pos.z != 0f) rt.localPosition = new Vector3(pos.x, pos.y, 0f);
    }

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(OnClicked);
        TrySubscribe();
    }

    // Same Start()-fallback pattern as TechNodeView/CurrencyDisplay — GameDataManager.Instance may
    // not be set yet during this object's own OnEnable if Awake order puts it first.
    private void Start() => TrySubscribe();

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnClicked);
        Unsubscribe();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        var data = GameDataManager.Instance;
        if (data == null) return;

        data.OnLevelStarsChanged += HandleLevelStarsChanged;
        subscribed = true;
        Refresh();
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        var data = GameDataManager.Instance;
        if (data != null) data.OnLevelStarsChanged -= HandleLevelStarsChanged;
    }

    // Refreshes on ANY level's stars changing, not just this one's — completing the PREREQUISITE is
    // exactly what might flip this button from locked to unlocked.
    private void HandleLevelStarsChanged(string changedLevelId) => Refresh();

    public void Refresh()
    {
        if (level == null) return;
        var data = GameDataManager.Instance;
        if (data == null) return;

        bool unlocked = data.IsLevelUnlocked(level);
        int earnedStars = data.GetStars(level);

        if (numberLabel != null) numberLabel.text = level.displayNumber.ToString();
        if (lockIcon != null) lockIcon.SetActive(!unlocked);
        if (button != null) button.interactable = unlocked;
        if (starRow != null) starRow.SetActive(unlocked);

        if (!unlocked) return; // locked — no rating to show at all

        for (int i = 0; i < stars.Length; i++)
            if (stars[i] != null) stars[i].sprite = i < earnedStars ? filledStarSprite : emptyStarSprite;
    }

    private void OnClicked()
    {
        PendingBattle.Level = level;
        Debug.Log($"TODO: load the battle scene for level '{level.id}' — e.g. SceneManager.LoadScene(\"Battle\")");
        OnLevelSelected?.Invoke(level);
    }
}
