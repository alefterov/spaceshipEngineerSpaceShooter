using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The info / confirmation panel for a level: pressing a level on the map (LevelButtonView) opens this instead of
/// starting the battle. It shows the level's number, how many waves it has and how hard it is for the player's
/// current ship (see LevelDifficulty), and the battle begins only when Play is pressed. A level that is still LOCKED
/// can be opened too — to look at it — but its Play button is disabled.
///
/// SETUP: put on the panel's root (it starts INACTIVE; level buttons find it by themselves, or you can assign it in
/// their Info Panel field). Assign Play Button; everything else is optional. Each label keeps its text format in a
/// field here ({0} is replaced by the value).
/// </summary>
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
    [Tooltip("Optional: hides the panel without starting anything.")]
    public Button closeButton;
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

    private void OnEnable()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        if (playButton != null) playButton.onClick.RemoveListener(OnPlayClicked);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    /// <summary>Shows the panel for this level. `unlocked` = false still shows everything, but Play is disabled.</summary>
    public void Open(LevelDefinition levelToShow, bool unlocked)
    {
        level = levelToShow;
        canPlay = unlocked;

        if (levelLabel != null && level != null) levelLabel.text = string.Format(levelFormat, level.displayNumber);
        if (wavesLabel != null && level != null) wavesLabel.text = string.Format(wavesFormat, level.waves != null ? level.waves.Count : 0);

        if (playButton != null) playButton.interactable = canPlay;
        if (lockedNote != null) lockedNote.SetActive(!canPlay);

        ShowDifficulty();

        gameObject.SetActive(true);
    }

    public void Close() => gameObject.SetActive(false);

    private void OnPlayClicked()
    {
        if (level == null || !canPlay) return;

        var chosen = level;
        Close();
        LevelButtonView.StartLevel(chosen);
    }

    // ---------- Difficulty ----------

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

    /// <summary>The blocks of the player's ship: the ship on screen in the main menu if there is one, otherwise the
    /// saved layout (blocks taken from their prefabs).</summary>
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
        if (data == null || db == null || data.Current.playerShip == null) return System.Array.Empty<ShipModule>();

        var layout = data.Current.playerShip;
        return layout.hull.Concat(layout.modules).Concat(layout.blocks)
            .Select(entry => db.GetById(entry.blockId))
            .Where(def => def != null && def.prefab != null)
            .Select(def => def.prefab.GetComponent<ShipModule>())
            .Where(m => m != null)
            .ToList();
    }
}
