using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put this on the BlockButton prefab (the one BuildPaletteUI instantiates per block).
/// Exposes explicit references instead of guessing via GetComponentInChildren, since a
/// button has multiple Image components (background, icon, highlight frame) that are
/// easy to mix up automatically.
/// </summary>
public class BlockButtonView : MonoBehaviour
{
    [Tooltip("The block's icon sprite. Its RectTransform is rotated to show the block's current orientation.")]
    public Image icon;

    [Tooltip("A white glow/border frame around the button, box-shadow style. " +
             "Child object, inactive by default — enabled only while this button is selected.")]
    public GameObject selectionHighlight;

    public RectTransform IconTransform => icon != null ? icon.rectTransform : null;

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null) selectionHighlight.SetActive(selected);
    }
}
