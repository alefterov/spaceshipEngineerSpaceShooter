using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "ShipBuilder/Shop Catalog", fileName = "ShopCatalog")]
public class ShopCatalog : ScriptableObject
{
    public List<ShopProduct> products = new();

    public IEnumerable<ShopProduct> RealMoneyProducts
        => products.Where(p => p != null && p.IsRealMoney && !string.IsNullOrEmpty(p.storeProductId));

    public ShopProduct GetByStoreId(string storeProductId)
        => string.IsNullOrEmpty(storeProductId) ? null : products.FirstOrDefault(p => p != null && p.IsRealMoney && p.storeProductId == storeProductId);
}
