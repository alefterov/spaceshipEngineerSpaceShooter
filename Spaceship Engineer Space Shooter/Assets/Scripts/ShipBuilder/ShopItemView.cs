using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One product block of the shop: the product's picture and a button with its price on it. Pressing the button buys the product (through
/// the ShopPanel it sits in). A coin price is shown as a number with the coin icon; a real-money price is the store's own text ("$0.99",
/// "39,99 ₴"). A product bought once (premium) shows Owned Text and its button is off.
///
/// SETUP: put on the product block, assign its Product (a ShopProduct asset), the Buy Button and its Price Label. Everything else is
/// optional: Icon (filled from the product's Icon if it has one — leave empty if the picture is already drawn), Coin Icon (shown only for
/// coin prices), Title Label, Rewards (a RewardRowView showing what the product gives), Owned Mark.
/// </summary>
public class ShopItemView : MonoBehaviour
{
    public ShopProduct product;

    [Header("Parts")]
    public Button buyButton;
    public TMP_Text priceLabel;
    [Tooltip("Optional: the product's picture, filled from the product's Icon (if it has one).")]
    public Image icon;
    [Tooltip("Optional: the coin picture next to a coin price — hidden for real-money prices.")]
    public GameObject coinIcon;
    [Tooltip("Optional: the product's name.")]
    public TMP_Text titleLabel;
    [Tooltip("Optional: what the product gives (amounts with icons).")]
    public RewardRowView rewards;
    public RewardIcons rewardIcons;
    [Tooltip("Optional: shown once a bought-once product (premium) is owned.")]
    public GameObject ownedMark;

    [Header("Texts")]
    public string ownedText = "Куплено";

    private ShopPanel panel;

    private void Awake() => panel = GetComponentInParent<ShopPanel>(true);

    private void OnEnable()
    {
        if (buyButton != null) buyButton.onClick.AddListener(OnBuyPressed);

        var data = GameDataManager.Instance;
        if (data != null) data.OnShopChanged += Refresh;
        if (IAPManager.Instance != null) IAPManager.Instance.OnProductsLoaded += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (buyButton != null) buyButton.onClick.RemoveListener(OnBuyPressed);

        var data = GameDataManager.Instance;
        if (data != null) data.OnShopChanged -= Refresh;
        if (IAPManager.Instance != null) IAPManager.Instance.OnProductsLoaded -= Refresh;
    }

    public void Refresh()
    {
        if (product == null) return;

        var data = GameDataManager.Instance;
        bool owned = data != null && data.IsShopProductOwned(product);

        if (icon != null && product.icon != null) icon.sprite = product.icon;
        if (titleLabel != null) titleLabel.text = product.displayName;
        if (rewards != null) rewards.Show(product.rewards, rewardIcons);
        if (ownedMark != null) ownedMark.SetActive(owned);
        if (coinIcon != null) coinIcon.SetActive(!owned && !product.IsRealMoney);

        if (priceLabel != null)
        {
            priceLabel.text = owned ? ownedText
                : product.IsRealMoney ? (IAPManager.Instance != null ? IAPManager.Instance.GetPriceText(product) : product.fallbackPriceText)
                : NumberFormat.Compact(product.coinPrice);
        }

        if (buyButton != null) buyButton.interactable = !owned;
    }

    private void OnBuyPressed()
    {
        if (product == null) return;

        if (panel != null) panel.Buy(product);
        else Debug.LogWarning("ShopItemView: not inside a ShopPanel - nothing to buy through.", this);
    }
}
