using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The damage counter of the battle scene: whenever a block is hurt, a number pops up in the middle of it (see DamageNumber
/// for how it moves). White for an ordinary hit, red for a critical one. The player can switch it off with a checkbox in the
/// main menu (DamageNumbersToggle); the choice is saved on the device.
///
/// Hits that come in very quick succession on the same block (a laser beam ticking every frame, a burst of bullets) are
/// added into ONE number per Merge Seconds, so the screen is not flooded. The number shows the health the block really lost.
///
/// SETUP: put one DamageNumbers on any object of the BATTLE scene and assign Number Prefab — your prefab with a DamageNumber
/// component and a text on it. Without this component in the scene nothing is shown.
///
/// CRITS: a hit rolled as critical (see CriticalHit) comes in through ShipModule.TakeDamage(amount, true) and the number turns red.
/// </summary>
public class DamageNumbers : MonoBehaviour
{
    private const string EnabledKey = "ui.damageNumbers";

    [Tooltip("The prefab of one number: a text (TextMeshPro, 3D or in a world-space canvas) plus a DamageNumber component.")]
    public DamageNumber numberPrefab;
    public Color normalColor = Color.white;
    public Color critColor = Color.red;
    [Tooltip("Only for a prefab whose text is a UI text (TextMeshProUGUI): how many world units one canvas unit is. Makes the " +
             "text bigger or smaller on the battlefield (font size 20 x 0.02 = about 0.4 world units tall).")]
    [Min(0.001f)] public float canvasUnitSize = 0.02f;
    [Tooltip("Hits on one block closer together than this many seconds make a single number.")]
    [Min(0f)] public float mergeSeconds = 0.15f;
    [Tooltip("Never more than this many numbers on screen at once; further ones are skipped until some vanish.")]
    [Min(1)] public int maxNumbers = 60;

    private struct Pending
    {
        public Vector3 position;
        public float amount;
        public bool crit;
        public float since;
    }

    private static DamageNumbers instance;
    private static int enabledCache = -1;

    private readonly Dictionary<ShipModule, Pending> pending = new();
    private readonly List<ShipModule> due = new();
    private int alive;

    /// <summary>Whether the counter is shown. Saved on the device; on by default.</summary>
    public static bool Enabled
    {
        get
        {
            if (enabledCache < 0) enabledCache = PlayerPrefs.GetInt(EnabledKey, 1);
            return enabledCache == 1;
        }
        set
        {
            enabledCache = value ? 1 : 0;
            PlayerPrefs.SetInt(EnabledKey, enabledCache);
            PlayerPrefs.Save();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        enabledCache = -1;
    }

    private void OnEnable() => instance = this;

    private void OnDisable()
    {
        if (instance == this) instance = null;
        pending.Clear();
    }

    /// <summary>Called by a block when it has lost health. `amount` is the health really lost.</summary>
    public static void Report(ShipModule block, float amount, bool crit)
    {
        if (instance == null || block == null || amount <= 0f || !Enabled) return;
        instance.Add(block, amount, crit);
    }

    private void Add(ShipModule block, float amount, bool crit)
    {
        Vector3 center = block.WorldBounds.center;
        center.z = block.transform.position.z;

        if (pending.TryGetValue(block, out var entry))
        {
            entry.amount += amount;
            entry.crit |= crit;
            entry.position = center;
            pending[block] = entry;
        }
        else
        {
            pending[block] = new Pending { position = center, amount = amount, crit = crit, since = Time.time };
        }
    }

    private void Update()
    {
        if (pending.Count == 0) return;

        due.Clear();
        foreach (var pair in pending)
            if (Time.time - pair.Value.since >= mergeSeconds) due.Add(pair.Key);

        foreach (var block in due)
        {
            var entry = pending[block];
            pending.Remove(block);
            Spawn(entry);
        }
    }

    private Transform canvasRoot;

    /// <summary>The world-space canvas that UI-text numbers live in. It is only a holder: one canvas unit is Canvas Unit Size
    /// world units, so a UI text of font size 20 comes out about a third of a world unit tall.</summary>
    private Transform GetCanvasRoot()
    {
        if (canvasRoot != null) return canvasRoot;

        var go = new GameObject("DamageNumbersCanvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.position = Vector3.zero;
        go.transform.localScale = Vector3.one * canvasUnitSize;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = numberPrefab != null ? numberPrefab.sortingOrder : 200;

        canvasRoot = go.transform;
        return canvasRoot;
    }

    private void Spawn(Pending entry)
    {
        if (numberPrefab == null || alive >= maxNumbers) return;

        int shown = Mathf.Max(1, Mathf.RoundToInt(entry.amount));

        // A UI text (TextMeshProUGUI) only draws inside a Canvas, so it is spawned into a world-space one made here.
        // A 3D text, or a prefab that brings its own Canvas, is spawned on its own.
        bool needsCanvas = numberPrefab.GetComponent<RectTransform>() != null && numberPrefab.GetComponentInChildren<Canvas>(true) == null;
        DamageNumber number;
        if (needsCanvas)
        {
            number = Instantiate(numberPrefab, entry.position, Quaternion.identity, GetCanvasRoot());
            number.gameObject.layer = canvasRoot.gameObject.layer; // the prefab sits on the UI layer, which the battle camera may not draw
        }
        else
        {
            number = Instantiate(numberPrefab, entry.position, Quaternion.identity);
        }

        alive++;
        number.OnFinished += () => alive--;
        number.Show(NumberFormat.Compact(shown), entry.crit ? critColor : normalColor, entry.crit);
    }
}
