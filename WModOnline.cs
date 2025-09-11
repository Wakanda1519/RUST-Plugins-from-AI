using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("WModOnline", "wakanda | AI", "1.0.0")]
    [Description("Добавляет фейковых игроков к онлайну сервера")]
    class WModOnline : RustPlugin
    {
        #region Configuration
        
        private Configuration config;
        
        #region Variables
        
        private int currentActiveFakes = 0;
        private int targetFakeCount = 0;
        private Timer fakeAdditionTimer;
        private List<int> randomizedNameIndices = new List<int>();
        
        #endregion
        
        public class Configuration
        {
            [JsonProperty("Динамическое добавление фейковых игроков")]
            public bool DynamicFakePlayersEnabled { get; set; } = true;
            
            [JsonProperty("Правила добавления фейковых игроков по количеству реальных")]
            public List<FakePlayerRule> FakePlayerRules { get; set; } = new List<FakePlayerRule>
            {
                new FakePlayerRule { MinRealPlayers = 1, MaxRealPlayers = 9, FakePlayersToAdd = 3 },
                new FakePlayerRule { MinRealPlayers = 10, MaxRealPlayers = 19, FakePlayersToAdd = 5 },
                new FakePlayerRule { MinRealPlayers = 20, MaxRealPlayers = 999, FakePlayersToAdd = 7 }
            };
            
            [JsonProperty("Постепенное добавление фейков (включить)")]
            public bool GradualAdditionEnabled { get; set; } = true;
            
            [JsonProperty("Интервал добавления фейков (секунды)")]
            public int AdditionIntervalSeconds { get; set; } = 60;
            
            [JsonProperty("Статическое количество фейковых игроков (если динамическое отключено)")]
            public int StaticFakePlayersCount { get; set; } = 3;
            
            [JsonProperty("Максимальный онлайн (реальные + боты)")]
            public int MaxTotalPlayers { get; set; } = 50;
            
            [JsonProperty("Список фейковых ников")]
            public List<string> FakePlayerNames { get; set; } = new List<string>
            {
                "Вова",
                "Коля",
                "Саша",
                "Маша#RUST",
                "бублик"
            };
            
            [JsonProperty("Включить перехват команды players")]
            public bool InterceptPlayersCommand { get; set; } = true;
            
            [JsonProperty("Префикс для фейковых игроков")]
            public string FakePlayerPrefix { get; set; } = "[BOT]";
        }
        
        public class FakePlayerRule
        {
            [JsonProperty("Минимум реальных игроков")]
            public int MinRealPlayers { get; set; }
            
            [JsonProperty("Максимум реальных игроков")]
            public int MaxRealPlayers { get; set; }
            
            [JsonProperty("Количество фейков для добавления")]
            public int FakePlayersToAdd { get; set; }
        }
        
        public class FakePlayerData
        {
            public string Name { get; set; }
            public string SteamID { get; set; }
            public string DisplayName { get; set; }
            public int Ping { get; set; }
        }
        
        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }
                
                // Перегенерируем случайные индексы после загрузки конфига
                if (config.FakePlayerNames != null && config.FakePlayerNames.Count > 0)
                {
                    GenerateRandomizedIndices();
                }
            }
            catch
            {
                PrintWarning("Конфигурация повреждена, загружается конфигурация по умолчанию");
                LoadDefaultConfig();
            }
        }
        
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        
        #endregion
        
        #region Hooks
        
        void OnPlayerConnected(BasePlayer player)
        {
            if (config.DynamicFakePlayersEnabled)
            {
                NextTick(() => {
                    var realPlayerCount = BasePlayer.activePlayerList.Count;
                    var totalBeforeUpdate = realPlayerCount + currentActiveFakes;
                    
                    // Если превышен максимум, убираем ботов
                    if (totalBeforeUpdate > config.MaxTotalPlayers)
                    {
                        var botsToRemove = totalBeforeUpdate - config.MaxTotalPlayers;
                        currentActiveFakes = Math.Max(0, currentActiveFakes - botsToRemove);
                        Puts($"Превышен максимум онлайна. Удалено ботов: {botsToRemove}. Текущее количество ботов: {currentActiveFakes}");
                    }
                    
                    UpdateTargetFakeCount();
                    if (!config.GradualAdditionEnabled)
                    {
                        currentActiveFakes = targetFakeCount;
                        NotifyLogoPlugin();
                    }
                });
            }
        }
        
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (config.DynamicFakePlayersEnabled)
            {
                NextTick(() => {
                    UpdateTargetFakeCount();
                    if (!config.GradualAdditionEnabled)
                    {
                        currentActiveFakes = targetFakeCount;
                        NotifyLogoPlugin();
                    }
                });
            }
        }
        
        void Init()
        {
            LoadConfig();
            
            // Генерируем случайные индексы для ников
            GenerateRandomizedIndices();
            
            if (config.DynamicFakePlayersEnabled)
            {
                var maxPossibleFakes = config.FakePlayerRules.Max(x => x.FakePlayersToAdd);
                if (config.FakePlayerNames.Count < maxPossibleFakes)
                {
                    PrintWarning($"Недостаточно ников в конфигурации! Максимально нужно {maxPossibleFakes}, а указано {config.FakePlayerNames.Count}");
                }
            }
            else
            {
                if (config.FakePlayerNames.Count < config.StaticFakePlayersCount)
                {
                    PrintWarning($"Недостаточно ников в конфигурации! Нужно {config.StaticFakePlayersCount}, а указано {config.FakePlayerNames.Count}");
                }
            }
            
            // Запускаем таймер для управления фейковыми игроками
            if (config.DynamicFakePlayersEnabled)
            {
                StartFakePlayersManagement();
            }
        }
        
        void Unload()
        {
            fakeAdditionTimer?.Destroy();
        }
        
        void StartFakePlayersManagement()
        {
            fakeAdditionTimer?.Destroy();
            
            // Обновляем целевое количество фейков
            UpdateTargetFakeCount();
            
            if (config.GradualAdditionEnabled)
            {
                // Запускаем таймер для постепенного добавления/удаления фейков
                fakeAdditionTimer = timer.Every(config.AdditionIntervalSeconds, () => 
                {
                    UpdateTargetFakeCount();
                    AdjustFakePlayersGradually();
                });
            }
            else
            {
                // Мгновенно устанавливаем нужное количество фейков
                currentActiveFakes = targetFakeCount;
            }
        }
        
        void UpdateTargetFakeCount()
        {
            var realPlayerCount = BasePlayer.activePlayerList.Count;
            
            // Если нет реальных игроков, не добавляем ботов
            if (realPlayerCount == 0)
            {
                targetFakeCount = 0;
                return;
            }
            
            // Ищем подходящее правило
            targetFakeCount = 0;
            foreach (var rule in config.FakePlayerRules)
            {
                if (realPlayerCount >= rule.MinRealPlayers && realPlayerCount <= rule.MaxRealPlayers)
                {
                    targetFakeCount = Math.Min(rule.FakePlayersToAdd, config.FakePlayerNames.Count);
                    break;
                }
            }
            
            // Проверяем максимальный лимит
            var currentTotal = realPlayerCount + targetFakeCount;
            if (currentTotal > config.MaxTotalPlayers)
            {
                targetFakeCount = Math.Max(0, config.MaxTotalPlayers - realPlayerCount);
            }
        }
        
        void AdjustFakePlayersGradually()
        {
            var realPlayerCount = BasePlayer.activePlayerList.Count;
            
            // Если нет реальных игроков и есть боты, убираем по одному боту каждый тик
            if (realPlayerCount == 0 && currentActiveFakes > 0)
            {
                currentActiveFakes--;
                Puts($"Нет реальных игроков - удален фейковый игрок. Текущее количество: {currentActiveFakes}/{targetFakeCount}");
            }
            // Обычная логика добавления/удаления ботов
            else if (currentActiveFakes < targetFakeCount)
            {
                currentActiveFakes++;
                Puts($"Добавлен фейковый игрок. Текущее количество: {currentActiveFakes}/{targetFakeCount}");
            }
            else if (currentActiveFakes > targetFakeCount)
            {
                currentActiveFakes--;
                Puts($"Удален фейковый игрок. Текущее количество: {currentActiveFakes}/{targetFakeCount}");
            }
            
            // Уведомляем Logo плагин об обновлении
            NotifyLogoPlugin();
        }
        
        void NotifyLogoPlugin()
        {
            var logoPlugin = plugins.Find("Logo");
            if (logoPlugin != null)
            {
                try
                {
                    logoPlugin.Call("RedrawUI");
                }
                catch
                {
                    // Игнорируем ошибки при вызове Logo плагина
                }
            }
        }
        
        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg?.cmd?.Name == "players" && config.InterceptPlayersCommand)
            {
                var player = arg.Connection?.player as BasePlayer;
                var realPlayers = BasePlayer.activePlayerList;
                var fakeCount = GetCurrentFakePlayerCount();
                
                PrintToConsole(player, "id                name               ping snap updt posi dist");
                
                foreach (var realPlayer in realPlayers)
                {
                    var ping = realPlayer.net?.connection != null ? Network.Net.sv.GetAveragePing(realPlayer.net.connection) : UnityEngine.Random.Range(15, 100);
                    var snap = 0;
                    var updt = 0;
                    var posi = 0;
                    var dist = 0;
                    
                    var formattedName = FormatPlayerName(realPlayer.displayName);
                    PrintToConsole(player, $"{realPlayer.UserIDString} {formattedName} {ping,4} {snap,4} {updt,4} {posi,4} {dist,4}");
                }
                
                for (int i = 0; i < fakeCount; i++)
                {
                    var fakeName = GetRandomizedFakeName(i);
                    if (!string.IsNullOrEmpty(config.FakePlayerPrefix))
                    {
                        fakeName = $"{config.FakePlayerPrefix} {fakeName}";
                    }

                    var fakeSteamId = GenerateFakeSteamID(i);
                    var ping = UnityEngine.Random.Range(15, 85);
                    var snap = 0;
                    var updt = 0;
                    var posi = 0;
                    var dist = 0;
                    
                    var formattedName = FormatPlayerName(fakeName);
                    PrintToConsole(player, $"{fakeSteamId} {formattedName} {ping,4} {snap,4} {updt,4} {posi,4} {dist,4}");
                }
                
                return true;
            }
            
            return null;
        }
        
        object OnServerInformation(ServerMgr serverMgr)
        {
            return null;
        }
        
        #endregion
        
        #region API Methods
        
        // API метод для получения реального количества игроков
        [HookMethod("GetRealPlayerCount")]
        public int GetRealPlayerCount()
        {
            return BasePlayer.activePlayerList.Count;
        }
        
        // API метод для получения фейкового количества игроков
        [HookMethod("GetFakePlayerCount")]
        public int GetFakePlayerCount()
        {
            return GetCurrentFakePlayerCount();
        }
        
        // Внутренний метод для получения текущего количества фейков
        private int GetCurrentFakePlayerCount()
        {
            if (config.DynamicFakePlayersEnabled)
            {
                return currentActiveFakes;
            }
            else
            {
                var realPlayerCount = BasePlayer.activePlayerList.Count;
                
                // Если нет реальных игроков, не показываем ботов
                if (realPlayerCount == 0)
                {
                    return 0;
                }
                
                var maxPossibleFakes = Math.Min(config.StaticFakePlayersCount, config.FakePlayerNames.Count);
                var maxAllowedTotal = config.MaxTotalPlayers - realPlayerCount;
                
                return Math.Min(maxPossibleFakes, Math.Max(0, maxAllowedTotal));
            }
        }
        
        // API метод для получения общего количества игроков
        [HookMethod("GetTotalPlayerCount")]
        public int GetTotalPlayerCount()
        {
            return GetRealPlayerCount() + GetFakePlayerCount();
        }
        
        // API метод для получения списка фейковых игроков
        [HookMethod("GetFakePlayerNames")]
        public List<string> GetFakePlayerNames()
        {
            var result = new List<string>();
            var fakeCount = GetCurrentFakePlayerCount();
            
            for (int i = 0; i < fakeCount; i++)
            {
                var fakeName = GetRandomizedFakeName(i);
                if (!string.IsNullOrEmpty(config.FakePlayerPrefix))
                {
                    fakeName = $"{config.FakePlayerPrefix} {fakeName}";
                }
                result.Add(fakeName);
            }
            
            return result;
        }
        
        // API метод для получения полных данных фейковых игроков
        [HookMethod("GetFakePlayersData")]
        public List<FakePlayerData> GetFakePlayersData()
        {
            var result = new List<FakePlayerData>();
            var fakeCount = GetCurrentFakePlayerCount();
            
            for (int i = 0; i < fakeCount; i++)
            {
                var baseName = GetRandomizedFakeName(i);
                var displayName = baseName;
                
                if (!string.IsNullOrEmpty(config.FakePlayerPrefix))
                {
                    displayName = $"{config.FakePlayerPrefix} {baseName}";
                }
                
                var fakePlayer = new FakePlayerData
                {
                    Name = baseName,
                    SteamID = GenerateFakeSteamID(i),
                    DisplayName = displayName,
                    Ping = UnityEngine.Random.Range(15, 85)
                };
                
                result.Add(fakePlayer);
            }
            
            return result;
        }
        
        // API метод для получения данных конкретного фейкового игрока по индексу
        [HookMethod("GetFakePlayerData")]
        public FakePlayerData GetFakePlayerData(int index)
        {
            var fakeCount = GetCurrentFakePlayerCount();
            
            if (index < 0 || index >= fakeCount)
                return null;
                
            var baseName = GetRandomizedFakeName(index);
            var displayName = baseName;
            
            if (!string.IsNullOrEmpty(config.FakePlayerPrefix))
            {
                displayName = $"{config.FakePlayerPrefix} {baseName}";
            }
            
            return new FakePlayerData
            {
                Name = baseName,
                SteamID = GenerateFakeSteamID(index),
                DisplayName = displayName,
                Ping = UnityEngine.Random.Range(15, 85)
            };
        }
        
        // API метод для проверки является ли SteamID фейковым
        [HookMethod("IsFakeSteamID")]
        public bool IsFakeSteamID(string steamId)
        {
            var fakeCount = GetCurrentFakePlayerCount();
            
            for (int i = 0; i < fakeCount; i++)
            {
                if (GenerateFakeSteamID(i) == steamId)
                    return true;
            }
            
            return false;
        }
        
        // API метод для получения фейкового игрока по SteamID
        [HookMethod("GetFakePlayerBySteamID")]
        public FakePlayerData GetFakePlayerBySteamID(string steamId)
        {
            var fakeCount = GetCurrentFakePlayerCount();
            
            for (int i = 0; i < fakeCount; i++)
            {
                if (GenerateFakeSteamID(i) == steamId)
                {
                    return GetFakePlayerData(i);
                }
            }
            
            return null;
        }
        
        #endregion
        
        #region Commands
        
        [ChatCommand("wm")]
        void ChatCommandWM(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                SendReply(player, "У вас нет прав для использования этой команды!");
                return;
            }
            
            if (args.Length == 0)
            {
                SendReply(player, "Использование:");
                SendReply(player, "/wm info - Показать информацию о плагине");
                SendReply(player, "/wm list - Показать список фейковых игроков");
                return;
            }
            
            switch (args[0].ToLower())
            {
                case "info":
                    SendReply(player, $"Реальных игроков: {GetRealPlayerCount()}");
                    SendReply(player, $"Фейковых игроков: {GetFakePlayerCount()}");
                    SendReply(player, $"Общий онлайн: {GetTotalPlayerCount()}/{config.MaxTotalPlayers}");
                    SendReply(player, $"Режим: {(config.DynamicFakePlayersEnabled ? "Динамический" : "Статический")}");
                    if (config.DynamicFakePlayersEnabled)
                    {
                        SendReply(player, $"Текущих активных фейков: {currentActiveFakes}/{targetFakeCount}");
                        SendReply(player, $"Постепенное добавление: {(config.GradualAdditionEnabled ? "Включено" : "Отключено")}");
                    }
                    break;
                    
                case "list":
                    var fakePlayersData = GetFakePlayersData();
                    if (fakePlayersData.Count == 0)
                    {
                        SendReply(player, "Нет активных фейковых игроков");
                        return;
                    }
                    
                    SendReply(player, "Список фейковых игроков:");
                    for (int i = 0; i < fakePlayersData.Count; i++)
                    {
                        var fakePlayer = fakePlayersData[i];
                        SendReply(player, $"{i + 1}. {fakePlayer.DisplayName} (ID: {fakePlayer.SteamID})");
                    }
                    break;
                    
                default:
                    SendReply(player, "Неизвестная команда. Используйте /wm для списка команд");
                    break;
            }
        }
        
        #endregion
        
        #region Server Query Hooks
        
        object OnServerQuery()
        {
            var originalMaxPlayers = ConVar.Server.maxplayers;
            var totalPlayers = GetTotalPlayerCount();
            
            return null;
        }
        
        #endregion
        
        #region Helper Methods
        
        void GenerateRandomizedIndices()
        {
            randomizedNameIndices.Clear();
            
            // Создаем список всех индексов
            for (int i = 0; i < config.FakePlayerNames.Count; i++)
            {
                randomizedNameIndices.Add(i);
            }
            
            // Перемешиваем список индексов
            for (int i = 0; i < randomizedNameIndices.Count; i++)
            {
                var randomIndex = UnityEngine.Random.Range(i, randomizedNameIndices.Count);
                var temp = randomizedNameIndices[i];
                randomizedNameIndices[i] = randomizedNameIndices[randomIndex];
                randomizedNameIndices[randomIndex] = temp;
            }
        }
        
        string GetRandomizedFakeName(int index)
        {
            if (randomizedNameIndices.Count == 0 || randomizedNameIndices.Count != config.FakePlayerNames.Count)
            {
                GenerateRandomizedIndices();
            }
            
            if (index >= 0 && index < randomizedNameIndices.Count)
            {
                var nameIndex = randomizedNameIndices[index];
                return config.FakePlayerNames[nameIndex];
            }
            
            return config.FakePlayerNames[index % config.FakePlayerNames.Count];
        }
        
        void PrintToConsole(BasePlayer player, string message)
        {
            if (player != null)
            {
                player.ConsoleMessage(message);
            }
            else
            {
                Puts(message);
            }
        }
        
        string GenerateFakeSteamID(int index)
        {
            var baseId = 76561198000000000L;
            
            // Генерируем более разнообразное смещение, избегая повторяющихся цифр
            var seed = index + 1; // +1 чтобы избежать нулевого индекса
            var offset = 0L;
            
            // Создаем псевдослучайное, но детерминированное смещение
            var random = new System.Random(seed * 12345);
            
            // Генерируем 8-значное число без повторяющихся цифр подряд
            var digits = new List<int>();
            var lastDigit = -1;
            
            for (int i = 0; i < 8; i++)
            {
                int digit;
                do
                {
                    digit = random.Next(1, 10); // От 1 до 9, избегаем 0 в начале
                    if (i > 0 && digit == lastDigit)
                    {
                        // Если цифра повторяется, генерируем другую
                        digit = (digit % 9) + 1;
                        if (digit == lastDigit)
                            digit = (digit % 9) + 1;
                    }
                }
                while (digit == lastDigit);
                
                digits.Add(digit);
                lastDigit = digit;
                offset = offset * 10 + digit;
            }
            
            var fakeSteamId = baseId + offset;
            return fakeSteamId.ToString();
        }
        
        string FormatPlayerName(string name)
        {
            if (name.Length > 14)
            {
                return (name.Substring(0, 14) + "..").PadRight(16);
            }
            
            return name.PadRight(16);
        }
        

        
        #endregion
    }
}
