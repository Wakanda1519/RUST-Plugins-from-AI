using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("WLockManager", "wakanda | AI", "1.0.9")]
    [Description("Automatically adds a codelock to doors and boxes with a customizable pin")]
    public class WLockManager : RustPlugin
    {
        private LockData _data;
        private const string DefaultCode = "0000";
        private const string DefaultDoorPermission = "wlockmanager.door";
        private const string DefaultBoxPermission = "wlockmanager.box";
        private string DoorPermissionUsed;
        private string BoxPermissionUsed;
        
        private void Init()
        {
            LoadData();
            DoorPermissionUsed = DefaultDoorPermission;
            BoxPermissionUsed = DefaultBoxPermission;
            permission.RegisterPermission(DoorPermissionUsed, this);
            permission.RegisterPermission(BoxPermissionUsed, this);
            cmd.AddChatCommand("lock", this, LockCommand);
        }
        
        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<LockData>(Name);
                if (_data == null)
                {
                    PrintWarning("Data file is null, creating a new one.");
                    _data = new LockData();
                    SaveData();
                }
            }
            catch (Exception ex)
            {
                PrintError($"Failed to load data: {ex.Message}");
                _data = new LockData();
                SaveData();
            }
        }
        
        private void SaveData()
        {
            try
            {
                if (_data != null)
                {
                    Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
                }
                else
                {
                    PrintWarning("Data is null, cannot save.");
                }
            }
            catch (Exception ex)
            {
                PrintError($"Failed to save data: {ex.Message}");
            }
        }
        
        private void OnEntityBuilt(HeldEntity plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            if (player == null) return;
            
            var entity = go.ToBaseEntity() as DecayEntity;
            if (entity == null) return;
            
            // Проверяем, является ли объект дверью или ящиком
            bool isDoor = entity is Door;
            bool isBox = entity.PrefabName.Contains("box.wooden") || entity.PrefabName.Contains("large.wood");
            if (!isDoor && !isBox) return;
            if (entity.IsLocked()) return;
            
            // Проверяем наличие соответствующих пермишенов и настройки автоматической установки
            if (isDoor && !permission.UserHasPermission(player.UserIDString, DoorPermissionUsed))
            {
                bool autoInstallDoor = _data != null && _data.AutoDoorInstall.ContainsKey(player.UserIDString) && _data.AutoDoorInstall[player.UserIDString];
                if (autoInstallDoor) player.ChatMessage("<color=#7799fffe>[ LOCK ]</color> У вас не достаточно прав!\n<size=12>Приорбетите привилегию на <color=orange>YRS .Market</color></size>");
                return;
            }
            if (isBox && !permission.UserHasPermission(player.UserIDString, BoxPermissionUsed))
            {
                bool autoInstallBox = _data != null && _data.AutoBoxInstall.ContainsKey(player.UserIDString) && _data.AutoBoxInstall[player.UserIDString];
                if (autoInstallBox) player.ChatMessage("<color=#7799fffe>[ LOCK ]</color> У вас нет прав для установки замка на ящики");
                return;
            }
            
            // Проверяем настройки автоматической установки
            bool shouldAutoInstallDoor = _data != null && _data.AutoDoorInstall.ContainsKey(player.UserIDString) && _data.AutoDoorInstall[player.UserIDString];
            bool shouldAutoInstallBox = _data != null && _data.AutoBoxInstall.ContainsKey(player.UserIDString) && _data.AutoBoxInstall[player.UserIDString];
            if ((isDoor && !shouldAutoInstallDoor) || (isBox && !shouldAutoInstallBox))
            {
                return;
            }
            
            string entityType = isDoor ? "дверь" : "ящик";
            bool hasCodeLock = HasCodeLock(player);
            
            if (!hasCodeLock)
            {
                player.ChatMessage("<color=#7799fffe>[ LOCK ]</color> У вас нет кодового замка");
                return;
            }
            
            // Установка кодового замка
            string playerCode = DefaultCode;
            if (_data != null && _data.PlayerCodes.ContainsKey(player.UserIDString))
            {
                playerCode = _data.PlayerCodes[player.UserIDString];
            }
            
            var codeLock = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab") as CodeLock;
            if (codeLock != null)
            {
                codeLock.gameObject.Identity();
                codeLock.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.Lock));
                codeLock.Spawn();
                codeLock.code = playerCode;
                codeLock.hasCode = true;
                entity.SetSlot(BaseEntity.Slot.Lock, codeLock);
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab", codeLock.transform.position);
                codeLock.whitelistPlayers.Add(player.userID);
                codeLock.SetFlag(BaseEntity.Flags.Locked, true);
                
                // Изымаем один замок из инвентаря
                player.inventory.Take(null, 1159991980, 1);
                
                // Проверяем, нужно ли скрывать код замка
                bool hideCode = _data != null && _data.HideCodeMessage.ContainsKey(player.UserIDString) && _data.HideCodeMessage[player.UserIDString];
                if (hideCode)
                {
                    player.ChatMessage($"<color=#7799fffe>[ LOCK ]</color> На {entityType} установлен замок");
                }
                else
                {
                    player.ChatMessage($"<color=#7799fffe>[ LOCK ]</color> На {entityType} установлен замок с кодом <color=orange>{playerCode}</color>");
                }
            }
        }
        
        private void LockCommand(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage("<color=#7799fffe>[ LOCK ]</color> Менеджер замков:\n\n"
                + "<color=orange>/lock save CODE</color> - Сохранить код из 4 цифр\n"
                + "<color=orange>/lock ad</color> - Вкл/выкл установки замка на двери\n"
                + "<color=orange>/lock ab</color> - Вкл/выкл установки замка на ящики\n"
                + "<color=orange>/lock hide</color> - Вкл/выкл показа кода в чате");
                return;
            }
            
            if (args[0].ToLower() == "save" && args.Length == 2 && args[1].Length == 4 && IsDigitsOnly(args[1]))
            {
                string newCode = args[1];
                if (_data != null)
                {
                    _data.PlayerCodes[player.UserIDString] = newCode;
                    SaveData();
                    player.ChatMessage($"<color=#7799fffe>[ LOCK ]</color> Новый код для ваших замков: <color=orange>{newCode}</color>");
                }
                else
                {
                    player.ChatMessage("<color=#7799fffe>[ LOCK ]</color> Ошибка: данные не загружены");
                }
                return;
            }
            
            if (args[0].ToLower() == "ad" && args.Length == 1)
            {
                if (_data != null)
                {
                    bool currentState = _data.AutoDoorInstall.ContainsKey(player.UserIDString) ? _data.AutoDoorInstall[player.UserIDString] : false;
                    bool newState = !currentState;
                    _data.AutoDoorInstall[player.UserIDString] = newState;
                    SaveData();
                    player.ChatMessage($"<color=#7799fffe>[ LOCK ]</color> Установка замков на двери: <color=orange>{(newState ? "включена" : "выключена")}</color>");
                }
                else
                {
                    player.ChatMessage("Ошибка: данные не загружены, попробуйте позже");
                }
                return;
            }
            
            if (args[0].ToLower() == "ab" && args.Length == 1)
            {
                if (_data != null)
                {
                    bool currentState = _data.AutoBoxInstall.ContainsKey(player.UserIDString) ? _data.AutoBoxInstall[player.UserIDString] : false;
                    bool newState = !currentState;
                    _data.AutoBoxInstall[player.UserIDString] = newState;
                    SaveData();
                    player.ChatMessage($"<color=#7799fffe>[ LOCK ]</color> Установка замков на ящики: <color=orange>{(newState ? "включена" : "выключена")}</color>");
                }
                else
                {
                    player.ChatMessage("Ошибка: данные не загружены, попробуйте позже");
                }
                return;
            }
            
            if (args[0].ToLower() == "hide" && args.Length == 1)
            {
                if (_data != null)
                {
                    bool currentState = _data.HideCodeMessage.ContainsKey(player.UserIDString) ? _data.HideCodeMessage[player.UserIDString] : false;
                    bool newState = !currentState;
                    _data.HideCodeMessage[player.UserIDString] = newState;
                    SaveData();
                    player.ChatMessage($"<color=#7799fffe>[ LOCK ]</color> Показ кода замка в чате: <color=orange>{(newState ? "выключен" : "включен")}</color>");
                }
                else
                {
                    player.ChatMessage("Ошибка: данные не загружены, попробуйте позже");
                }
                return;
            }
            
            player.ChatMessage("<color=#7799fffe>[ LOCK ]</color> Используйте <color=orange>/lock</color=orange> для списка команд");
        }
        
        private bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }
            return true;
        }
        
        private bool HasCodeLock(BasePlayer player)
        {
            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item.info.itemid == 1159991980)
                    return true;
            }
            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.info.itemid == 1159991980)
                    return true;
            }
            foreach (var item in player.inventory.containerWear.itemList)
            {
                if (item.info.itemid == 1159991980)
                    return true;
            }
            return false;
        }
        
        private class LockData
        {
            public Dictionary<string, string> PlayerCodes = new Dictionary<string, string>();
            public Dictionary<string, bool> AutoDoorInstall = new Dictionary<string, bool>();
            public Dictionary<string, bool> AutoBoxInstall = new Dictionary<string, bool>();
            public Dictionary<string, bool> HideCodeMessage = new Dictionary<string, bool>();
        }
        
        private void Unload()
        {
            SaveData();
        }
    }
}