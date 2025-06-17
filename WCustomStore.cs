using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("WCustomStore", "wakanda | AI", "2.0.1")]
    [Description("Плагин для проверки ID магазина и содержимого в безопасном городе.")]
    public class WCustomStore : RustPlugin
    {
        private ConfigData configData;
        private Dictionary<uint, string> renamedShopNames = new Dictionary<uint, string>();
        
        private class ConfigData
        {
            [JsonProperty("Магазины мирного города")]
            public Dictionary<string, ShopConfig> CompoundShops { get; set; }
            
            [JsonProperty("Остальные магазины")]
            public Dictionary<string, ShopConfig> OtherShops { get; set; }
        }
        
        private class ShopConfig
        {
            [JsonProperty("Настройка товаров")]
            public List<ShopItemConfig> ItemsConfig { get; set; }
        }
        
        private class ShopItemConfig
        {
            [JsonProperty("Shortname для продажи")]
            public string ItemToSell { get; set; }
            
            [JsonProperty("Кол-во предмета для продажи")]
            public int AmountToSell { get; set; }
            
            [JsonProperty("Shortname для цены")]
            public string CurrencyItem { get; set; }
            
            [JsonProperty("Кол-во предмета для цены")]
            public int CurrencyAmount { get; set; }
        }
        
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                CompoundShops = new Dictionary<string, ShopConfig>
                {
                    {
                        "Building", new ShopConfig { ItemsConfig = new List<ShopItemConfig>() }
                    },
                    {
                        "Weapons", new ShopConfig { ItemsConfig = new List<ShopItemConfig>() }
                    },
                    {
                        "Resource Exchange", new ShopConfig { ItemsConfig = new List<ShopItemConfig>() }
                    },
                    {
                        "Components", new ShopConfig { ItemsConfig = new List<ShopItemConfig>() }
                    },
                    {
                        "Tools & Stuff", new ShopConfig { ItemsConfig = new List<ShopItemConfig>() }
                    },
                    {
                        "Output Outfitters", new ShopConfig { ItemsConfig = new List<ShopItemConfig>() }
                    }
                },
                OtherShops = new Dictionary<string, ShopConfig>
                {
                    {
                        "Food", new ShopConfig
                        {
                            ItemsConfig = new List<ShopItemConfig>
                            {
                                new ShopItemConfig
                                {
                                    ItemToSell = "scrap",
                                    AmountToSell = 30,
                                    CurrencyItem = "fish.minnows",
                                    CurrencyAmount = 10
                                }
                            }
                        }
                    }
                }
            };
            SaveConfig();
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();
        }
        
        protected override void SaveConfig()
        {
            Config.WriteObject(configData);
        }
        
        void OnServerInitialized()
        {
            Bounds compoundBounds = GetCompoundCenter();
            int totalOtherShopsUpdated = 0;
            int totalCompoundShopsUpdated = 0;
            
            foreach (var shop in configData.OtherShops)
            {
                string shopName = shop.Key;
                var shopConfig = shop.Value;
                int updatedCount = 0;
                
                if (shopConfig.ItemsConfig == null || shopConfig.ItemsConfig.Count == 0)
                {
                    continue;
                }
                
                foreach (var vendingMachine in BaseNetworkable.serverEntities.OfType<VendingMachine>())
                {
                    if (vendingMachine.shopName == shopName && !compoundBounds.Contains(vendingMachine.transform.position))
                    {
                        vendingMachine.sellOrders.sellOrders.Clear();
                        vendingMachine.inventory.Clear();
                        
                        foreach (var itemConfig in shopConfig.ItemsConfig)
                        {
                            var itemDef = ItemManager.FindItemDefinition(itemConfig.ItemToSell);
                            var currencyDef = ItemManager.FindItemDefinition(itemConfig.CurrencyItem);
                            
                            if (itemDef != null && currencyDef != null)
                            {
                                var sellOrder = new ProtoBuf.VendingMachine.SellOrder
                                {
                                    itemToSellID = itemDef.itemid,
                                    itemToSellAmount = itemConfig.AmountToSell,
                                    currencyID = currencyDef.itemid,
                                    currencyAmountPerItem = itemConfig.CurrencyAmount,
                                    inStock = 9999
                                };
                                
                                vendingMachine.sellOrders.sellOrders.Add(sellOrder);
                                
                                Item item = ItemManager.Create(itemDef, 9999 * itemConfig.AmountToSell);
                                if (item != null)
                                {
                                    vendingMachine.inventory.itemList.Add(item);
                                    item.parent = vendingMachine.inventory;
                                }
                            }
                        }
                        
                        vendingMachine.SendNetworkUpdateImmediate();
                        vendingMachine.UpdateEmptyFlag();
                        vendingMachine.RefreshSellOrderStockLevel();
                        updatedCount++;
                    }
                }
                totalOtherShopsUpdated += updatedCount;
            }
            
            foreach (var shop in configData.CompoundShops)
            {
                string shopName = shop.Key;
                var shopConfig = shop.Value;
                int updatedCount = 0;
                
                if (shopConfig.ItemsConfig == null || shopConfig.ItemsConfig.Count == 0)
                {
                    continue;
                }
                
                foreach (var vendingMachine in BaseNetworkable.serverEntities.OfType<VendingMachine>())
                {
                    if (vendingMachine.shopName == shopName && compoundBounds.Contains(vendingMachine.transform.position))
                    {
                        vendingMachine.sellOrders.sellOrders.Clear();
                        vendingMachine.inventory.Clear();
                        
                        foreach (var itemConfig in shopConfig.ItemsConfig)
                        {
                            var itemDef = ItemManager.FindItemDefinition(itemConfig.ItemToSell);
                            var currencyDef = ItemManager.FindItemDefinition(itemConfig.CurrencyItem);
                            
                            if (itemDef != null && currencyDef != null)
                            {
                                var sellOrder = new ProtoBuf.VendingMachine.SellOrder
                                {
                                    itemToSellID = itemDef.itemid,
                                    itemToSellAmount = itemConfig.AmountToSell,
                                    currencyID = currencyDef.itemid,
                                    currencyAmountPerItem = itemConfig.CurrencyAmount,
                                    inStock = 9999
                                };
                                
                                vendingMachine.sellOrders.sellOrders.Add(sellOrder);
                                
                                Item item = ItemManager.Create(itemDef, 9999 * itemConfig.AmountToSell);
                                if (item != null)
                                {
                                    vendingMachine.inventory.itemList.Add(item);
                                    item.parent = vendingMachine.inventory;
                                }
                            }
                        }
                        
                        vendingMachine.SendNetworkUpdateImmediate();
                        vendingMachine.UpdateEmptyFlag();
                        vendingMachine.RefreshSellOrderStockLevel();
                        updatedCount++;
                    }
                }
                totalCompoundShopsUpdated += updatedCount;
            }
            
            PrintWarning($"Обновлено магазинов вне компаунда: {totalOtherShopsUpdated}, в компаунде: {totalCompoundShopsUpdated}");
        }
        
        [ChatCommand("cs")]
        private void CsCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("Только администраторы могут использовать /cs.");
                return;
            }
            
            if (args.Length == 0)
            {
                player.ChatMessage("Список команд WCustomStore:");
                player.ChatMessage("/cs check - Проверить ID и содержимое магазина");
            }
            else if (args.Length == 1 && args[0].ToLower() == "check")
            {
                CheckStore(player);
            }
            else
            {
                player.ChatMessage("Используйте /cs для списка команд.");
            }
        }
        
        private void CheckStore(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
            {
                var vendingMachine = hit.GetEntity() as VendingMachine;
                if (vendingMachine != null)
                {
                    string shopName = vendingMachine.shopName;
                    uint netId = vendingMachine.net.ID;
                    
                    string contents = GetVendingMachineContents(vendingMachine);
                    
                    player.ChatMessage($"ID магазина: {netId} (Название: {shopName})");
                    player.ChatMessage($"Содержимое: {contents}");
                }
                else
                {
                    player.ChatMessage("Вы не смотрите на торговый автомат.");
                }
            }
            else
            {
                player.ChatMessage("Вы не смотрите на торговый автомат.");
            }
        }

        private string GetVendingMachineContents(VendingMachine vendingMachine)
        {
            string contents = "";
            foreach (var item in vendingMachine.sellOrders.sellOrders)
            {
                string itemName = ItemManager.FindItemDefinition(item.currencyID)?.displayName?.english ?? "Unknown Item";
                int itemAmount = item.currencyAmountPerItem;
                string costName = ItemManager.FindItemDefinition(item.itemToSellID)?.displayName?.english ?? "Unknown Item";
                int costAmount = item.itemToSellAmount;
                contents += $"{costAmount} {costName} за {itemAmount} {itemName}\n";
            }
            return contents;
        }

        private Bounds GetCompoundCenter()
        {
            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (monument.name.Contains("compound") || monument.name.Contains("outpost"))
                {
                    Vector3 center = monument.transform.position;
                    return new Bounds(center, new Vector3(500, 150, 500));
                }
            }
            PrintWarning("Не удалось найти центр компаунда. Используется центр карты.");
            return new Bounds(new Vector3(0, 0, 0), new Vector3(500, 150, 500));
        }
    }
}

