using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsPanel : MonoBehaviour
{
    public Slider musicSlider;
    public Slider sfxSlider;

    private void OnEnable()
    {
        var manager = AudioManager.Instance;
        if (manager == null) return;

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(manager.MusicVolume);
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(manager.SfxVolume);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        }
    }

    private void OnDisable()
    {
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
    }

    private void OnMusicChanged(float value) => AudioManager.Instance?.SetMusicVolume(value);
    private void OnSfxChanged(float value) => AudioManager.Instance?.SetSfxVolume(value);
}
