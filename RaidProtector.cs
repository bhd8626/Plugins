using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RaidProtector", "Vlad-00003", "1.6.1", ResourceId = 87)]
    [Description("Decrease damage based on permissions, time and online state of the owner.")]
    /*
    * Author info:
    *   E-mail: Vlad-00003@mail.ru
    *   Vk: vk.com/vlad_00003
    */
    class RaidProtector : RustPlugin
    {
        #region Vars‍‍‌​‍​
        private static RaidProtector Instance;
        private PluginConfig _config;
        private PluginData _data = new PluginData();
        private Dictionary<ulong, float> _informed;
        private Dictionary<ulong, BuildingOwners> _entityOwners;
        private Dictionary<ulong, BuildingOwners> _buildingOwners;
        #endregion

        #region Config ‍‍‌​‍​

        [JsonObject(MemberSerialization.OptIn)]
        private class TimeConfig
        {
            [JsonProperty("Использовать защиту в указанный промежуток времени")]
            private readonly bool _use;
            [JsonProperty("Час начала защиты")]
            private readonly int _start;
            [JsonProperty("Час снятия защиты")]
            private readonly int _end;

            private TimeSpan _startTimeSpan;
            private TimeSpan _endTimeSpan;
            //private TimeSpan _duration;

            public TimeConfig(int start, int end, bool use)
            {
                _start = start;
                _end = end;
                _use = use;
            }

            public bool IsActive
            {
                get
                {
                    if (!_use) 
                        return false;
                    var now = Instance.CurrentTime;
                    if (_startTimeSpan < _endTimeSpan)
                        return _startTimeSpan <= now && now <= _endTimeSpan;
                    return !(_endTimeSpan < now && now < _startTimeSpan);
                }
            }

            [OnDeserialized]
            internal void OnDeserializedMethod(StreamingContext context)
            {
                _startTimeSpan = new TimeSpan(_start, 0, 0);
                _endTimeSpan = new TimeSpan(_end, 0, 0);
                //_duration = (_startTimeSpan - _endTimeSpan).Duration();
            }
        }

        private class NewbieProtectionSettings
        {
            [JsonProperty("Длительность защиты (HH:mm:ss)")]
            public string ProtectionTimeStr;
            [JsonIgnore]
            public double ProtectionTime;

            #region Default Config

            public static NewbieProtectionSettings Default => 
                new NewbieProtectionSettings { ProtectionTimeStr = "12:00:00" };
            public static NewbieProtectionSettings DefaultPro => 
                new NewbieProtectionSettings { ProtectionTimeStr = "24:00:00" };
            public static NewbieProtectionSettings DefaultVip => 
                new NewbieProtectionSettings { ProtectionTimeStr = "25:00:00" };

            #endregion

            public bool IsActive(double timeSinceFirstSeen) => ProtectionTime > timeSinceFirstSeen;
        }

        private class ProtectionSettings
        {
            [JsonProperty("Множитель урона по постройкам")]
            public float Modifier;
            [JsonProperty("Настройка времени")]
            public TimeConfig TimeConfig;
            [JsonProperty("Защищать постройки когда игрок вне сети")]
            public bool Offline;

            #region Default Config‍‍‌​‍​

            public static ProtectionSettings DefaultSettings => new ProtectionSettings
            {
                Modifier = 0.7f,
                Offline = false,
                TimeConfig = new TimeConfig(23, 10, true)
            };

            public static ProtectionSettings Pro => new ProtectionSettings()
            {
                Modifier = 0.5f,
                Offline = false,
                TimeConfig = new TimeConfig(20, 12, true)
            };
            public static ProtectionSettings Vip => new ProtectionSettings
            {
                Modifier = 0.3f,
                Offline = true,
                TimeConfig = new TimeConfig(23, 10, false)
            };

            #endregion

            public bool IsActive(bool anyOnline)
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                return Modifier != 1f && (TimeConfig.IsActive || Offline && !anyOnline);
            }

            public static ProtectionSettings GetBigger(ProtectionSettings first, ProtectionSettings second, bool anyOnline)
            {
                if (!first.IsActive(anyOnline)) 
                    return second;
                if (!second.IsActive(anyOnline)) 
                    return first;
                return first.Modifier < second.Modifier ? first : second;
            }
        }

        private class BuildingBlockTierProtection
        {
            [JsonProperty("Солома")]
            private bool _twigs{get;set;}
            [JsonProperty("Дерево")]
            private bool _wood {get;set;}
            [JsonProperty("Камень")]
            private bool _stone {get;set;}
            [JsonProperty("Металл")]
            private bool _metal {get;set;}
            [JsonProperty("Бронированный")]
            private bool _topTier {get;set;}

            #region Default Config

            public static BuildingBlockTierProtection DefaultConfig => new BuildingBlockTierProtection
            {
                _twigs = true,
                _wood = true,
                _stone = true,
                _metal = true,
                _topTier = true,
            };

            #endregion

            public bool ShouldProtect(BuildingBlock block)
            {
                switch (block.currentGrade.gradeBase.type)
                {
                    case BuildingGrade.Enum.None:
                        return false;
                    case BuildingGrade.Enum.Twigs:
                        return _twigs;
                    case BuildingGrade.Enum.Wood:
                        return _wood;
                    case BuildingGrade.Enum.Stone:
                        return _stone;
                    case BuildingGrade.Enum.Metal:
                        return _metal;
                    case BuildingGrade.Enum.TopTier:
                        return _topTier;
                    case BuildingGrade.Enum.Count:
                        return false;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

        }
        
        private class MainSettings
        {
            [JsonProperty("Чат-команда для получения короткого имени префаба предмета, на который вы смотрите")]
            public string GetPrefabNameCommand;
            [JsonProperty("Чат-команда для получения списка владельцев строения, на которое вы смотрите")]
            public string GetOwnersCommand;
            [JsonProperty("Привилегия для выполнения команд")]
            public string CommandsPermission;
            [JsonProperty("Задержка перед активацией защиты при выходе из игры (в секундах)")]
            public float DelayBeforeOffline;
            [JsonProperty("Процент объектов, которые должны принадлежать игроку чтобы он считался владельцам (0-100)")]
            public int? OwnerPercentInt;
            [JsonProperty("Время, которое должно пройти после нанесения урона для пересчёта владельцев постройки")]
            public float ValidationDelay;
            [JsonProperty("Защищать строительные блоки(Стены, фундаменты, каркасы...) по типам")]
            public BuildingBlockTierProtection BuildingBlockProtection;
            [JsonProperty("Защищать двери(обычные, двойные, высокие)")]
            private bool _door;
            [JsonProperty("Защищать простые строительные блоки(высокие стены)")]
            private bool _simpleBuildingBlock;
            [JsonProperty("Список префабов, которые необходимо защищать(короткое или полное имя префаба)")]
            private List<string> _prefabs;

            [JsonIgnore]
            public float OwnerPercent;
            
            #region Default Config‍‍‌​‍​

            public static MainSettings DefaultConfig => new MainSettings
            {
                GetPrefabNameCommand = "/shortname",
                GetOwnersCommand = "/rpowners",
                CommandsPermission = nameof(RaidProtector)+".admin",
                DelayBeforeOffline = 120f,
                OwnerPercentInt = 20,
                ValidationDelay = 120f,
                BuildingBlockProtection = BuildingBlockTierProtection.DefaultConfig,
                _door = true,
                _simpleBuildingBlock = true,
                _prefabs = new List<string>
                {
                    "mining_quarry",
                    "vendingmachine.deployed",
                    "furnace.large",
                    "cupboard.tool.deployed",
                    "refinery_small_deployed"
                }
            };

            #endregion
            
            public bool CheckEntity(BaseEntity entity)
            {
                if (entity.OwnerID == 0)
                    return false;
                if (_prefabs.Contains(entity.ShortPrefabName))
                    return true;
                var buildingBlock = entity as BuildingBlock;
                if (buildingBlock)
                    return BuildingBlockProtection.ShouldProtect(buildingBlock);
                if (entity is Door && _door)
                    return true;
                if (entity is SimpleBuildingBlock && _simpleBuildingBlock)
                    return true;
                return false;
            }

            public void SetOwnershipPercent()
            {
                OwnerPercentInt = OwnerPercentInt.HasValue ? Mathf.Clamp(OwnerPercentInt.Value, 0, 100) : 20;
                OwnerPercent = OwnerPercentInt.Value / 100f;
            }
        }
        private class PluginConfig
        {
            [JsonProperty("Использовать внутриигровое время (false - реальное время на серверной машине)")]
            public bool InGameTime;
            [JsonProperty("Настройка привилегий")]
            public Dictionary<string, ProtectionSettings> Custom;
            [JsonProperty("Стандартные настройки для всех игроков")]
            public ProtectionSettings Default;
            [JsonProperty("Настройки защиты")]
            public MainSettings MainSettings;
            [JsonProperty("Формат сообщений в чате")]
            public string ChatFormat;
            [JsonProperty("Задержка между сообщениями в чат о блокировке")]
            public float MessageCooldown;
            [JsonProperty("Стандартная защита для новичков")]
            public NewbieProtectionSettings DefaultNewbieProtection;
            [JsonProperty("Полная защита для новичков по привилегиям")]
            public Dictionary<string, NewbieProtectionSettings> NewbieProtection;

            #region Default Config‍‍‌​‍​

            public static PluginConfig DefaultConfig => new PluginConfig
            {
                InGameTime = false,
                ChatFormat = "<color=#f4c842>[RaidProtector]</color> <color=#969696>{0}</color>",
                MessageCooldown = 15f,
                Default = ProtectionSettings.DefaultSettings,
                Custom = new Dictionary<string, ProtectionSettings>
                {
                    [nameof(RaidProtector)+".pro"] = ProtectionSettings.Pro,
                    [nameof(RaidProtector)+".vip"] = ProtectionSettings.Vip
                },
                MainSettings = MainSettings.DefaultConfig,
                DefaultNewbieProtection = NewbieProtectionSettings.Default,
                NewbieProtection = new Dictionary<string, NewbieProtectionSettings>
                {
                    [nameof(RaidProtector)+".newbiePro"] = NewbieProtectionSettings.DefaultVip,
                    [nameof(RaidProtector)+".newbieVip"] = NewbieProtectionSettings.DefaultPro
                }
            };

            #endregion

        }
        #endregion

        #region Data ‍‍‌​‍​
        [ProtoContract]
        private class PlayerData
        {
            [ProtoMember(2)]
            public DateTime? FirstSeen;
            [ProtoMember(3)]
            public DateTime? LastSeen;

            public void OnConnected()
            {
                if (FirstSeen != null)
                    return;
                FirstSeen = DateTime.UtcNow;
            }

            public void OnDisconnected()
            {
                LastSeen = DateTime.UtcNow;
            }
        }
        
        [ProtoContract]
        private class PluginData
        {
            [ProtoMember(1)]
            public Dictionary<ulong, PlayerData> PlayersData = new Dictionary<ulong, PlayerData>();

            public PlayerData GetOrCreate(ulong userId)
            {
                PlayerData data;
                if (PlayersData.TryGetValue(userId, out data))
                    return data;
                return PlayersData[userId] = new PlayerData
                {
                    FirstSeen = null,
                    LastSeen = null
                };
            }
        }

        #endregion

        #region Config and data initialization‍‍‌​‍​

        #region Config‍‍‌​‍​

        private bool ShouldUpdateConfig()
        {
            var res = false;
            //Version < 1.1.0
            if (Config["Настройки защиты", "Задержка перед активацией защиты при выходе из игры (в секундах)"] == null)
            {
                PrintWarning("Option \"Delay before offline\" added to the config");
                _config.MainSettings.DelayBeforeOffline = 120f;
                res = true;
            }
            //Version < 1.3.0
            if (Config["Настройки защиты",
                    "Время, которое должно пройти после нанесения урона для пересчёта владельцев постройки"] == null)
            {
                PrintWarning("Option \"Delay before validation\" added to the config");
                _config.MainSettings.ValidationDelay = 120f;
                res = true;
            }

            //version < 1.4.0
            if (Config["Использовать внутриигровое время (false - реальное время на серверной машине)"] == null)
            {
                PrintWarning("Option \"Use in-game time\" was added to the config");
                _config.InGameTime = false;
                res = true;
            }
            //version < 1.5.0
            if (_config.NewbieProtection == null || _config.DefaultNewbieProtection == null)
            {
                _config.DefaultNewbieProtection = NewbieProtectionSettings.Default;
                _config.NewbieProtection = new Dictionary<string, NewbieProtectionSettings>
                {
                    [nameof(RaidProtector) + ".newbiePro"] = NewbieProtectionSettings.DefaultVip,
                    [nameof(RaidProtector) + ".newbieVip"] = NewbieProtectionSettings.DefaultPro
                };
                PrintWarning("Newbie protection settings were added to the config");
                res = true;
            }
            if (!_config.MainSettings.OwnerPercentInt.HasValue)
            {
                _config.MainSettings.OwnerPercentInt = 20;
                PrintWarning("Required ownership percent was added to the config");
                _config.MainSettings.GetOwnersCommand = "/rpowners";
                _config.MainSettings.CommandsPermission = nameof(RaidProtector) + ".admin";
                PrintWarning("Ownership check command and permission for commands were added to config");
                res = true;
            }
            //Version < 1.6.0
            if (_config.MainSettings.BuildingBlockProtection == null)
            {
                _config.MainSettings.BuildingBlockProtection = BuildingBlockTierProtection.DefaultConfig;
                PrintWarning("Building block types added to config file");
                res = true;
            }

            return res;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за приобретение плагина на сайте RustPlugin.ru. Если вы приобрели этот плагин на другом ресурсе знайте - это лишает вас гарантированных обновлений!");
            _config = PluginConfig.DefaultConfig;
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            if(ShouldUpdateConfig())
                SaveConfig();

            if(!string.IsNullOrEmpty(_config.MainSettings.CommandsPermission))
                permission.RegisterPermission(_config.MainSettings.CommandsPermission, this);

            foreach (var perm in _config.Custom.Keys)
                permission.RegisterPermission(perm, this);

            foreach (var perm in _config.NewbieProtection)
            {
                TimeSpan protectionTime;
                if (!TimeSpan.TryParse(perm.Value.ProtectionTimeStr, out protectionTime))
                {
                    PrintError("Failed to parse {0} as newbie protection time for {1}.",perm.Value.ProtectionTimeStr, perm.Key);
                    continue;
                }
                perm.Value.ProtectionTime = protectionTime.TotalSeconds;
                permission.RegisterPermission(perm.Key, this);
            }

            TimeSpan defaultNewbieProtection;
            if (TimeSpan.TryParse(_config.DefaultNewbieProtection.ProtectionTimeStr, out defaultNewbieProtection))
                _config.DefaultNewbieProtection.ProtectionTime = defaultNewbieProtection.TotalSeconds;
            else
                PrintError("Failed to parse {0} as newbie protection time for default protection.",
                    _config.DefaultNewbieProtection.ProtectionTimeStr);
            _config.MainSettings.SetOwnershipPercent();
            LoadData();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
        
        #region Data‍‍‌​‍​
        private void SaveData()
        {
            ProtoStorage.Save(_data, Title);
        }
        private void LoadData()
        {
            _data = ProtoStorage.Load<PluginData>(Name);
            if (_data == null)
            {
                _data = new PluginData();
                SaveData();
            }
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(Title)) 
                return;

            try
            {
                var oldData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, DateTime>>(Title);
                if (oldData == null) 
                    return;

                foreach (var kvp in oldData)
                {
                    var data = _data.GetOrCreate(kvp.Key);
                    data.FirstSeen = SaveRestore.SaveCreatedTime;
                    data.LastSeen = kvp.Value;
                }

                Interface.Oxide.DataFileSystem.WriteObject<object>(Title, null);
                SaveData();
            }
            catch (Exception ex)
            {
                PrintError("Failed to load old data file, ignoring. Exception: {0}",ex);
            }
        }

        #endregion
        
        #endregion

        #region Initialization and quitting‍‍‌​‍​

        private void Init()
        {
            Instance = this;
            cmd.AddChatCommand(_config.MainSettings.GetPrefabNameCommand.Replace("/", string.Empty), this, GetShortName);
            cmd.AddChatCommand(_config.MainSettings.GetOwnersCommand.Replace("/", string.Empty), this, GetBuildingOwnership);
            _informed = PoolEx.GetDict<ulong, float>();
            _entityOwners = PoolEx.GetDict<ulong, BuildingOwners>();
            _buildingOwners = PoolEx.GetDict<ulong, BuildingOwners>();
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerDisconnected(player, "PluginUnloading");
            OnServerSave();
            _data = null;
            _informed.Clear();
			_entityOwners.Clear();
			_buildingOwners.Clear();

        } 
        private void OnServerSave()
        {
            SaveData();
        }

        #endregion

        #region Localization‍‍‌​‍​

        private void SendResponse(BasePlayer player, string langKey, params object[] args)
        {
            var format = GetMsg(langKey, player);
            format = args.Length == 0 ? format : string.Format(format, args);
            player.ChatMessage(string.Format(_config.ChatFormat, format));
        }
        private string GetMsg(string key, BasePlayer player) => lang.GetMessage(key, this, player?.UserIDString);
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["Protected"] = "This building <color=#770000>is protected</color>  and will receive only <color=#777700>{0}%</color> of damage",
                ["Protected anti"] = "Damage for this building <color=#007700>is increased</color> to <color=#777700>{0}%</color>",
                ["Name found"] = "Shortname for entity \"{0}\"",
                ["No item found"] = "No entity can be found in front of you!",
                ["Ownership"] = "Building {0}, owners:\n{1}\n{2}",
                ["OwnerPercent"] = "{0}: {1:P}",
                ["OwnerAuthed"] = "{0}: Authorized in the tool cupboard\\Only owner",
                ["ProtectionStatus"] = "Protection is {0}, damage percent: {1:P}",
                ["Active"] = "<color=green>ACTIVE</color>",
                ["Inactive"] = "<color=red>INACTIVE</color>",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command"
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                ["Protected"] = "Эта постройка <color=#770000>защищена</color> и будет получать только <color=#777700>{0}%</color> от урона",
                ["Protected anti"] = "Урон по данной постройке <color=#007700>увеличен</color> на <color=#777700>{0}%</color>",
                ["Name found"] = "Короткое имя предмета \"{0}\"",
                ["No item found"] = "Перед вам не обнаружено предмета!",
                ["Ownership"] = "Строение {0}, владельцы :\n{1}\n{2}",
                ["OwnerPercent"] = "{0}: {1:P}",
                ["OwnerAuthed"] = "{0}: Авторизован в шкафу\\Единственный владелец",
                ["ProtectionStatus"] = "Защита {0}, процент получаемого урона: {1:P}",
                ["Active"] = "<color=green>АКТИВНА</color>",
                ["Inactive"] = "<color=red>НЕ АКТИВНА</color>",
                ["NotAllowed"] = "У вас нет разрешение на использование команды '{0}'"
            }, this, "ru");
        }
        #endregion

        #region Chat commands‍‍‌​‍​

        private bool HasAdminPermission(BasePlayer player)
        {
            if (player == null || !player)
                return false;
            return string.IsNullOrEmpty(_config.MainSettings.CommandsPermission) ||
                   permission.UserHasPermission(player.UserIDString, _config.MainSettings.CommandsPermission);
        }

        private void GetBuildingOwnership(BasePlayer player, string command, string[] args)
        {
            if (!HasAdminPermission(player))
            {
                player.ChatMessage(string.Format(GetMsg("NotAllowed",player),command));
                return;
            }
            RaycastHit rayHit;
            var targetEntity =  Physics.Raycast(player.eyes.HeadRay(), out rayHit, 100) ? rayHit.GetEntity() : null;
            if (targetEntity)
            {
                var data = GetProtectionData(targetEntity);
                var msg = GetMsg("Ownership", player);
                var owners = string.Join("\n", data.GetFormattedOwners(player));
                float mod;
                var protection = string.Format(GetMsg("ProtectionStatus", player),
                    GetMsg(data.IsActive(out mod) ? "Active" : "Inactive", player), mod);
                player.ChatMessage(string.Format(msg,data.BuildingId,owners,protection));
                
                return;
            }
            player.ChatMessage(GetMsg("No item found", player));
        }

        private void GetShortName(BasePlayer player, string command, string[] args)
        { 
            if (!HasAdminPermission(player))
            {
                player.ChatMessage(string.Format(GetMsg("NotAllowed",player),command));
                return;
            }
            RaycastHit rayHit;
            var targetEntity =  Physics.Raycast(player.eyes.HeadRay(), out rayHit, 100) ? rayHit.GetEntity() : null;
            if (targetEntity)
            {
                string message = string.Format(GetMsg("Name found", player), targetEntity.ShortPrefabName);
                player.ChatMessage(message);
                player.ConsoleMessage(message);
                return;
            }
            player.ChatMessage(GetMsg("No item found", player));
        }
        #endregion

        #region Owners data‍‍‌​‍​

        private BuildingOwners GetProtectionData(BaseEntity entity)
        {
            BuildingOwners data;
            var building = (entity as DecayEntity)?.GetBuilding();
            if (building != null)
            {
                if (_buildingOwners.TryGetValue(building.ID, out data) && data.IsValid)
                    return data;
                data = new BuildingOwners(building);
                _buildingOwners[building.ID] = data;
                return data;
            }

            if (_entityOwners.TryGetValue(entity.net.ID.Value, out data) && data.IsValid)
                return data;
            data = new BuildingOwners(entity);
            _buildingOwners[entity.net.ID.Value] = data;
            return data;

        }
        private void OnBuildingSplit(BuildingManager.Building building, uint newId)
        {
            BuildingOwners data;
            if (!_buildingOwners.TryGetValue(building.ID, out data))
                return;
            _buildingOwners[newId] = data;
        }

        private struct BuildingOwner
        {
            public readonly ulong PlayerId;
            public readonly string PlayerIdString;
            public readonly bool FromCupboard;
            public readonly float Percent;

            public BuildingOwner(ulong playerId)
            {
                PlayerId = playerId;
                PlayerIdString = playerId.ToString();
                FromCupboard = true;
                Percent = float.MinValue;
            }

            public BuildingOwner(ulong playerId, float percent)
            {
                PlayerId = playerId;
                PlayerIdString = playerId.ToString();
                FromCupboard = false;
                Percent = percent;
            }
            
        }
        private struct BuildingOwners
        {
            public bool IsValid => _nextCheckTime > Time.realtimeSinceStartup;

            public bool IsOwner(ulong playerid = 0)
            {
                return _owners.Any(x => x.PlayerId == playerid);
            }

            public IEnumerable<string> GetFormattedOwners(BasePlayer forPlayer)
            {
                foreach (var owner in _owners)
                {
                    var name = Instance.covalence.Players.FindPlayerById(owner.PlayerIdString)?.Name 
                               ?? owner.PlayerIdString;
                    if (owner.FromCupboard)
                        yield return string.Format(Instance.GetMsg("OwnerAuthed", forPlayer), name);
                    else
                        yield return string.Format(Instance.GetMsg("OwnerPercent", forPlayer), name, owner.Percent);
                }
            }
            
            public bool IsActive(out float modifier)
            {
                var anyOnline = Instance.AnyOnline(_owners);
                var settings = Instance.GetPermission(anyOnline,_owners);
                if (Instance.IsNewbieProtectionEnabled(_owners))
                {
                    modifier = 0;
                    return true;
                }
                modifier = settings.Modifier;
                return settings.IsActive(anyOnline);
            }

            private readonly BuildingOwner[] _owners;
            private readonly float _nextCheckTime;

            public string BuildingId { get; }

            public BuildingOwners(BuildingManager.Building building)
            {
                _owners = Instance.GetOwners(building).ToArray();
                _nextCheckTime = Time.realtimeSinceStartup + Instance._config.MainSettings.ValidationDelay;
                BuildingId = $"Building [{building.ID}]";
            }

            public BuildingOwners(BaseEntity entity)
            {
                _owners = new[] {new BuildingOwner(entity.OwnerID)};
                _nextCheckTime = Time.realtimeSinceStartup + Instance._config.MainSettings.ValidationDelay;
                BuildingId = entity.ToString();
            }
        }

        #endregion

        #region Oxide Hooks (Player logging)‍‍‌​‍​

        private void OnNewSave(string filename)
        {
            _data.PlayersData.Clear();
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            _data.GetOrCreate(player.userID).OnConnected();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            _data.GetOrCreate(player.userID).OnDisconnected();
        }

        #endregion

        #region Oxide Hooks (Damage Protection)‍‍‌​‍​

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!_config.MainSettings.CheckEntity(entity))
                return;

            var data = GetProtectionData(entity);
            float damageModifier;
            if (!data.IsActive(out damageModifier))
                return;
            
            var initiatorPlayer = hitInfo.InitiatorPlayer;
            if (initiatorPlayer)
            {
                if (data.IsOwner(initiatorPlayer.userID))
                    return;

                hitInfo.damageTypes.ScaleAll(damageModifier);

                if (!ShouldReceiveMessage(initiatorPlayer)) 
                    return;
                
                if (damageModifier < 1)
                    SendResponse(initiatorPlayer, "Protected", damageModifier * 100f);
                else
                    SendResponse(initiatorPlayer, "Protected anti", (damageModifier - 1f) * 100f);
                return;
            }
            if (hitInfo.Initiator?.name == "assets/bundled/prefabs/fireball_small.prefab")
                hitInfo.damageTypes.ScaleAll(damageModifier);
        }
        #endregion

        #region Helpers‍‍‌​‍​

        private TimeSpan CurrentTime
        {
            get
            {
                if (!Instance._config.InGameTime)
                    return DateTime.Now.TimeOfDay;
                var instance = TOD_Sky.Instance;
                return !instance ? TimeSpan.Zero : instance.Cycle.DateTime.TimeOfDay;
            }
        }

        private bool ShouldReceiveMessage(BasePlayer player)
        {
            float time;
            if (_informed.TryGetValue(player.userID, out time))
            {
                if (time > Time.realtimeSinceStartup)
                    return false;
            }

            _informed[player.userID] = Time.realtimeSinceStartup + _config.MessageCooldown;
            return true;
        }

        #region Owner Getters‍‍‌​‍​
        
        private IEnumerable<BuildingOwner> GetOwners(BuildingManager.Building building)
        {
            if(building.HasBuildingPrivileges())
            {
                foreach (var authorizedPlayer in building.buildingPrivileges.SelectMany(cupboard => cupboard.authorizedPlayers))
                {
                    yield return new BuildingOwner(authorizedPlayer.userid);
                }
            }
            if(!building.HasDecayEntities())
                yield break;
            var decayEntities = Pool.GetList<DecayEntity>();
            decayEntities.AddRange(building.decayEntities.Where(x => _config.MainSettings.CheckEntity(x)).ToArray());
            float total = decayEntities.Count;
            var owners = decayEntities.GroupBy(x => x.OwnerID).Select(x => new
            {
                userId = x.Key,
                percent = x.Count() / total
            });
            foreach (var owner in owners)
                if (owner.percent >= _config.MainSettings.OwnerPercent)
                    yield return new BuildingOwner(owner.userId, owner.percent);
            Pool.FreeList(ref decayEntities);
        }

        #endregion

        #region Online checks‍‍‌​‍​
        
        private bool AnyOnline(params BuildingOwner[] players)
        {
            //var reply = 0;
            //var online = BasePlayer.activePlayerList.Select(x => x.userID);
            //return players.Intersect(online).Any();
            return players.Any(IsOnline);
        }

        private bool IsOnline(BuildingOwner owner)
        {
            var isOnline = BasePlayer.FindByID(owner.PlayerId) != null;
            if (isOnline)
                return true;

            var data = _data.GetOrCreate(owner.PlayerId);
            if (!data.LastSeen.HasValue)
                return false;
            return data.LastSeen.Value.AddSeconds(_config.MainSettings.DelayBeforeOffline) > DateTime.UtcNow;
        }
        
        #endregion

        #region Permissions‍‍‌​‍​

        private ProtectionSettings GetPermission(bool anyOnline, params BuildingOwner[] owners)
        {
            return owners.Select(x => GetPermission(x.PlayerIdString, anyOnline))
                .Aggregate(_config.Default, (x,y) => ProtectionSettings.GetBigger(x,y,anyOnline));
        }

        private ProtectionSettings GetPermission(string player, bool anyOnline)
        {
            var available = _config.Custom
                .Where(x => permission.UserHasPermission(player, x.Key)).Select(x => x.Value);
            return available.Aggregate(_config.Default, (x,y) => ProtectionSettings.GetBigger(x,y,anyOnline));
        }

        private bool IsNewbieProtectionEnabled(params BuildingOwner[] players) => players.Any(IsNewbieProtectionEnabled);

        private bool IsNewbieProtectionEnabled(BuildingOwner owner)
        {
            var data = _data.GetOrCreate(owner.PlayerId);
            if (!data.FirstSeen.HasValue)
                return false;
            var timeSinceFirstSeen = (DateTime.UtcNow - data.FirstSeen).Value.TotalSeconds;
            var available = _config.NewbieProtection
                .Where(x => permission.UserHasPermission(owner.PlayerIdString, x.Key)).Select(x => x.Value);
            return _config.DefaultNewbieProtection.IsActive(timeSinceFirstSeen) ||
                   available.Any(x => x.IsActive(timeSinceFirstSeen));
        }

        #endregion

        #region Pool Additions‍‍‌​‍​

        public static class PoolEx
		{
			public static Dictionary<TKey, TValue> GetDict<TKey, TValue>()
			{
				return Pool.Get<Dictionary<TKey, TValue>>() ?? new Dictionary<TKey, TValue>();
			}

			public static void FreeDict<TKey, TValue>(ref Dictionary<TKey, TValue> dict)
			{
				if (dict != null)
				{
					dict.Clear();
					dict = null;
				}
			}
		}


        #endregion
       
        #endregion
    }
}
