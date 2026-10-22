using TMPro;
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

    [Tooltip("Shows the block's build price (credits), shortened like every price: 1000 = 1k, 1 250 000 = 1.25m. The button no longer shows the " +
             "block's name — that lives on the block card.")]
    public TMP_Text costLabel;

    public RectTransform IconTransform => icon != null ? icon.rectTransform : null;

    /// <summary>Writes the build price on the button (rounded for display only, like all prices).</summary>
    public void SetPrice(int credits)
    {
        if (costLabel != null) costLabel.text = NumberFormat.Compact(credits);
    }


    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null) selectionHighlight.SetActive(selected);
    }
}
