using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shows what an enemy ship prefab looks like. An EnemyShip prefab is an empty object — it builds its blocks only when the
/// game runs — so this gives it two editor-only views, neither of which adds anything to the prefab or the scene:
///
///  - INSPECTOR PREVIEW: below the component, a picture of the ship made from the real block prefabs (sprites, turrets
///    and all), drawn in a hidden preview scene. It updates by itself when the Blocks list changes. Works with the prefab
///    selected in the Project window, too.
///  - SCENE VIEW OUTLINE: when the ship is selected (also in Prefab Mode), every block is outlined in a colour of its
///    kind with its name, so the layout can be read in the Scene view: armor grey, weapon red, engine orange, generator
///    yellow, shield blue, cockpit green, repair lime. The arrow marks the ship's front (the side that fires).
///
/// The ship is built facing UP (like the player's); in the game it turns to face down. "Show as in battle" in the
/// preview turns the picture the same way.
/// </summary>
[CustomEditor(typeof(EnemyShip))]
public class EnemyShipEditor : Editor
{
    private const float PreviewHeight = 300f;

    private PreviewRenderUtility preview;
    private readonly List<GameObject> spawned = new();
    private readonly List<Material> materials = new();
    private Bounds bounds;
    private int builtHash = int.MinValue;
    private bool builtFlipped;
    private bool open = true;
    private bool asInBattle = true;
    private float zoom = 1f;

    private void OnDisable() => Cleanup();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        open = EditorGUILayout.Foldout(open, "Ship preview", true);
        if (!open) return;

        var ship = (EnemyShip)target;

        if (Application.isPlaying) // the block prefabs would run their game code inside the preview scene
        {
            EditorGUILayout.HelpBox("The ship preview is shown outside Play Mode.", MessageType.None);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        asInBattle = EditorGUILayout.ToggleLeft("Show as in battle (facing down)", asInBattle);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("Zoom", GUILayout.Width(40));
        zoom = GUILayout.HorizontalSlider(zoom, 0.5f, 2f, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        Rect rect = GUILayoutUtility.GetRect(10f, PreviewHeight, GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint) return;

        bool flipped = asInBattle && ship.faceDown;
        int hash = ComputeHash(ship);
        if (hash != builtHash || flipped != builtFlipped) Rebuild(ship, flipped, hash);

        if (spawned.Count == 0)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.13f, 0.16f));
            GUI.Label(rect, "No blocks to show", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        float aspect = rect.width / rect.height;
        float half = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * 1.25f / zoom;
        preview.camera.orthographicSize = Mathf.Max(0.5f, half);
        preview.camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);

