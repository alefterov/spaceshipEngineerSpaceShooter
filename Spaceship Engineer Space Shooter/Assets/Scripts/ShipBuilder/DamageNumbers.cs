using System.Collections.Generic;
using UnityEngine;

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
        EffectSorting.ApplyToCanvas(canvas);

        canvasRoot = go.transform;
        return canvasRoot;
    }

    private void Spawn(Pending entry)
    {
        if (numberPrefab == null || alive >= maxNumbers) return;

        int shown = Mathf.Max(1, Mathf.RoundToInt(entry.amount));

        bool needsCanvas = numberPrefab.GetComponent<RectTransform>() != null && numberPrefab.GetComponentInChildren<Canvas>(true) == null;
        DamageNumber number;
        if (needsCanvas)
        {
            number = Instantiate(numberPrefab, entry.position, Quaternion.identity, GetCanvasRoot());
            number.gameObject.layer = canvasRoot.gameObject.layer;
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
