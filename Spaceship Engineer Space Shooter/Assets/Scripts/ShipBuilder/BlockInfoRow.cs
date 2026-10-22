using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One row of the block card (BlockInfoCard): a small icon and a number. This is the component of the ROW PREFAB — make a
/// prefab with an Image (the icon) and a text (the number) next to each other, put this on its root and assign the two.
/// BlockInfoCard creates one such row for every parameter the selected block has, inside its vertical layout group,
/// and fills in the icon and the number.
/// </summary>
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
