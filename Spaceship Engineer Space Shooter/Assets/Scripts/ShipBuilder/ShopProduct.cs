using System.Collections.Generic;
using UnityEngine;

public enum ShopPayment
{
    Coins = 0,
    RealMoney = 1,
}

public enum StoreProductType
{
    Consumable = 0,
    NonConsumable = 1,
}

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

    public bool IsOneTime => grantsPremium || (payment == ShopPayment.RealMoney && storeType == StoreProductType.NonConsumable);

    public bool IsRealMoney => payment == ShopPayment.RealMoney;
}
