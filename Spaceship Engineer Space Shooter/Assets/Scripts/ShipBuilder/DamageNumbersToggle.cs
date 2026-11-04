using UnityEngine;
using UnityEngine.UI;

public class DamageNumbersToggle : MonoBehaviour
{
    public Toggle toggle;

    private void OnEnable()
    {
        if (toggle == null) return;

        toggle.SetIsOnWithoutNotify(DamageNumbers.Enabled);
        toggle.onValueChanged.AddListener(OnChanged);
    }

    private void OnDisable()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(bool value) => DamageNumbers.Enabled = value;
}
