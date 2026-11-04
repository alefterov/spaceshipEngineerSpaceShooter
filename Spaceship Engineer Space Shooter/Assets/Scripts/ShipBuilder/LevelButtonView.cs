using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [Tooltip("The button stays clickable while locked (the info panel opens, only Play is disabled), so it is darkened by " +
             "this colour instead of by Disabled Color. White = no darkening.")]
    public Color lockedTint = new(0.5f, 0.5f, 0.5f, 1f);
    [Tooltip("Parent of the 3 star images — hidden entirely while locked (nothing to rate yet).")]
    public GameObject starRow;
    [Tooltip("Exactly 3, left to right.")]
    public Image[] stars = new Image[3];
    public Sprite filledStarSprite;
    public Sprite emptyStarSprite;

    [Header("Confirmation")]
    [Tooltip("The level info panel opened when this button is pressed; the battle starts only from its Play button. " +
             "Found in the scene automatically if left empty. With no panel at all the level starts at once, as before.")]
    public LevelInfoPanel infoPanel;

    [Header("Path to the next level")]
    [Tooltip("The picture of the path from this level to the next one. Fully visible once THIS level is completed, " +
             "faint until then. Hidden entirely on the last level of a campaign (no Next Level set on its LevelDefinition).")]
    public Image pathToNext;
    [Range(0f, 1f), Tooltip("Alpha of the path once this level has been completed.")]
    public float completedPathAlpha = 1f;
    [Range(0f, 1f), Tooltip("Alpha of the path while this level has not been completed yet.")]
    public float uncompletedPathAlpha = 0.2f;

    public static event Action<LevelDefinition> OnLevelSelected;

    private bool subscribed;

    private void Awake()
    {
        var rt = (RectTransform)transform;
        var pos = rt.localPosition;
        if (pos.z != 0f) rt.localPosition = new Vector3(pos.x, pos.y, 0f);
    }

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(OnClicked);
        TrySubscribe();
    }

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
        if (button != null)
        {
            button.interactable = true;
            if (button.targetGraphic != null) button.targetGraphic.color = unlocked ? Color.white : lockedTint;
        }
        if (starRow != null) starRow.SetActive(unlocked);

        RefreshPath(earnedStars > 0);

        if (!unlocked) return;

        for (int i = 0; i < stars.Length; i++)
            if (stars[i] != null) stars[i].sprite = i < earnedStars ? filledStarSprite : emptyStarSprite;
    }

    private void RefreshPath(bool completed)
    {
        if (pathToNext == null) return;

        bool hasNext = level.nextLevel != null;
        pathToNext.gameObject.SetActive(hasNext);
        if (!hasNext) return;

        var color = pathToNext.color;
        color.a = completed ? completedPathAlpha : uncompletedPathAlpha;
        pathToNext.color = color;
    }

    private void OnClicked()
    {
        if (infoPanel == null) infoPanel = FindFirstObjectByType<LevelInfoPanel>(FindObjectsInactive.Include);

        if (infoPanel != null) infoPanel.Open(level, GameDataManager.Instance != null && GameDataManager.Instance.IsLevelUnlocked(level));
        else if (GameDataManager.Instance != null && GameDataManager.Instance.IsLevelUnlocked(level)) StartLevel(level);
    }

    public static void StartLevel(LevelDefinition chosen)
    {
        PendingBattle.Level = chosen;
        SceneLoader.Load(PendingBattle.BattleSceneName);
        OnLevelSelected?.Invoke(chosen);
    }
}
