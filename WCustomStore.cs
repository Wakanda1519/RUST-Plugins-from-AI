using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("WCustomStore", "wakanda | AI", "1.0.0")]
    [Description("Плагин для проверки ID магазина и содержимого в безопасном городе.")]
    public class WCustomStore : RustPlugin
    {
        private ConfigData configData;
        
        private class ConfigData
        {
            public Dictionary<string, ShopConfig> Shops { get; set; }
        }
        
        private class ShopConfig
        {
            public List<ShopItemConfig> НастройкаТоваров { get; set; }
        }
        
        private class ShopItemConfig
        {
            public string ПредметДляПродажи { get; set; }
            public Dictionary<string, int> ЦенаЗаПредмет { get; set; }
        }
        
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                Shops = new Dictionary<string, ShopConfig>
                {
                    {
                        "Output Outfitters", new ShopConfig
                        {
                            НастройкаТоваров = new List<ShopItemConfig>
                            {
                                new ShopItemConfig
                                {
                                    ПредметДляПродажи = "pistol.revolver",
                                    ЦенаЗаПредмет = new Dictionary<string, int> { { "scrap", 66 } }
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
            // При загрузке сервера обновляем содержимое магазинов из конфига
            foreach (var shop in configData.Shops)
            {
                string shopName = shop.Key;
                var shopConfig = shop.Value;
                
                if (shopConfig.НастройкаТоваров == null || shopConfig.НастройкаТоваров.Count == 0)
                {
                    Puts($"Конфигурация товаров для магазина {shopName} не найдена или пуста.");
                    continue;
                }
                
                foreach (var vendingMachine in BaseNetworkable.serverEntities.OfType<VendingMachine>())
                {
                    if (vendingMachine.shopName == shopName)
                    {
                        vendingMachine.sellOrders.sellOrders.Clear();
                        
                        foreach (var itemConfig in shopConfig.НастройкаТоваров)
                        {
                            var itemDef = ItemManager.FindItemDefinition(itemConfig.ПредметДляПродажи);
                            var currencyDef = ItemManager.FindItemDefinition(itemConfig.ЦенаЗаПредмет.Keys.First());
                            int currencyAmount = itemConfig.ЦенаЗаПредмет.Values.First();
                            
                            if (itemDef != null && currencyDef != null)
                            {
                                var sellOrder = new ProtoBuf.VendingMachine.SellOrder
                                {
                                    itemToSellID = itemDef.itemid,
                                    itemToSellAmount = 1,
                                    currencyID = currencyDef.itemid,
                                    currencyAmountPerItem = currencyAmount,
                                    inStock = 9999
                                };
                                
                                vendingMachine.sellOrders.sellOrders.Add(sellOrder);
                                
                                Item item = ItemManager.Create(itemDef, 9999);
                                if (item != null)
                                {
                                    vendingMachine.inventory.itemList.Add(item);
                                    item.parent = vendingMachine.inventory;
                                }
                            }
                        }
                        
                        vendingMachine.SendNetworkUpdate();
                        vendingMachine.UpdateEmptyFlag();
                        Puts($"Магазин '{shopName}' (ID: {vendingMachine.net.ID}) был обновлен с товарами из конфигурации.");
                    }
                }
            }
        }
        
        [ChatCommand("cs")]
        private void CsCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("У вас нет прав для использования этой команды. Только администраторы могут использовать /cs.");
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
                player.ChatMessage("Неизвестная команда. Используйте /cs для списка команд.");
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
    }
}
