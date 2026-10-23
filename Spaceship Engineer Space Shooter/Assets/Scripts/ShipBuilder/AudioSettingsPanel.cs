using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The sound part of the settings menu: two sliders, one for the music and one for the effects. Put it on the settings
/// panel, assign the sliders (their range should be 0 to 1); the volumes are saved on the device and used the next time too.
/// </summary>
public class AudioSettingsPanel : MonoBehaviour
{
    public Slider musicSlider;
    public Slider sfxSlider;

    private void OnEnable()
    {
        var manager = AudioManager.Instance;
        if (manager == null) return;

        // Show the current values without that counting as the player moving the slider.
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