        preview.BeginPreview(rect, GUIStyle.none);
        preview.camera.Render();
        Texture picture = preview.EndPreview();
        GUI.DrawTexture(rect, picture, ScaleMode.StretchToFill, false);
    }

    // ---------- Building the picture ----------

    private static int ComputeHash(EnemyShip ship)
    {
        int hash = 17;
        var grid = ship.GetComponent<ShipGrid>();
        if (grid != null) hash = hash * 31 + grid.width * 1000 + grid.height + grid.HangarColumns * 7 + grid.HangarRows * 13 + (int)(grid.cellSize * 100f);

        foreach (var placement in ship.blocks)
        {
            hash = hash * 31 + (placement.block != null ? placement.block.GetHashCode() : 0);
            hash = hash * 31 + placement.position.x * 97 + placement.position.y;
            hash = hash * 31 + placement.rotationSteps;
        }

        return hash;
    }

    private void Rebuild(EnemyShip ship, bool flipped, int hash)
    {
        Cleanup();
        builtHash = hash;
        builtFlipped = flipped;

        var grid = ship.GetComponent<ShipGrid>();
        int width = grid != null ? grid.GridWidth : 12, height = grid != null ? grid.GridHeight : 12;
        float cell = grid != null ? grid.cellSize : 1f;

        preview = new PreviewRenderUtility();
        preview.camera.orthographic = true;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
        preview.camera.nearClipPlane = 0.1f;
        preview.camera.farClipPlane = 50f;
        preview.camera.transform.rotation = Quaternion.identity;

        var unlit = Shader.Find("Sprites/Default"); // the sprites normally use a lit 2D material, which is black without lights
        bool any = false;

        foreach (var placement in ship.blocks)
        {
            var def = placement.block;
            if (def == null || def.prefab == null) continue;

            var rotatedCells = BlockDefinition.RotateCells(def.cells, placement.rotationSteps);
            var (centroid, _) = ShipGrid.ComputeLocalFootprint(BlockDefinition.Offsets(rotatedCells));

            var go = Instantiate(def.prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            preview.AddSingleGO(go);

            float x = (placement.position.x - width * 0.5f + 0.5f) * cell;
            float y = (placement.position.y - height * 0.5f + 0.5f) * cell;
            Vector3 position = new(x, y, 0f);
            Quaternion turn = flipped ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
            go.transform.SetPositionAndRotation(turn * position, turn);

            // The same placement the game does: only the visual child is moved to the middle of the block's footprint
            // and turned by the block's rotation.
            if (go.TryGetComponent<ShipModule>(out var module) && module.visualRoot != null && module.visualRoot != go.transform)
            {
                module.visualRoot.localPosition = new Vector3(centroid.x * cell, centroid.y * cell, 0f);
                module.visualRoot.localRotation = Quaternion.Euler(0f, 0f, 90f * placement.rotationSteps);
            }

            foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (unlit != null)
                {
                    var material = new Material(unlit) { hideFlags = HideFlags.HideAndDontSave };
                    renderer.sharedMaterial = material;
                    materials.Add(material);
                }
            }

            foreach (var behaviour in go.GetComponentsInChildren<Behaviour>(true))
                if (behaviour is Animator animator) animator.enabled = false;

            spawned.Add(go);
            any = true;
        }

        if (!any) return;

        bool first = true;
        foreach (var go in spawned)
        {
            foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null || !renderer.enabled) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }

        if (first) // nothing drew anything: frame the cells instead
            bounds = new Bounds(Vector3.zero, Vector3.one * 3f);
    }

    private void Cleanup()
    {
        foreach (var go in spawned) if (go != null) DestroyImmediate(go);
        spawned.Clear();
        foreach (var material in materials) if (material != null) DestroyImmediate(material);
        materials.Clear();

        if (preview != null)
        {
            preview.Cleanup();
            preview = null;
        }

        builtHash = int.MinValue;
    }

    // ---------- Scene view outline ----------

    private void OnSceneGUI()
    {
        var ship = (EnemyShip)target;
        var grid = ship.GetComponent<ShipGrid>();
        int width = grid != null ? grid.GridWidth : 12, height = grid != null ? grid.GridHeight : 12;
        float cell = grid != null ? grid.cellSize : 1f;

        var previousMatrix = Handles.matrix;
        Handles.matrix = ship.transform.localToWorldMatrix;

        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        bool any = false;

        foreach (var placement in ship.blocks)
        {
            var def = placement.block;
            if (def == null) continue;

            Color color = KindColor(def.category);
            var rotatedCells = BlockDefinition.RotateCells(def.cells, placement.rotationSteps);
            Vector3 labelCenter = Vector3.zero;

            foreach (var blockCell in rotatedCells)
            {
                Vector2Int c = placement.position + blockCell.offset;
                Vector3 center = new((c.x - width * 0.5f + 0.5f) * cell, (c.y - height * 0.5f + 0.5f) * cell, 0f);
                labelCenter += center;

                min = Vector3.Min(min, center - Vector3.one * cell * 0.5f);
                max = Vector3.Max(max, center + Vector3.one * cell * 0.5f);
                any = true;

                Handles.DrawSolidRectangleWithOutline(
                    new[]
                    {
                        center + new Vector3(-0.47f, -0.47f) * cell, center + new Vector3(-0.47f, 0.47f) * cell,
                        center + new Vector3(0.47f, 0.47f) * cell, center + new Vector3(0.47f, -0.47f) * cell,
                    },
                    new Color(color.r, color.g, color.b, 0.28f), color);
            }

            if (rotatedCells.Count > 0)
                Handles.Label(labelCenter / rotatedCells.Count + new Vector3(-0.4f, 0.15f) * cell, ShortName(def));
        }

        if (any)
        {
            // An arrow out of the front (the +Y side), where the weapons point.
            Vector3 tip = new((min.x + max.x) * 0.5f, max.y + cell * 0.9f, 0f);
            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            Handles.DrawLine(new Vector3(tip.x, max.y + cell * 0.1f), tip);
            Handles.DrawLine(tip, tip + new Vector3(-0.25f, -0.3f) * cell);
            Handles.DrawLine(tip, tip + new Vector3(0.25f, -0.3f) * cell);
        }

        Handles.matrix = previousMatrix;
    }

    private static string ShortName(BlockDefinition def)
    {
        string name = !string.IsNullOrEmpty(def.displayName) ? def.displayName : def.name;
        return name.Length > 14 ? name.Substring(0, 14) : name;
    }

    private static Color KindColor(BlockCategory category) => category switch
    {
        BlockCategory.Armor => new Color(0.7f, 0.7f, 0.75f),
        BlockCategory.Weapon => new Color(1f, 0.3f, 0.3f),
        BlockCategory.Engine => new Color(1f, 0.6f, 0.15f),
        BlockCategory.Generator => new Color(1f, 0.9f, 0.2f),
        BlockCategory.Shield => new Color(0.3f, 0.7f, 1f),
        BlockCategory.Cockpit => new Color(0.3f, 1f, 0.4f),
        BlockCategory.Repair => new Color(0.7f, 1f, 0.2f),
        _ => Color.white,
    };
}
