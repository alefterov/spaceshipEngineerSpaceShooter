using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One reward shown as a picture and an amount: credits, coins, experience, research points, or crew cards. Numbers are shortened like
/// every number of the game (1000 = 1k); cards show "x2".
/// </summary>
public class RewardView : MonoBehaviour
{
    public Image icon;
    public TMP_Text amountLabel;

    public void Show(RewardData reward, RewardIcons icons)
    {
        if (icon != null)
        {
            var sprite = icons != null ? icons.Get(reward.type) : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (amountLabel != null)
            amountLabel.text = reward.type == RewardType.RandomCrewCard ? "x" + reward.amount : NumberFormat.Compact(reward.amount);
    }
}
