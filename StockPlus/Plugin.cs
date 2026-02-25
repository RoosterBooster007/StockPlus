using BepInEx;
using HarmonyLib;
using System.Collections;
using Mirror;
using UnityEngine;
using System.Reflection;
using System.Linq;
using BepInEx.Configuration;
using System.Collections.Generic;
using System;
using HutongGames.PlayMaker.Actions;
namespace StockPlus
{
    [BepInPlugin(GUID, PluginName, VersionString)]
    public class StockPlusPlugin : BaseUnityPlugin
    {
        private const string GUID = "RB007.plugins.StockPlus";
        private const string PluginName = "StockPlus";
        private const string VersionString = "1.0.0";
        internal static Harmony Harmony;

        internal static bool isHost = false;
        public static ConfigEntry<bool> isStocking;
        public static ConfigEntry<int> minStockThreshold;
        public static ConfigEntry<bool> telemetryEnabled;
        internal static float checkInterval = 20f;

        internal static GASession gaSession;

        private void Awake()
        {
            isStocking = Config.Bind("Settings", "Auto Stocking Enabled", true, "Any available items (when they are fewer than the threshold below) will be purchased automatically.");
            minStockThreshold = Config.Bind("Settings", "Minimum Stock Threshold", 16, "When there are fewer of each item (than this value) available, they will be added to the shopping list (and purchased).");
            telemetryEnabled = Config.Bind("Misc", "Enable Telemetry", true, "Help me improve this mod by sending logs of small mod/game events.");

            Harmony = new Harmony(GUID);
            Harmony.PatchAll();

            gaSession = new GASession(GUID, PluginName, VersionString, "G-0BREKNCPF9", "MainMenu", "InGame", true, 30);

            Debug.Log("Loaded " + PluginName + " successfully!");
        }

        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnStartHost))]
        public static class OnHost
        {
            [HarmonyPostfix]
            public static void Postfix(NetworkManager __instance)
            {
                isHost = true;
                __instance.StartCoroutine(CheckStock());
            }

            private static IEnumerator CheckStock()
            {
                while (true)
                {
                    yield return new WaitForSeconds(checkInterval);

                    if (isStocking.Value)
                    {
                        AddLowStock(true);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnStartClient))]
        public static class OnClient
        {
            [HarmonyPostfix]
            public static void Postfix(NetworkManager __instance)
            {
                // Allow one-click re-stocking for client players? Needs UI...
            }
        }

        public static void AddLowStock(bool buyToo)
        {
            ManagerBlackboard mboard = FindFirstObjectByType<ManagerBlackboard>();
            ProductListing plisting = FindFirstObjectByType<ProductListing>();
            if (mboard == null || plisting == null)
            {
                Debug.LogWarning("Failed to add low stock to shopping list!");
                return;
            }

            FieldInfo isSpawningField = typeof(ManagerBlackboard).GetField("isSpawning", BindingFlags.NonPublic | BindingFlags.Instance);

            if (isSpawningField != null)
            {
                bool isSpawningValue = (bool)isSpawningField.GetValue(mboard);
                if (isSpawningValue && buyToo)
                {
                    return;
                }
            }

            Type dataProductType = typeof(ProductListing).GetNestedType("ProductData", BindingFlags.Public | BindingFlags.NonPublic);

            FieldInfo productsDataField = typeof(ProductListing).GetField("productsData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo productTierField = dataProductType.GetField("productTier");
            FieldInfo basePricePerUnitField = dataProductType.GetField("basePricePerUnit");
            FieldInfo maxItemsPerBoxField = dataProductType.GetField("maxItemsPerBox");

            Array productsData = (Array)productsDataField.GetValue(plisting);
            MethodInfo gpeInfo = typeof(ManagerBlackboard).GetMethod("GetProductsExistences", BindingFlags.NonPublic | BindingFlags.Instance);

            int count = 0;
            int items = 0;
            float cost = 0;
            foreach (int prodID in plisting.availableProducts)
            {
                if (prodID < 0 || prodID >= productsData.Length)
                {
                    return;
                }

                object prodData = productsData.GetValue(prodID);
                if (prodData == null)
                {
                    return;
                }

                int productTier = (int)productTierField.GetValue(prodData);
                if (!plisting.unlockedProductTiers[productTier])
                {
                    return;
                }

                if (gpeInfo != null)
                {
                    int[] existences = (int[])gpeInfo.Invoke(mboard, new object[] { prodID });
                    if (existences.Sum() < minStockThreshold.Value)
                    {
                        float basePricePerUnit = (float)basePricePerUnitField.GetValue(prodData);
                        int maxItemsPerBox = (int)maxItemsPerBoxField.GetValue(prodData);

                        float price = basePricePerUnit * plisting.tierInflation[productTier];
                        price *= maxItemsPerBox;
                        price = Mathf.Round(price * 100f) / 100f;

                        mboard.AddShoppingListProduct(prodID, price);

                        count++;
                        items += maxItemsPerBox;
                        cost += price;
                    }
                }
            }

            if (buyToo && mboard.shoppingTotalCharge > 0)
            {
                BuyStock();
            }

            if (count > 0) {
                gaSession.sendGAEvent("game", "restock", new Dictionary<string, string>() { ["boxes"] = count.ToString(), ["items"] = items.ToString(), ["cost"] = cost.ToString(), ["buy"] = buyToo.ToString() });
            } else
            {
                gaSession.sendGAEvent("game", "scan", new Dictionary<string, string>() { ["threshold"] = minStockThreshold.Value.ToString(), ["buy"]  = buyToo.ToString() });
            }
        }

        public static void BuyStock()
        {
            ManagerBlackboard mboard = FindFirstObjectByType<ManagerBlackboard>();

            if (mboard == null)
            {
                Debug.LogWarning("Failed to buy shopping list!");
                return;
            }

            mboard.BuyCargo();
        }

        [HarmonyPatch(typeof(PlayerNetwork), nameof(PlayerNetwork.OnStartClient))]
        public static class CreateUI
        {
            [HarmonyPostfix]
            public static void Postfix(PlayerNetwork __instance)
            {
                ManagerBlackboard mboard = FindFirstObjectByType<ManagerBlackboard>();

                if (mboard == null)
                {
                    Debug.LogWarning("Failed to create " + PluginName + " UI!");
                    return;
                }

                // Add UI in future update?
            }
        }

        [HarmonyPatch(typeof(ApplicationQuit), nameof(ApplicationQuit.OnEnter))]
        public static class QuitEvent
        {
            [HarmonyPostfix]
            public static void Prefix(ApplicationQuit __instance)
            {
                gaSession.sendGAEvent("state", "session_end", new Dictionary<string, string>() { ["closed"] = "true" });
            }
        }
    }
}
