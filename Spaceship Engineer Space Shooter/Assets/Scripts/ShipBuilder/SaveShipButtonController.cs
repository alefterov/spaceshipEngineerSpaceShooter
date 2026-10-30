using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The save button of the ship builder. Three looks:
///  - OFF (still shown, greyed) while the ship cannot be saved: no cockpit, or some blocks are not joined to the rest (they would break off
///    in battle).
///  - HIGHLIGHTED while the ship can be saved and differs from the saved one — placed, removed or rotated a block since the last save — so the
///    player sees there is something to save. The highlight tints the button and its icon (and pulses, if Pulse Speed is above 0) and can
///    switch on an extra object (a glow, a dot).
///  - NORMAL when there is nothing new to save.
///
/// SETUP: put on the save button itself (the object with the Button that calls MainMenuFlowController.OnSaveShipPressed). Everything else is
/// found by itself: the ship grid from the MainMenuFlowController, the icon as the button's first child Image.
/// </summary>
public class SaveShipButtonController : MonoBehaviour
{
    [Tooltip("The save button. Empty = the Button on this object (or the nearest parent Button that has an On Click).")]
    public Button button;
    [Tooltip("The ship being built. Empty = MainMenuFlowController's Player Ship.")]
    public ShipGrid grid;
    [Tooltip("The picture on the button. Empty = the button's first child Image.")]
    public Graphic icon;
    [Tooltip("Optional: a SEPARATE object (a glow around the button...) shown only while there is something to save. Not the button or its icon.")]
    public GameObject unsavedHighlight;

    [Header("Look")]
    [Tooltip("Tint of the button and its icon while there is something to save.")]
    public Color unsavedColor = new(1f, 0.92f, 0.45f);
    [Tooltip("How fast the tint pulses between the normal colour and Unsaved Color, in cycles per second. 0 = a steady tint.")]
    [Min(0f)] public float pulseSpeed = 1.5f;
    [Tooltip("Tint of the icon while the button is off.")]
    public Color disabledIconColor = new(0.78f, 0.78f, 0.78f, 0.5f);

    private Color backgroundColor = Color.white;
    private Color iconColor = Color.white;
    private bool dirty = true;
    private bool canSave;
    private bool hasChanges;

    private void Awake()
    {
        if (button == null) button = FindButton();
        if (button == null)
        {
            Debug.LogWarning("SaveShipButtonController: no Button found - put it on the save button.", this);
            enabled = false;
            return;
        }

        if (button.image != null) backgroundColor = button.image.color;

        // A Button left on the icon (or another child) catches the taps on it, and they never reach the save button: switch those off.
        foreach (var inner in button.GetComponentsInChildren<Button>(true))
        {
            if (inner == button || inner.onClick.GetPersistentEventCount() > 0) continue;
            inner.enabled = false;
            Debug.LogWarning($"SaveShipButtonController: switched off the extra Button on '{inner.name}' - it took the taps meant for saving. Remove it from that object.", inner);
        }

        if (icon == null)
        {
            foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.gameObject == button.gameObject) continue;
                icon = graphic;
                break;
            }
        }

        if (icon != null) iconColor = icon.color;

        // Switching off the button, its icon or this object would hide the button itself instead of a glow.
        if (unsavedHighlight != null && (button.transform.IsChildOf(unsavedHighlight.transform) || transform.IsChildOf(unsavedHighlight.transform)
                                         || (icon != null && icon.gameObject == unsavedHighlight)))
        {
            Debug.LogWarning("SaveShipButtonController: Unsaved Highlight must be a separate glow object, not the button or its icon - ignored.", this);
            unsavedHighlight = null;
        }
    }

    /// <summary>The Button here; if this sits on a child of the real save button (its icon), the parent Button that actually saves.</summary>
    private Button FindButton()
    {
        var buttons = GetComponentsInParent<Button>(true);
        foreach (var candidate in buttons)
        {
            if (candidate.onClick.GetPersistentEventCount() > 0) return candidate;
        }

        return buttons.Length > 0 ? buttons[0] : null;
    }

    private void OnEnable()
    {
        if (button == null) return;

        if (grid == null)
        {
            var flow = FindFirstObjectByType<MainMenuFlowController>(FindObjectsInactive.Include);
            if (flow != null) grid = flow.playerShip;
        }

        if (grid != null) grid.OnShipChanged += MarkDirty;
        button.onClick.AddListener(OnPressed);
        dirty = true;
    }

    private void OnDisable()
    {
        if (grid != null) grid.OnShipChanged -= MarkDirty;
        if (button != null) button.onClick.RemoveListener(OnPressed);
    }

    private void MarkDirty() => dirty = true;

    // The press itself saves (MainMenuFlowController); look again afterwards.
    private void OnPressed() => dirty = true;

    private void Update()
    {
        if (grid == null) return;

        if (dirty)
        {
            dirty = false;
            canSave = grid.CanBeSaved;
            hasChanges = DiffersFromSaved();
            button.interactable = canSave;
            if (unsavedHighlight != null) unsavedHighlight.SetActive(canSave && hasChanges);
        }

        ApplyLook();
    }

    private void ApplyLook()
    {
        float t = hasChanges && canSave
            ? (pulseSpeed > 0f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) : 1f)
            : 0f;

        if (button.image != null) button.image.color = Color.Lerp(backgroundColor, unsavedColor, t);
        if (icon != null) icon.color = !canSave ? disabledIconColor : Color.Lerp(iconColor, unsavedColor, t);
    }

    /// <summary>Whether the ship on the grid is not the one in the save: a block was added, removed, moved or rotated.</summary>
    private bool DiffersFromSaved()
    {
        var data = GameDataManager.Instance;
        var saved = data != null && data.Current != null ? data.Current.playerShip : null;

        var now = Describe(grid.ExportLayout());
        var then = Describe(saved);
        return !now.SequenceEqual(then);
    }

    private static string[] Describe(ShipLayout layout)
    {
        if (layout == null) return System.Array.Empty<string>();
        return layout.blocks.Select(b => $"{b.blockId}@{b.anchorX},{b.anchorY}r{b.rotationSteps}").OrderBy(s => s, System.StringComparer.Ordinal).ToArray();
    }
}
