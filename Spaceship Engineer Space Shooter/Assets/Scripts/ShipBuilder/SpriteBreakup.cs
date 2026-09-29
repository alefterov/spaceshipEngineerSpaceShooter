using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Breaks a sprite into flying, fading fragments — shared by everything that shatters (meteors,
/// projectiles). Fragments are either hand-drawn chunks the caller supplies or the body's own sprite
/// automatically cut into a grid; see Spawn.
/// </summary>
public static class SpriteBreakup
{
    /// <summary>How the fragments move — bundled so meteors and projectiles can each expose their own values.</summary>
    public struct Motion
    {
        public float speedMin, speedMax;   // outward speed, world units per second
        public float inheritVelocity;      // 0..1 share of the source's own velocity they keep
        public float spinMax;              // degrees per second, either direction
        public float delay;                // seconds they hold still before flying
        public float fadeSeconds;          // seconds spent drifting and fading after the delay
    }

    /// <summary>
    /// Spawns the fragments of `body`'s sprite at its current position. With customSprites, one fragment
    /// per sprite starts at the body's center and flies off in a random direction. Without, the body's own
    /// sprite is cut into columns x rows chunks; each starts exactly where it sat in the whole and flies
    /// straight away from the center, so it reads as the thing cracking apart.
    /// </summary>
    public static void Spawn(SpriteRenderer body, Sprite[] customSprites, int columns, int rows,
                             Vector2 sourceVelocity, Motion motion)
    {
        if (body == null || body.sprite == null) return;

        Vector2 inherited = sourceVelocity * motion.inheritVelocity;
        Transform bodyTransform = body.transform;

        if (customSprites != null && customSprites.Length > 0)
        {
            foreach (var sprite in customSprites)
            {
                if (sprite == null) continue;
                SpawnOne(body, sprite, bodyTransform.position, Random.insideUnitCircle.normalized, inherited, motion);
            }
            return;
        }

        foreach (var (sprite, localOffset) in GetSlices(body.sprite, Mathf.Max(1, columns), Mathf.Max(1, rows)))
        {
            Vector3 worldPos = bodyTransform.TransformPoint(localOffset);
            Vector2 outward = (Vector2)(worldPos - bodyTransform.position);
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Random.insideUnitCircle.normalized;
            SpawnOne(body, sprite, worldPos, outward, inherited, motion);
        }
    }

    private static void SpawnOne(SpriteRenderer source, Sprite sprite, Vector3 worldPos, Vector2 outward, Vector2 inherited, Motion motion)
    {
        var obj = new GameObject("Fragment");
        obj.transform.SetPositionAndRotation(worldPos, source.transform.rotation);
        obj.transform.localScale = source.transform.lossyScale;

        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = source.sharedMaterial;
        sr.color = source.color;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder;

        float speedOut = Random.Range(Mathf.Min(motion.speedMin, motion.speedMax), Mathf.Max(motion.speedMin, motion.speedMax));
        float spin = Random.Range(-motion.spinMax, motion.spinMax);
        obj.AddComponent<MeteorDebris>().Begin(sr, inherited + outward * speedOut, spin, motion.delay, motion.fadeSeconds);
    }

    // Cutting a sprite into pieces is done once per sprite/grid and reused — Sprite.Create makes a new
    // asset-like object each call that Unity never frees on its own.
    private static readonly Dictionary<(Sprite, int, int), (Sprite sprite, Vector2 localOffset)[]> sliceCache = new();

    /// <summary>Cuts a sprite's texture rectangle into columns x rows chunks. localOffset is where each
    /// chunk's center sits relative to the ORIGINAL sprite's pivot, in that sprite's local units, so the
    /// pieces can be laid back exactly over the whole before flying apart.</summary>
    private static (Sprite sprite, Vector2 localOffset)[] GetSlices(Sprite source, int columns, int rows)
    {
        var key = (source, columns, rows);
        if (sliceCache.TryGetValue(key, out var cached) && cached.Length > 0 && cached[0].sprite != null) return cached;

        Rect rect = source.textureRect;
        float pieceW = rect.width / columns;
        float pieceH = rect.height / rows;
        var pieces = new (Sprite, Vector2)[columns * rows];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                var pieceRect = new Rect(rect.x + c * pieceW, rect.y + r * pieceH, pieceW, pieceH);
                // FullRect: the default Tight mesh would need a readable texture just to trace its outline.
                var piece = Sprite.Create(source.texture, pieceRect, new Vector2(0.5f, 0.5f),
                                          source.pixelsPerUnit, 0, SpriteMeshType.FullRect);

                Vector2 centerPx = new((c + 0.5f) * pieceW, (r + 0.5f) * pieceH);
                pieces[r * columns + c] = (piece, (centerPx - source.pivot) / source.pixelsPerUnit);
            }
        }

        sliceCache[key] = pieces;
        return pieces;
    }
}
