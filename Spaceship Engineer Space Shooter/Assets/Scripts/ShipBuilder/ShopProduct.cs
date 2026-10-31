using System.Collections.Generic;
using UnityEngine;

/// <summary>How a shop product is paid for.</summary>
public enum ShopPayment
{
    /// <summary>With the game's coins.</summary>
    Coins = 0,
    /// <summary>With real money through the platform store (Google Play) — see IAPManager.</summary>
    RealMoney = 1,
}

/// <summary>The kind of a real-money product, as it is set up in the Google Play Console.</summary>
public enum StoreProductType
{
    /// <summary>Can be bought again and again (a coin pack).</summary>
    Consumable = 0,
    /// <summary>Bought once and kept forever, restored after reinstalling (the premium).</summary>
    NonConsumable = 1,
}

/// <summary>
/// One product of the shop: what it costs and what the player gets. Every number is set here in the Inspector.
///  - Paid with COINS: Coin Price. Gives Rewards (credits, research points, crew cards, experience, coins).
///  - Paid with REAL MONEY: Store Product Id (the exact id of the product in the Google Play Console) and its Store Type. The price shown is
///    the store's own (in the player's currency); Fallback Price Text is shown until the store answers (and in the Editor).
///  - Grants Premium: no ads, and battle credits and experience x2 (GameDataManager.Premium Reward Multiplier) — forever. A premium product
///    (and any Non Consumable one) can be bought only once; its button then shows "bought".
/// Put every product into the ShopCatalog (IAPManager registers the real-money ones with the store from it) and give each ShopItemView one.
/// </summary>
[CreateAssetMenu(menuName = "ShipBuilder/Shop Product", fileName = "NewShopProduct")]
public class ShopProduct : ScriptableObject
{
    [Tooltip("Unique id, kept in the save for products bought once. Never change it after release.")]
    public string id = "shop_product";
    [Tooltip("Name for the shop (optional — the product's own picture may already say it).")]
    public string displayName;
    [Tooltip("Picture of the product (optional — ShopItemView shows it if it has an Icon image).")]
    public Sprite icon;

    [Header("Price")]
    public ShopPayment payment = ShopPayment.Coins;
    [Tooltip("Coins: the price in coins.")]
    [Min(0)] public int coinPrice = 100;

    [Header("Real money (Google Play)")]
    [Tooltip("Real money: the product id exactly as created in the Google Play Console (Monetize > Products > In-app products).")]
    public string storeProductId;
    [Tooltip("Real money: Consumable = can be bought again (coin packs), Non Consumable = bought once and kept (premium). Must match the Console.")]
    public StoreProductType storeType = StoreProductType.Consumable;
    [Tooltip("Real money: price text shown until the store sends the real one (and in the Editor).")]
    public string fallbackPriceText = "$0.99";

    [Header("What the player gets")]
    [Tooltip("Credits, coins, research points, experience, random crew cards — any number of them.")]
    public List<RewardData> rewards = new();
    [Tooltip("No ads + battle credits and experience x2, forever. Bought only once.")]
    public bool grantsPremium;

    /// <summary>Can be bought only once (then it is owned).</summary>
    public bool IsOneTime => grantsPremium || (payment == ShopPayment.RealMoney && storeType == StoreProductType.NonConsumable);

    public bool IsRealMoney => payment == ShopPayment.RealMoney;
}
