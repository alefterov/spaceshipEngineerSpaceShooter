using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The battle speed setting: a slider that steps through Speeds (0.5x, 1x, 2x by default) and saves the choice (GameSpeed). The battle
/// then runs at that speed; the menus always run normally.
///
/// SETUP: put on the slider (TimeSlider) or anywhere and assign it. Its range is set by itself: whole numbers from 0 to the number of
/// Speeds - 1, one step per speed. Optional Value Label shows the chosen speed ("x2").
/// </summary>
public class GameSpeedSlider : MonoBehaviour
{
    [Tooltip("The slider. Empty = the Slider on this object.")]
    public Slider slider;
    [Tooltip("Optional: shows the chosen speed.")]
    public TMP_Text valueLabel;
    [Tooltip("{0} = the speed.")]
    public string labelFormat = "x{0}";
    [Tooltip("The speeds the slider steps through, slowest first.")]
    public float[] speeds = { 0.5f, 1f, 2f };

    private void Awake()
    {
        if (slider == null) slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (slider == null || speeds == null || speeds.Length == 0) return;

        slider.wholeNumbers = true;
        slider.minValue = 0;
        slider.maxValue = speeds.Length - 1;
        slider.SetValueWithoutNotify(ClosestStep(GameSpeed.Value));
        slider.onValueChanged.AddListener(OnChanged);
        ShowLabel(GameSpeed.Value);
    }

    private void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(float step)
    {
        float speed = speeds[Mathf.Clamp(Mathf.RoundToInt(step), 0, speeds.Length - 1)];
        GameSpeed.Set(speed);
        ShowLabel(speed);
    }

    private int ClosestStep(float speed)
    {
        int best = 0;
        for (int i = 1; i < speeds.Length; i++)
        {
            if (Mathf.Abs(speeds[i] - speed) < Mathf.Abs(speeds[best] - speed)) best = i;
        }

        return best;
    }

    private void ShowLabel(float speed)
    {
        if (valueLabel != null) valueLabel.text = string.Format(labelFormat, speed.ToString("0.##"));
    }
}
