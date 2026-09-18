using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives one radial-fill UI gauge from a live current/max pair. Generic — one instance per stat
/// (energy, HP, shield, armor), each fed by BattleHudGauges.
///
/// PREFAB SETUP: Fill Image needs Image Type = Filled, Fill Method = Radial 360 — this only ever
/// touches its fillAmount.
/// </summary>
public class RadialGaugeView : MonoBehaviour
{
    public Image fillImage;

    public void SetValue(float current, float max)
    {
        if (fillImage != null) fillImage.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }
}
