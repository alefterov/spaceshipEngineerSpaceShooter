using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Ship Builder > Create Default Shop Products: makes the starting set of shop products (Assets/Data/Shop) and the Shop Catalog
/// holding them — the premium, credits, crew card sets and research points for coins, and three coin packs for real money. A product that
/// already exists is left as it is (its numbers may have been changed by hand), only missing ones are made. Every number can then be changed
/// in the product's Inspector.
/// </summary>
public static class ShopSetup
{
    private const string Folder = "Assets/Data/Shop";

    [MenuItem("Tools/Ship Builder/Create Default Shop Products")]
    public static void CreateDefaultProducts()
    {
        Directory.CreateDirectory(Folder);

        var products = new List<ShopProduct>
        {
            // Real money
            RealMoney("shop_premium", "Премиум", "premium", StoreProductType.NonConsumable, "$4.99", premium: true),
            RealMoney("shop_coins_1000", "1000 монет", "coins_1000", StoreProductType.Consumable, "$0.99", new RewardData(RewardType.Coins, 1000)),
            RealMoney("shop_coins_5500", "5500 монет", "coins_5500", StoreProductType.Consumable, "$4.99", new RewardData(RewardType.Coins, 5500)),
            RealMoney("shop_coins_12000", "12000 монет", "coins_12000", StoreProductType.Consumable, "$9.99", new RewardData(RewardType.Coins, 12000)),

            // Credits for coins
            ForCoins("shop_credits_1000", "1000 кредитов", 100, new RewardData(RewardType.Credits, 1000)),
            ForCoins("shop_credits_10000", "10000 кредитов", 949, new RewardData(RewardType.Credits, 10000)),
            ForCoins("shop_credits_100000", "100000 кредитов", 9449, new RewardData(RewardType.Credits, 100000)),

            // Crew card sets for coins
            ForCoins("shop_crew_1", "1 карта экипажа", 150, new RewardData(RewardType.RandomCrewCard, 1)),
            ForCoins("shop_crew_5", "5 карт экипажа", 700, new RewardData(RewardType.RandomCrewCard, 5)),
            ForCoins("shop_crew_10", "10 карт экипажа", 1300, new RewardData(RewardType.RandomCrewCard, 10)),

            // Research points for coins
            ForCoins("shop_research_100", "100 очков исследования", 200, new RewardData(RewardType.ResearchPoints, 100)),
            ForCoins("shop_research_500", "500 очков исследования", 900, new RewardData(RewardType.ResearchPoints, 500)),
            ForCoins("shop_research_2000", "2000 очков исследования", 3200, new RewardData(RewardType.ResearchPoints, 2000)),
        };

        string catalogPath = $"{Folder}/ShopCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(catalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ShopCatalog>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
        }

        foreach (var product in products)
        {
            if (!catalog.products.Contains(product)) catalog.products.Add(product);
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        // Hand the catalog to the IAPManager of the open scene, if it has none yet.
        var manager = Object.FindAnyObjectByType<IAPManager>(FindObjectsInactive.Include);
        if (manager != null && manager.catalog == null)
        {
            Undo.RecordObject(manager, "Assign Shop Catalog");
            manager.catalog = catalog;
            EditorUtility.SetDirty(manager);
        }

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
        Debug.Log($"ShopSetup: {catalog.products.Count} shop products in {catalogPath}.");
    }

    private static ShopProduct ForCoins(string id, string name, int price, params RewardData[] rewards)
        => GetOrCreate(id, p =>
        {
            p.displayName = name;
            p.payment = ShopPayment.Coins;
            p.coinPrice = price;
            p.rewards = new List<RewardData>(rewards);
        });

    private static ShopProduct RealMoney(string id, string name, string storeId, StoreProductType type, string price, params RewardData[] rewards)
        => RealMoney(id, name, storeId, type, price, false, rewards);

    private static ShopProduct RealMoney(string id, string name, string storeId, StoreProductType type, string price, bool premium, params RewardData[] rewards)
        => GetOrCreate(id, p =>
        {
            p.displayName = name;
            p.payment = ShopPayment.RealMoney;
            p.storeProductId = storeId;
            p.storeType = type;
            p.fallbackPriceText = price;
            p.grantsPremium = premium;
            p.rewards = new List<RewardData>(rewards);
        });

    /// <summary>The product asset with this id; made (and filled by `fill`) only if it does not exist yet.</summary>
    private static ShopProduct GetOrCreate(string id, System.Action<ShopProduct> fill)
    {
        string path = $"{Folder}/{id}.asset";
        var product = AssetDatabase.LoadAssetAtPath<ShopProduct>(path);
        if (product != null) return product;

        product = ScriptableObject.CreateInstance<ShopProduct>();
        product.id = id;
        fill(product);
        AssetDatabase.CreateAsset(product, path);
        return product;
    }
}
