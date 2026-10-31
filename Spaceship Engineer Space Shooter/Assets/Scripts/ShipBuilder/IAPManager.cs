using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if IAP_SDK
using UnityEngine.Purchasing;
#endif

/// <summary>
/// Purchases for real money through the platform store (Google Play), with Unity In-App Purchasing 5. Registers every real-money product of
/// the Shop Catalog with the store, shows the store's own prices, buys, and gives the purchase out (GameDataManager.GrantShopProduct).
///
/// How a purchase goes (the store's rule): the store reports it as pending -> the game gives it out and SAVES -> only then the game
/// confirms it to the store. If the game is closed in between, the store sends the purchase again at the next start, and it is given then.
/// Every transaction given out is remembered (GameData.processedOrders), so the same purchase is never given twice. Bought-once products
/// (the premium) are taken back from the store at every start — after reinstalling, on another phone — and "Restore Purchases" asks again.
///
/// WITHOUT THE STORE — before the In App Purchasing package is installed (the scripting symbol IAP_SDK is set by IapDefineSetter as soon as
/// it is), and in the Editor while Simulate In Editor is on — a purchase succeeds at once, for testing.
///
/// SETUP: one object in the first scene (it survives scene changes, like AdsManager). Assign the Shop Catalog.
/// </summary>
public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    [Tooltip("Every product of the shop; the real-money ones are registered with the store.")]
    public ShopCatalog catalog;
    [Tooltip("In the Editor, and while the In App Purchasing package is not installed: real-money purchases succeed at once (for testing).")]
    public bool simulateInEditor = true;

    /// <summary>Prices arrived from the store (or the store went away) — price labels refresh on this.</summary>
    public event Action OnProductsLoaded;
    /// <summary>A real-money purchase was given out.</summary>
    public event Action<ShopProduct> OnPurchaseSucceeded;
    /// <summary>A real-money purchase did not happen. The text is the reason; null if the player simply cancelled.</summary>
    public event Action<ShopProduct, string> OnPurchaseFailed;
    /// <summary>A real-money purchase waits for approval (a parent, a slow payment) — it is given out when the store approves it.</summary>
    public event Action<ShopProduct> OnPurchaseDeferred;
    /// <summary>"Restore Purchases" finished: true if the store answered.</summary>
    public event Action<bool> OnRestoreFinished;

    /// <summary>The store is connected and its products are loaded — purchases can start.</summary>
    public bool IsReady { get; private set; }

    /// <summary>A purchase is in progress (between pressing Buy and the store's answer).</summary>
    public bool IsBusy { get; private set; }

    private bool Simulated
    {
        get
        {
#if IAP_SDK
            return Application.isEditor && simulateInEditor;
#else
            return simulateInEditor && (Application.isEditor || Debug.isDebugBuild);
#endif
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Start, not Awake: the store may hand over a purchase from the last session at once, and GameDataManager must exist by then.
    private void Start()
    {
        if (Simulated)
        {
            IsReady = true;
            OnProductsLoaded?.Invoke();
            return;
        }

#if IAP_SDK
        ConnectStore();
#else
        Debug.Log("IAPManager: the In App Purchasing package is not installed - real-money purchases are off.");
#endif
    }

    // =====================================================================================================
    //  Public API
    // =====================================================================================================

    /// <summary>The price to show on a real-money product's button: the store's own (in the player's currency) once loaded, else the
    /// product's Fallback Price Text.</summary>
    public string GetPriceText(ShopProduct product)
    {
        if (product == null) return "";

#if IAP_SDK
        if (!Simulated && store != null)
        {
            var storeProduct = store.GetProductById(product.storeProductId);
            if (storeProduct != null && storeProduct.metadata != null && !string.IsNullOrEmpty(storeProduct.metadata.localizedPriceString))
                return storeProduct.metadata.localizedPriceString;
        }
#endif

        return product.fallbackPriceText;
    }

    /// <summary>Whether this real-money product can be bought now (the store is ready and knows it).</summary>
    public bool IsAvailable(ShopProduct product)
    {
        if (product == null || !product.IsRealMoney || !IsReady) return false;
        if (Simulated) return true;

#if IAP_SDK
        var storeProduct = store != null ? store.GetProductById(product.storeProductId) : null;
        return storeProduct != null && storeProduct.availableToPurchase;
#else
        return false;
#endif
    }

    /// <summary>Starts buying a real-money product. The answer comes through OnPurchaseSucceeded / OnPurchaseFailed / OnPurchaseDeferred.</summary>
    public void Buy(ShopProduct product)
    {
        if (product == null || !product.IsRealMoney) return;

        if (IsBusy)
        {
            OnPurchaseFailed?.Invoke(product, "busy");
            return;
        }

        if (!IsAvailable(product))
        {
            OnPurchaseFailed?.Invoke(product, "unavailable");
            return;
        }

        if (Simulated)
        {
            Debug.Log($"IAPManager: simulated purchase of {product.storeProductId}.");
            GameDataManager.Instance.GrantShopProduct(product);
            OnPurchaseSucceeded?.Invoke(product);
            return;
        }

#if IAP_SDK
        IsBusy = true;
        pendingProduct = product;
        store.PurchaseProduct(store.GetProductById(product.storeProductId));
#endif
    }

    /// <summary>Asks the store again for the bought-once products (the premium). For a "Restore Purchases" button — required on iOS.</summary>
    public void RestorePurchases()
    {
        if (Simulated)
        {
            OnRestoreFinished?.Invoke(true);
            return;
        }

#if IAP_SDK
        if (store == null || !IsReady)
        {
            OnRestoreFinished?.Invoke(false);
            return;
        }

        store.RestoreTransactions((success, error) =>
        {
            if (!success) Debug.LogWarning($"IAPManager: restore failed: {error}");
            OnRestoreFinished?.Invoke(success);
        });
#else
        OnRestoreFinished?.Invoke(false);
#endif
    }

    private ShopProduct FindProduct(string storeProductId) => catalog != null ? catalog.GetByStoreId(storeProductId) : null;

#if IAP_SDK
    // =====================================================================================================
    //  Unity IAP 5
    // =====================================================================================================

    private StoreController store;
    private ShopProduct pendingProduct;

    private async void ConnectStore()
    {
        if (catalog == null)
        {
            Debug.LogWarning("IAPManager: no Shop Catalog assigned - real-money purchases are off.", this);
            return;
        }

        store = UnityIAPServices.StoreController();

        // Every event BEFORE Connect: a purchase left unfinished last time may arrive at once.
        store.OnStoreConnected += HandleStoreConnected;
        store.OnStoreDisconnected += HandleStoreDisconnected;
        store.OnProductsFetched += HandleProductsFetched;
        store.OnProductsFetchFailed += HandleProductsFetchFailed;
        store.OnPurchasesFetched += HandlePurchasesFetched;
        store.OnPurchasesFetchFailed += HandlePurchasesFetchFailed;
        store.OnPurchasePending += HandlePurchasePending;
        store.OnPurchaseConfirmed += HandlePurchaseConfirmed;
        store.OnPurchaseFailed += HandlePurchaseFailed;
        store.OnPurchaseDeferred += HandlePurchaseDeferred;

        try
        {
            await store.Connect();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"IAPManager: could not connect to the store: {e.Message}");
        }
    }

    private void HandleStoreConnected()
    {
        var definitions = catalog.RealMoneyProducts
            .Select(p => new ProductDefinition(p.storeProductId, p.storeType == StoreProductType.NonConsumable ? ProductType.NonConsumable : ProductType.Consumable))
            .GroupBy(d => d.id)
            .Select(g => g.First())
            .ToList();

        if (definitions.Count == 0)
        {
            Debug.LogWarning("IAPManager: the Shop Catalog has no real-money product with a Store Product Id.", this);
            return;
        }

        store.FetchProducts(definitions);
    }

    private void HandleStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        Debug.LogWarning($"IAPManager: store disconnected: {failure.Message}");
        IsReady = false;
        IsBusy = false;
        OnProductsLoaded?.Invoke();
    }

    private void HandleProductsFetched(List<Product> products)
    {
        IsReady = true;
        OnProductsLoaded?.Invoke();
        store.FetchPurchases(); // bought-once products the player already owns (reinstall, another phone)
    }

    private void HandleProductsFetchFailed(ProductFetchFailed failure)
    {
        Debug.LogWarning($"IAPManager: products could not be loaded: {failure.FailureReason}");
        OnProductsLoaded?.Invoke();
    }

    private void HandlePurchasesFetched(Orders orders)
    {
        var data = GameDataManager.Instance;
        if (data == null) return;

        foreach (var order in orders.ConfirmedOrders)
        {
            foreach (var item in order.CartOrdered.Items())
            {
                var product = FindProduct(item.Product?.definition.id);
                if (product != null && product.IsOneTime) data.RestoreShopProduct(product);
            }
        }
    }

    private void HandlePurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        => Debug.LogWarning($"IAPManager: purchases could not be loaded: {failure.Message}");

    private void HandlePurchasePending(PendingOrder order)
    {
        var data = GameDataManager.Instance;
        var item = order.CartOrdered.Items().FirstOrDefault();
        var product = FindProduct(item?.Product?.definition.id);

        // Not confirmed on purpose: the store sends it again at the next start, when it can be given out.
        if (data == null || product == null)
        {
            Debug.LogWarning($"IAPManager: a purchase of '{item?.Product?.definition.id}' could not be given out now (no GameDataManager, or not in the Shop Catalog).");
            return;
        }

        string transactionId = order.Info.TransactionID;
        bool alreadyGiven = data.IsOrderProcessed(transactionId) || (product.IsOneTime && data.IsShopProductOwned(product));

        if (!alreadyGiven)
        {
            data.GrantShopProduct(product); // saves
            data.MarkOrderProcessed(transactionId); // saves
        }

        store.ConfirmPurchase(order); // only after the save

        IsBusy = false;
        pendingProduct = null;
        if (!alreadyGiven) OnPurchaseSucceeded?.Invoke(product);
    }

    private void HandlePurchaseConfirmed(Order order)
    {
        if (order is FailedOrder failed) Debug.LogWarning($"IAPManager: confirming a purchase failed: {failed.FailureReason} - {failed.Details}");
    }

    private void HandlePurchaseFailed(FailedOrder failed)
    {
        var item = failed.CartOrdered.Items().FirstOrDefault();
        var product = FindProduct(item?.Product?.definition.id) ?? pendingProduct;

        IsBusy = false;
        pendingProduct = null;

        bool cancelled = failed.FailureReason == PurchaseFailureReason.UserCancelled;
        if (!cancelled) Debug.LogWarning($"IAPManager: purchase failed: {failed.FailureReason} - {failed.Details}");
        OnPurchaseFailed?.Invoke(product, cancelled ? null : failed.FailureReason.ToString());
    }

    private void HandlePurchaseDeferred(DeferredOrder deferred)
    {
        var item = deferred.CartOrdered.Items().FirstOrDefault();
        var product = FindProduct(item?.Product?.definition.id) ?? pendingProduct;

        IsBusy = false;
        pendingProduct = null;
        OnPurchaseDeferred?.Invoke(product);
    }
#endif
}
