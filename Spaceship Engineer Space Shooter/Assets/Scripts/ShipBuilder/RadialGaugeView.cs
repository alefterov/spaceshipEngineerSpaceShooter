using UnityEngine;
using UnityEngine.UI;

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
