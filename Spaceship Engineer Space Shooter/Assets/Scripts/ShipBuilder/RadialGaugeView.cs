using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one radial-fill UI gauge from a live current/max pair. Generic — one instance per stat
/// (energy, HP, shield, armor), each fed by BattleHudGauges.
///
/// LOOK: a ring (outline) that fills around the icon in its center. Fill Image is the ring sprite that
/// fills; the icon is just a separate Image in the middle of the gauge and needs no code. An optional
/// dim copy of the ring behind it (a separate Image, not touched here) shows the empty part.
///
/// PREFAB SETUP: Fill Image needs Image Type = Filled, Fill Method = Radial 360 — this only ever
/// touches its fillAmount.
/// </summary>
public class RadialGaugeView : MonoBehaviour
{
    public Image fillImage;
    [Tooltip("Off: the ring shows what is LEFT (full at 100%, empties as it's spent). " +
             "On: the ring shows what is USED (empty at 100%, fills up as it's spent).")]
    public bool showUsed;

    public void SetValue(float current, float max)
    {
        if (fillImage == null) return;

        float remaining = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        fillImage.fillAmount = showUsed ? 1f - remaining : remaining;
    }
}
