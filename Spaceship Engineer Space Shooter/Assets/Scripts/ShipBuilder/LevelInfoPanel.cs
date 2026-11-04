using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelInfoPanel : MonoBehaviour
{
    [Header("UI (assign whichever exist)")]
    [Tooltip("Shows the level number, e.g. \"Уровень 3\".")]
    public TMP_Text levelLabel;
    public string levelFormat = "Уровень {0}";
    [Tooltip("Shows the number of waves, e.g. \"Волн: 5\".")]
    public TMP_Text wavesLabel;
    public string wavesFormat = "Волн: {0}";
    [Tooltip("Starts the level. Disabled while the level is locked.")]
    public Button playButton;
    [Tooltip("Optional object (a note, a padlock) shown only when the level is locked.")]
    public GameObject lockedNote;

    [Header("Difficulty")]
    [Tooltip("Shows the difficulty, e.g. \"Сложность: Средне\". Coloured by the tier.")]
    public TMP_Text difficultyLabel;
    public string difficultyFormat = "Сложность: {0}";
    [Tooltip("Optional: the number behind it — enemy power divided by yours, e.g. \"x1.4\".")]
    public TMP_Text ratioLabel;
    public string ratioFormat = "x{0:0.0}";
    [Tooltip("Optional: an Image that shows the difficulty as a picture — one of the three sprites below. Its colour is not changed.")]
    public Image difficultyImage;
    public Sprite easySprite;
    public Sprite mediumSprite;
    public Sprite hardSprite;
    public string easyText = "Легко";
    public string mediumText = "Средне";
    public string hardText = "Сложно";
    public Color easyColor = new(0.4f, 0.9f, 0.4f);
    public Color mediumColor = new(1f, 0.85f, 0.3f);
    public Color hardColor = new(1f, 0.4f, 0.35f);
    [Tooltip("The ratio from which a level counts as MEDIUM (below it: easy).")]
    public float mediumFrom = 1f;
    [Tooltip("The ratio from which a level counts as HARD.")]
    public float hardFrom = 2f;
    [Tooltip("Used to read the player's ship from the saved layout when it is not on screen. Found from the main menu " +
             "controller if left empty.")]
    public BlockDatabase blockDatabase;

    private LevelDefinition level;
    private bool canPlay;
    private bool shown;

    private void OnEnable()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);

        shown = false;
        ShowDefaultLevel();
    }

    private void Start()
    {
        if (!shown) ShowDefaultLevel();
    }

    private void OnDisable()
    {
        if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);
    }

    private void ShowDefaultLevel()
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        var levels = FindObjectsByType<LevelButtonView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Select(b => b.level)
            .Where(l => l != null)
            .Distinct()
            .OrderBy(l => l.displayNumber)
            .ToList();
        if (levels.Count == 0) return;

        var pick = levels.FirstOrDefault(l => !data.IsLevelCompleted(l)) ?? levels[levels.Count - 1];
        Open(pick, data.IsLevelUnlocked(pick));
    }

    public void Open(LevelDefinition levelToShow, bool unlocked)
    {
        level = levelToShow;
        canPlay = unlocked;
        shown = true;

        if (levelLabel != null && level != null) levelLabel.text = string.Format(levelFormat, level.displayNumber);
        if (wavesLabel != null && level != null) wavesLabel.text = string.Format(wavesFormat, level.waves != null ? level.waves.Count : 0);

        if (playButton != null) playButton.interactable = canPlay;
        if (lockedNote != null) lockedNote.SetActive(!canPlay);

        ShowDifficulty();
    }

    private void OnPlayClicked()
    {
        if (level == null || !canPlay) return;
        LevelButtonView.StartLevel(level);
    }

    private void ShowDifficulty()
    {
        if (difficultyLabel == null && ratioLabel == null && difficultyImage == null) return;

        var result = LevelDifficulty.Calculate(level, GetPlayerBlocks(), mediumFrom, hardFrom);

        string name = result.tier switch
        {
            DifficultyTier.Hard => hardText,
            DifficultyTier.Medium => mediumText,
            _ => easyText,
        };
        Color color = result.tier switch
        {
            DifficultyTier.Hard => hardColor,
            DifficultyTier.Medium => mediumColor,
            _ => easyColor,
        };

        if (difficultyLabel != null)
        {
            difficultyLabel.text = string.Format(difficultyFormat, name);
            difficultyLabel.color = color;
        }

        if (difficultyImage != null)
        {
            difficultyImage.sprite = result.tier switch
            {
                DifficultyTier.Hard => hardSprite,
                DifficultyTier.Medium => mediumSprite,
                _ => easySprite,
            };
            difficultyImage.enabled = difficultyImage.sprite != null;
        }

        if (ratioLabel != null)
        {
            ratioLabel.text = float.IsInfinity(result.ratio) ? "x∞" : string.Format(ratioFormat, result.ratio);
            ratioLabel.color = color;
        }
    }

    private System.Collections.Generic.IEnumerable<ShipModule> GetPlayerBlocks()
    {
        var menu = FindFirstObjectByType<MainMenuFlowController>(FindObjectsInactive.Include);

        if (menu != null && menu.playerShip != null)
        {
            var live = menu.playerShip.Blocks.Where(b => b != null && !b.IsDestroyed).ToList();
            if (live.Count > 0) return live;
        }

        var data = GameDataManager.Instance;
        var db = blockDatabase != null ? blockDatabase : menu != null ? menu.database : null;
        if (data == null || db == null || data.Current == null || data.Fleet.Count == 0) return System.Array.Empty<ShipModule>();

        var layout = data.SelectedShip.layout;
        return layout.hull.Concat(layout.modules).Concat(layout.blocks)
            .Select(entry => db.GetById(entry.blockId))
            .Where(def => def != null && def.prefab != null)
            .Select(def => def.prefab.GetComponent<ShipModule>())
            .Where(m => m != null)
            .ToList();
    }
}
