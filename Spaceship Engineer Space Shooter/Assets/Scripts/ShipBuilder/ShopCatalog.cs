using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Every product of the shop in one list. IAPManager registers the real-money ones with the store from here, and finds the product
/// a store purchase belongs to.</summary>
[CreateAssetMenu(menuName = "ShipBuilder/Shop Catalog", fileName = "ShopCatalog")]
public class ShopCatalog : ScriptableObject
{
    public List<ShopProduct> products = new();

    public IEnumerable<ShopProduct> RealMoneyProducts
        => products.Where(p => p != null && p.IsRealMoney && !string.IsNullOrEmpty(p.storeProductId));

    public ShopProduct GetByStoreId(string storeProductId)
        => string.IsNullOrEmpty(storeProductId) ? null : products.FirstOrDefault(p => p != null && p.IsRealMoney && p.storeProductId == storeProductId);
}
