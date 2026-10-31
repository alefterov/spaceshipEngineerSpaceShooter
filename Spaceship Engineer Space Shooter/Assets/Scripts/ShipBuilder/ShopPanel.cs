using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The shop window. Holds the product blocks (ShopItemView, anywhere inside it) and does the buying: a coin product is paid at once
/// (GameDataManager.TryBuyWithCoins), a real-money one goes to the store (IAPManager). Tells the player how it went through the Message
/// Toast (bought / not enough coins / waiting for approval / failed).
///
/// SETUP: put on the shop window. Wire the shop button of the main panel to Open() (or Toggle()) and the window's close button to Close()
/// (or assign it to Close Button). Optional: Restore Button (asks the store again for the premium — wire it if the game goes to iOS).
/// </summary>
public class ShopPanel : MonoBehaviour
{
    [Tooltip("Optional: closes the window.")]
    public Button closeButton;
    [Tooltip("Optional: \"Restore Purchases\" — gives back the premium bought before (reinstall, another phone).")]
    public Button restoreButton;
    [Tooltip("Shows the short messages below.")]
    public MessageToast messageToast;

    [Header("Messages")]
    public string purchasedMessage = "Покупка выполнена";
    public string notEnoughCoinsMessage = "Недостаточно монет";
    public string deferredMessage = "Покупка ожидает подтверждения";
    public string failedMessage = "Покупка не удалась";
    public string unavailableMessage = "Магазин сейчас недоступен";
    public string restoredMessage = "Покупки восстановлены";

    public void Open() => gameObject.SetActive(true);
    public void Close() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (restoreButton != null) restoreButton.onClick.AddListener(RestorePurchases);
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (restoreButton != null) restoreButton.onClick.RemoveListener(RestorePurchases);
    }

    private void OnEnable()
    {
        var iap = IAPManager.Instance;
        if (iap == null) return;

        iap.OnPurchaseSucceeded += HandleSucceeded;
        iap.OnPurchaseFailed += HandleFailed;
        iap.OnPurchaseDeferred += HandleDeferred;
        iap.OnRestoreFinished += HandleRestored;
    }

    private void OnDisable()
    {
        var iap = IAPManager.Instance;
        if (iap == null) return;

        iap.OnPurchaseSucceeded -= HandleSucceeded;
        iap.OnPurchaseFailed -= HandleFailed;
        iap.OnPurchaseDeferred -= HandleDeferred;
        iap.OnRestoreFinished -= HandleRestored;
    }

    /// <summary>Buys a product — ShopItemView calls this when its button is pressed.</summary>
    public void Buy(ShopProduct product)
    {
        var data = GameDataManager.Instance;
        if (product == null || data == null || data.IsShopProductOwned(product)) return;

        if (product.IsRealMoney)
        {
            if (IAPManager.Instance != null) IAPManager.Instance.Buy(product);
            else Toast(unavailableMessage);
            return;
        }

        if (data.TryBuyWithCoins(product)) Toast(purchasedMessage);
        else if (data.Coins < product.coinPrice) Toast(notEnoughCoinsMessage);
    }

    /// <summary>Asks the store again for the bought-once products (premium).</summary>
    public void RestorePurchases()
    {
        if (IAPManager.Instance != null) IAPManager.Instance.RestorePurchases();
        else Toast(unavailableMessage);
    }

    private void HandleSucceeded(ShopProduct product) => Toast(purchasedMessage);

    // A null reason = the player cancelled the store window: nothing to say.
    private void HandleFailed(ShopProduct product, string reason)
    {
        if (reason == null || reason == "busy") return;
        Toast(reason == "unavailable" ? unavailableMessage : failedMessage);
    }

    private void HandleDeferred(ShopProduct product) => Toast(deferredMessage);

    private void HandleRestored(bool success) => Toast(success ? restoredMessage : unavailableMessage);

    private void Toast(string message)
    {
        if (messageToast != null && !string.IsNullOrEmpty(message)) messageToast.Show(message);
    }
}
