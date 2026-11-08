using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Tooltip("Shows the block's size in cells, width x height: \"1x1\", \"1x2\", \"2x2\"...")]
    public TMP_Text sizeLabel;

    public RectTransform IconTransform => icon != null ? icon.rectTransform : null;

    public BlockDefinition Block { get; set; }

    public void SetPrice(int credits)
    {
        if (costLabel != null) costLabel.text = NumberFormat.Compact(credits);
    }

    public void SetSize(BlockDefinition block)
    {
        if (sizeLabel == null || block == null || block.cells == null || block.cells.Count == 0) return;

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var cell in block.cells)
        {
            minX = Mathf.Min(minX, cell.offset.x);
            minY = Mathf.Min(minY, cell.offset.y);
            maxX = Mathf.Max(maxX, cell.offset.x);
            maxY = Mathf.Max(maxY, cell.offset.y);
        }

        sizeLabel.text = $"{maxX - minX + 1}x{maxY - minY + 1}";
    }

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null) selectionHighlight.SetActive(selected);
    }
}
