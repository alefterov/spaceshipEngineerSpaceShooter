using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if IAP_SDK
using UnityEngine.Purchasing;
#endif

public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    [Tooltip("Every product of the shop; the real-money ones are registered with the store.")]
    public ShopCatalog catalog;
    [Tooltip("In the Editor, and while the In App Purchasing package is not installed: real-money purchases succeed at once (for testing).")]
    public bool simulateInEditor = true;

    public event Action OnProductsLoaded;
    public event Action<ShopProduct> OnPurchaseSucceeded;
    public event Action<ShopProduct, string> OnPurchaseFailed;
    public event Action<ShopProduct> OnPurchaseDeferred;
    public event Action<bool> OnRestoreFinished;

    public bool IsReady { get; private set; }

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

#endif
    }

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
        store.FetchPurchases();
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

        if (data == null || product == null)
        {
            Debug.LogWarning($"IAPManager: a purchase of '{item?.Product?.definition.id}' could not be given out now (no GameDataManager, or not in the Shop Catalog).");
            return;
        }

        string transactionId = order.Info.TransactionID;
        bool alreadyGiven = data.IsOrderProcessed(transactionId) || (product.IsOneTime && data.IsShopProductOwned(product));

        if (!alreadyGiven)
        {
            data.GrantShopProduct(product);
            data.MarkOrderProcessed(transactionId);
        }

        store.ConfirmPurchase(order);

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
