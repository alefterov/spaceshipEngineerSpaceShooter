using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BlockInfoRow : MonoBehaviour
{
    public Image icon;
    public TMP_Text valueLabel;

    public void Set(Sprite sprite, string value)
    {
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (valueLabel != null) valueLabel.text = value;
    }
}
