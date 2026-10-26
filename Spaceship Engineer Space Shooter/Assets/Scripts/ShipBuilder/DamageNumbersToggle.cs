using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "show damage numbers" checkbox of the settings menu. Put it on the settings panel (next to AudioSettingsPanel, say)
/// and assign the Toggle. It shows the saved choice when the panel opens and saves a new one at once; the battle scene reads
/// it through DamageNumbers.Enabled.
/// </summary>
public class DamageNumbersToggle : MonoBehaviour
{
    public Toggle toggle;

    private void OnEnable()
    {
        if (toggle == null) return;

        toggle.SetIsOnWithoutNotify(DamageNumbers.Enabled); // showing the saved value is not the player changing it
        toggle.onValueChanged.AddListener(OnChanged);
    }

    private void OnDisable()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(bool value) => DamageNumbers.Enabled = value;
}
