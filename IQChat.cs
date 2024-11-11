using UnityEngine;
using Object = System.Object;
using System.Collections.Generic;
using Oxide.Core;
using Pool = Facepunch.Pool;
using CompanionServer;
using System.Text.RegularExpressions;
using System;
using Oxide.Core.Plugins;
using System.Text;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using ConVar;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using UnityEngine.Networking;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("IQChat", "Mercury", "2.65.46")]
    [Description("The most pleasant chat for your server from the IQ system")]
    class IQChat : RustPlugin
    {
        [ChatCommand("adminalert")]
        private void AdminAlertChatCommand(BasePlayer Sender, String cmd, String[] args)
        {
            if (!permission.UserHasPermission(Sender.UserIDString, PermissionAlert)) return;
            Alert(Sender, args, true);
        }
        void OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            DiscordLoggCommand(player, command, args);
        }
        
        
        [ConsoleCommand("alert")]
        private void AlertConsoleCommand(ConsoleSystem.Arg args)
        {
            BasePlayer Sender = args.Player();
            if (Sender != null)
                if (!permission.UserHasPermission(Sender.UserIDString, PermissionAlert)) return;

            Alert(Sender, args.Args, false);
        }

        void IQRankSetRank(BasePlayer.EncryptedValue<UInt64> userID, string RankKey) => IQRankSetRank(userID.Get(), RankKey);
        private void DrawUI_IQChat_OpenDropList(BasePlayer player, TakeElementUser ElementType, Int32 Page = 0)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_OpenDropList");
            if (Interface == null) return;

            if (!LocalBase.ContainsKey(player)) return;

            String Title = String.Empty;
            String Description = String.Empty;
            List<Configuration.ControllerParameters.AdvancedFuncion> InfoUI = new List<Configuration.ControllerParameters.AdvancedFuncion>();

            switch (ElementType)
            {
                case TakeElementUser.MultiPrefix:
                case TakeElementUser.Prefix:
                    {
                        InfoUI = LocalBase[player].ElementsPrefix;
                        Title = GetLang("IQCHAT_CONTEXT_SLIDER_PREFIX_TITLE", player.UserIDString);
                        Description = GetLang("IQCHAT_CONTEXT_DESCRIPTION_PREFIX", player.UserIDString);
                        break;
                    }
                case TakeElementUser.Nick:
                    {
                        InfoUI = LocalBase[player].ElementsNick;
                        Title = GetLang("IQCHAT_CONTEXT_SLIDER_NICK_COLOR_TITLE", player.UserIDString);
                        Description = GetLang("IQCHAT_CONTEXT_DESCRIPTION_NICK", player.UserIDString);
                        break;
                    }
                case TakeElementUser.Chat:
                    {
                        InfoUI = LocalBase[player].ElementsChat;
                        Title = GetLang("IQCHAT_CONTEXT_SLIDER_MESSAGE_COLOR_TITLE", player.UserIDString);
                        Description = GetLang("IQCHAT_CONTEXT_DESCRIPTION_CHAT", player.UserIDString);
                        break;
                    }
                case TakeElementUser.Rank:
                    {
                        InfoUI = LocalBase[player].ElementsRanks;
                        Title = GetLang("IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE", player.UserIDString);
                        Description = GetLang("IQCHAT_CONTEXT_DESCRIPTION_RANK", player.UserIDString);
                        break;
                    }
                default:
                    break;
            }

            //  if (InfoUI == null || InfoUI.Count == 0) return;

            Interface = Interface.Replace("%TITLE%", Title);
            Interface = Interface.Replace("%DESCRIPTION%", Description);

            String CommandRight = InfoUI.Skip(9 * (Page + 1)).Count() > 0 ? $"newui.cmd droplist.controller page.controller {ElementType} + {Page}" : String.Empty;
            String CommandLeft = Page != 0 ? $"newui.cmd droplist.controller page.controller {ElementType} - {Page}" : String.Empty;
		   		 		  						  	   		  		 			  		  		  			 		   			
            Interface = Interface.Replace("%NEXT_BTN%", CommandRight);
            Interface = Interface.Replace("%BACK_BTN%", CommandLeft);

            Interface = Interface.Replace("%COLOR_RIGHT%", String.IsNullOrWhiteSpace(CommandRight) ? "1 1 1 0.1" : "1 1 1 1");
            Interface = Interface.Replace("%COLOR_LEFT%", String.IsNullOrWhiteSpace(CommandLeft) ? "1 1 1 0.1" : "1 1 1 1");

            CuiHelper.DestroyUi(player, "OpenDropList");
            CuiHelper.AddUi(player, Interface);

            Int32 Count = 0;
            Int32 X = 0, Y = 0;
            foreach (Configuration.ControllerParameters.AdvancedFuncion Info in InfoUI.Skip(9 * Page).Take(9))
            {
                DrawUI_IQChat_OpenDropListArgument(player, ElementType, Info, X, Y, Count);

                if (ElementType == TakeElementUser.MultiPrefix && UserInformation[player.userID].Info.PrefixList.Contains(Info.Argument))
                    DrawUI_IQChat_OpenDropListArgument(player, Count);
		   		 		  						  	   		  		 			  		  		  			 		   			
                Count++;
                X++;
                if (X == 3)
                {
                    X = 0;
                    Y++;
                }
            }
        }
        private void OnServerInitialized()
        {
            _ = this;
            
            if(IQFakeActive && config.ReferenceSetting.IQFakeActiveSettings.UseIQFakeActive)
                if (IQFakeActive.Version < new Oxide.Core.VersionNumber(2, 0, 0))
                {
                    PrintWarning(LanguageEn
                        ? "You have an outdated version of the IQFakeActive plugin, the plugin cannot access the API, update the IQIQFakeActiveChat plugin to version 2.0.0 or higher"
                        : "У вас устаревшая версия плагина IQFakeActive, плагин не может получить доступ к API, обновите плагин IQFakeActive до версии 2.0.0 или выше");
                    NextTick(() => { Interface.Oxide.UnloadPlugin(Name); });
                    return;
                }

            timer.Once(3f, () => 
            {
                if (!TranslationAPI && config.ReferenceSetting.translationApiSettings.useTranslationApi)
                {
                    PrintWarning(LanguageEn
                        ? "You have TranslationAPI support enabled, but the plugin is not installed. Download and install the plugin - https://umod.org/plugins/translation-api"
                        : "У вас включена поддержка TranslationAPI, но не установлен плагин. Скачайте и установите плагин - https://umod.org/plugins/translation-api");
                    NextTick(() => { Interface.Oxide.UnloadPlugin(Name); });
                    return;
                }
            });

            _imageUI = new ImageUI();
            _imageUI.DownloadImage();

            MigrateDataToNoob();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                UserConnecteionData(player);

            RegisteredPermissions();
            BroadcastAuto();

            CheckValidateUsers();

            if (!config.ControllerMessages.Formatting.ControllerNickname.UseNickController)
                Unsubscribe("OnUserConnected");

        }
        String API_GET_DEFAULT_MESSAGE_COLOR() => config.ControllerConnect.SetupDefaults.MessageDefault;
        private String GetClanTag(UInt64 playerID)
        {
            if (!Clans) return String.Empty;
            if (!config.ReferenceSetting.ClansSettings.UseClanTag) return String.Empty;
            String ClanTag = (String)Clans?.CallHook("GetClanOf", playerID);
            if(String.IsNullOrWhiteSpace(ClanTag)) return String.Empty;
            String tagClan = GetLang("CLANS_SYNTAX_PREFIX", playerID.ToString(), ClanTag);
            
            Object resultColorMessage = IsGradientColorValue(config.ReferenceSetting.ClansSettings.colorTag);
            if (resultColorMessage is List<String> gradientColors)
                tagClan = ApplyGradientToText(tagClan, gradientColors);
            else tagClan = $"<color={config.ReferenceSetting.ClansSettings.colorTag}>{tagClan}</color>";

            return tagClan;
        }
		   		 		  						  	   		  		 			  		  		  			 		   			
        String API_GET_PREFIX(BasePlayer.EncryptedValue<UInt64> ID) => API_GET_PREFIX(ID.Get());
        
        [ChatCommand("unmutevoice")]
        void UnMuteVoiceCustomChat(BasePlayer Moderator, string cmd, string[] arg)
        {
            if (!permission.UserHasPermission(Moderator.UserIDString, PermissionMute)) return;
            if (arg == null || arg.Length != 1 || arg.Length > 1)
            {
                ReplySystem(Moderator, LanguageEn ? "Invalid syntax, please use : unmutevoice Steam64ID" : "Неверный синтаксис,используйте : unmutevoice Steam64ID");
                return;
            }
            string NameOrID = arg[0];
            
            if (IsFakeUser(NameOrID))
            {
                List<FakePlayer> playerList = GetCombinedPlayerList();
                if (playerList != null)
                {
                    FakePlayer fakeUser = playerList.FirstOrDefault(x => x.userId.Equals(NameOrID) || x.displayName.ToLower().Contains(NameOrID.ToLower()));
                    if (fakeUser != null)
                        UnmutePlayer(null, MuteType.Chat, Moderator, false, true, fakeUser.userId);
                }
                return;
            }
            
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (!Info.MuteInfo.IsMute(MuteType.Voice))
                        {
                            ReplySystem(Moderator, LanguageEn ? "The player does not have a chat lock" : "У игрока нет блокировки чата");
                            return;
                        }
                        
                        if (!Moderator.IsAdmin && Moderator.userID == Steam64ID)
                        {
                            ReplySystem(Moderator, GetLang("FUNC_MESSAGE_UNMUTE_YOURSELF", Moderator.UserIDString));
                            return;
                        }

                        Info.MuteInfo.UnMute(MuteType.Voice);
                        ReplySystem(Moderator, LanguageEn ? "You have unblocked the offline chat to the player" : "Вы разблокировали чат offline игроку");
                        return;
                    }
                    else
                    {
                        ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }
            UnmutePlayer(target, MuteType.Voice, Moderator, false, true);
        }
        private void DrawUI_IQChat_Slider_Update_Argument(BasePlayer player, TakeElementUser ElementType)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Slider_Update_Argument");
            User Info = UserInformation[player.userID];
            if (Info == null || Interface == null) return;

            String Argument = String.Empty;
            String Name = String.Empty;
            String Parent = String.Empty;
            
            switch (ElementType)
            {
                case TakeElementUser.Prefix:
                    Argument = Info.Info.Prefix;
                    Parent = "SLIDER_PREFIX";
                    Name = "ARGUMENT_PREFIX";
                    break;
                case TakeElementUser.Nick:

                    String DisplayName = player.displayName;
                    String ColorNickPlayer = Info.Info.ColorNick;
                    Object resultColorNick = IsGradientColorValue(ColorNickPlayer);
                    if (resultColorNick is List<String> gradientColors)
                        DisplayName = ApplyGradientToText(DisplayName, gradientColors);
                    else DisplayName = $"<color={ColorNickPlayer}>{DisplayName}</color>";
                    
                    Argument = DisplayName;
                    Parent = "SLIDER_NICK_COLOR";
                    Name = "ARGUMENT_NICK_COLOR";
                    break;
                case TakeElementUser.Chat:
                    String ChatMessage = GetLang("IQCHAT_CONTEXT_NICK_DISPLAY_MESSAGE", player.UserIDString);
                    String ColorChat = Info.Info.ColorMessage;
                    Object resultColorChat = IsGradientColorValue(ColorChat);
                    if (resultColorChat is List<String> gradientChatColors)
                        ChatMessage = ApplyGradientToText(ChatMessage, gradientChatColors);
                    else ChatMessage = $"<color={ColorChat}>{ChatMessage}</color>";
                    
                    Argument = ChatMessage;
                    Parent = "SLIDER_MESSAGE_COLOR";
                    Name = "ARGUMENT_MESSAGE_COLOR";
                    break;
                case TakeElementUser.Rank:
                    Argument = IQRankGetNameRankKey(Info.Info.Rank) ?? GetLang("IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE_NULLER", player.UserIDString);
                    Parent = "SLIDER_IQRANK";
                    Name = "ARGUMENT_RANK";
                    break;
                default:
                    break;
            }
		   		 		  						  	   		  		 			  		  		  			 		   			
            String Pattern = @"</?size.*?>";
            String ArgumentRegex = Regex.IsMatch(Argument, Pattern) ? Regex.Replace(Argument, Pattern, "") : Argument;
            Interface = Interface.Replace("%ARGUMENT%", ArgumentRegex);
            Interface = Interface.Replace("%PARENT%", Parent);
            Interface = Interface.Replace("%NAME%", Name);

            CuiHelper.DestroyUi(player, Name);
            CuiHelper.AddUi(player, Interface);

        }
        private Dictionary<String, TranslationState> saveTranslate = new();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();

                if (config.OtherSetting.renameSteamApiKey == null)
                    config.OtherSetting.renameSteamApiKey = "";
                
                if (config.ControllerMessages.Formatting.BadWords == null ||
                    config.ControllerMessages.Formatting.BadWords.Count == 0)
                {
                    config.ControllerMessages.Formatting.BadWords = LanguageEn
                        ? new Dictionary<String, Boolean>()
                            { ["fucking"] = false, ["sucking"] = false, ["fuckyou"] = false, ["fuck"] = true }
                        : new Dictionary<String, Boolean>()
                            { ["бля"] = false, ["сука"] = false, ["говно"] = false, ["нахуй"] = true };
                }

                if (config.ControllerMessages.Formatting.ControllerNickname.BadNicks == null ||
                    config.ControllerMessages.Formatting.ControllerNickname.BadNicks.Count == 0)
                {
                    config.ControllerMessages.Formatting.ControllerNickname.BadNicks = LanguageEn
                        ? new Dictionary<String, Boolean>()
                            { ["Admin"] = true, ["Owner"] = true, ["Moderator"] = true, ["Helper"] = true }
                        : new Dictionary<String, Boolean>()
                            { ["Админ"] = true, ["Модератор"] = true, ["Модер"] = true, ["Хелпер"] = true };
                }

                if (config.ReferenceSetting.playerRanksSettings.colorTag == null)
                    config.ReferenceSetting.playerRanksSettings.colorTag =
                        "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00";
                
                if (config.ReferenceSetting.ClansSettings.colorTag == null)
                    config.ReferenceSetting.ClansSettings.colorTag =
                        "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00";
                
                if (config.ReferenceSetting.XLevelsSettings.colorTag == null)
                    config.ReferenceSetting.XLevelsSettings.colorTag =
                        "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00";
                
                if (config.ReferenceSetting.skillTreeSettings.colorTag == null)
                    config.ReferenceSetting.skillTreeSettings.colorTag =
                        "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00";

                if (config.ReferenceSetting.xPrisonSettings == null)
                {
                    config.ReferenceSetting.xPrisonSettings = new Configuration.ReferenceSettings.XPrison()
                    {
                        UseXPrison = false,
                        colorTag = "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00",
                    };
                }
                
                if (config.ReferenceSetting.xPrisonSettings.colorTag == null)
                    config.ReferenceSetting.xPrisonSettings.colorTag =
                        "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00";
            }
            catch
            {
                PrintWarning(LanguageEn
                    ? $"Error #132 read configuration 'oxide/config/{Name}', create a new configuration!!"
                    : $"Ошибка #132 чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");

                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        
                private void DrawUI_IQChat_Context(BasePlayer player)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Context");
            User Info = UserInformation[player.userID];
            Configuration.ControllerParameters ControllerParameter = config.ControllerParameter;
            if (Info == null || ControllerParameter == null || Interface == null) return;

            String BackgroundStatic = IQRankSystem && config.ReferenceSetting.IQRankSystems.UseRankSystem ? "UI_IQCHAT_CONTEXT_RANK" : "UI_IQCHAT_CONTEXT_NO_RANK";
            
            Interface = Interface.Replace("%IMG_BACKGROUND%", _imageUI.GetImage(BackgroundStatic));
            Interface = Interface.Replace("%TITLE%", GetLang("IQCHAT_CONTEXT_TITLE", player.UserIDString));
            Interface = Interface.Replace("%SETTING_ELEMENT%", GetLang("IQCHAT_CONTEXT_SETTING_ELEMENT_TITLE", player.UserIDString));
            Interface = Interface.Replace("%INFORMATION%", GetLang("IQCHAT_CONTEXT_INFORMATION_TITLE", player.UserIDString));
            Interface = Interface.Replace("%SETTINGS%", GetLang("IQCHAT_CONTEXT_SETTINGS_TITLE", player.UserIDString));
            Interface = Interface.Replace("%SETTINGS_PM%", GetLang("IQCHAT_CONTEXT_SETTINGS_PM_TITLE", player.UserIDString));
            Interface = Interface.Replace("%SETTINGS_ALERT%", GetLang("IQCHAT_CONTEXT_SETTINGS_ALERT_TITLE", player.UserIDString));
            Interface = Interface.Replace("%SETTINGS_ALERT_PM%", GetLang("IQCHAT_CONTEXT_SETTINGS_ALERT_PM_TITLE", player.UserIDString));
            Interface = Interface.Replace("%SETTINGS_SOUNDS%", GetLang("IQCHAT_CONTEXT_SETTINGS_SOUNDS_TITLE", player.UserIDString));
            Interface = Interface.Replace("%MUTE_STATUS_TITLE%", GetLang("IQCHAT_CONTEXT_MUTE_STATUS_TITLE", player.UserIDString));
            Interface = Interface.Replace("%IGNORED_STATUS_COUNT%", GetLang("IQCHAT_CONTEXT_IGNORED_STATUS_COUNT", player.UserIDString, Info.Settings.IgnoreUsers.Count));
            Interface = Interface.Replace("%IGNORED_STATUS_TITLE%", GetLang("IQCHAT_CONTEXT_IGNORED_STATUS_TITLE", player.UserIDString));
            Interface = Interface.Replace("%NICK_DISPLAY_TITLE%", GetLang("IQCHAT_CONTEXT_NICK_DISPLAY_TITLE", player.UserIDString));
            Interface = Interface.Replace("%MUTE_STATUS_PLAYER%", Info.MuteInfo.IsMute(MuteType.Chat) ? FormatTime(Info.MuteInfo.GetTime(MuteType.Chat), player.UserIDString) : GetLang("IQCHAT_CONTEXT_MUTE_STATUS_NOT", player.UserIDString));
            Interface = Interface.Replace("%SLIDER_PREFIX_TITLE%", GetLang("IQCHAT_CONTEXT_SLIDER_PREFIX_TITLE", player.UserIDString));
            Interface = Interface.Replace("%SLIDER_NICK_COLOR_TITLE%", GetLang("IQCHAT_CONTEXT_SLIDER_NICK_COLOR_TITLE", player.UserIDString));

            Interface = Interface.Replace("%SLIDER_MESSAGE_COLOR_TITLE%",GetLang("IQCHAT_CONTEXT_SLIDER_MESSAGE_COLOR_TITLE", player.UserIDString));
            
            Interface = Interface.Replace("%SLIDER_IQRANK_TITLE%", IQRankSystem && config.ReferenceSetting.IQRankSystems.UseRankSystem ? GetLang("IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE", player.UserIDString) : String.Empty);

            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_Chat_Context);
            CuiHelper.AddUi(player, Interface);

            DrawUI_IQChat_Update_DisplayName(player);

            if (ControllerParameter.VisualParametres.PrefixType == SelectedParametres.DropList || ControllerParameter.Prefixes.TurnMultiPrefixes)
                DrawUI_IQChat_DropList(player, "-46.788 67.4", "-14.788 91.4", GetLang("IQCHAT_CONTEXT_SLIDER_PREFIX_TITLE_DESCRIPTION", player.UserIDString), ControllerParameter.Prefixes.TurnMultiPrefixes ? TakeElementUser.MultiPrefix : TakeElementUser.Prefix);
            else DrawUI_IQChat_Sliders(player, "SLIDER_PREFIX", "-140 54", "-16 78", TakeElementUser.Prefix);

            if (ControllerParameter.VisualParametres.NickColorType == SelectedParametres.DropList)
                DrawUI_IQChat_DropList(player, "112.34 67.4", "144.34 91.4", GetLang("IQCHAT_CONTEXT_SLIDER_CHAT_NICK_TITLE_DESCRIPTION", player.UserIDString), TakeElementUser.Nick);
            else DrawUI_IQChat_Sliders(player, "SLIDER_NICK_COLOR", "20 54", "144 78", TakeElementUser.Nick);
            
            if (ControllerParameter.VisualParametres.ChatColorType == SelectedParametres.DropList)
                DrawUI_IQChat_DropList(player, "-46.787 -0.591", "-14.787 23.409",GetLang("IQCHAT_CONTEXT_SLIDER_CHAT_MESSAGE_TITLE_DESCRIPTION", player.UserIDString),TakeElementUser.Chat);
            else DrawUI_IQChat_Sliders(player, "SLIDER_MESSAGE_COLOR", "-140 -12", "-16 12", TakeElementUser.Chat);
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (IQRankSystem && config.ReferenceSetting.IQRankSystems.UseRankSystem)
            {
                if (ControllerParameter.VisualParametres.IQRankSystemType == SelectedParametres.DropList)
                    DrawUI_IQChat_DropList(player, "112.34 -0.591", "144.34 23.409", GetLang("IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE_DESCRIPTION", player.UserIDString), TakeElementUser.Rank);
                else DrawUI_IQChat_Sliders(player, "SLIDER_IQRANK", "20 -12", "144 12", TakeElementUser.Rank);
            }

            DrawUI_IQChat_Update_Check_Box(player, ElementsSettingsType.PM, "143.38 -67.9", "151.38 -59.9", Info.Settings.TurnPM);
            DrawUI_IQChat_Update_Check_Box(player, ElementsSettingsType.Broadcast, "143.38 -79.6", "151.38 -71.6", Info.Settings.TurnBroadcast);
            DrawUI_IQChat_Update_Check_Box(player, ElementsSettingsType.Alert, "143.38 -91.6", "151.38 -83.6", Info.Settings.TurnAlert);
            DrawUI_IQChat_Update_Check_Box(player, ElementsSettingsType.Sound, "143.38 -103.6", "151.38 -95.6", Info.Settings.TurnSound);
            DrawUI_IQChat_Context_AdminAndModeration(player);
        }
        public class TranslationState
        {
            public Boolean IsProcessed { get; set; }
            public String Translation { get; set; }
            public String DoTranslation { get; set; }
        }

        [ConsoleCommand("hunmute")]
        void HideUnMuteConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermissionMute)) return;
            if (arg == null || arg.Args == null || arg.Args.Length != 1 || arg.Args.Length > 1)
            {
                PrintWarning(LanguageEn ? "Invalid syntax, please use : hunmute Steam64ID" : "Неверный синтаксис,используйте : hunmute Steam64ID");
                return;
            }
            string NameOrID = arg.Args[0];
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (!Info.MuteInfo.IsMute(MuteType.Chat))
                        {
                            ConsoleOrPrintMessage(arg.Player(),
                                LanguageEn ? "The player does not have a chat lock" : "У игрока нет блокировки чата");
                            return;
                        }
                        
                        if (arg.Player()!= null)
                        {
                            if (!arg.Player().IsAdmin && arg.Player().userID == Steam64ID)
                            {
                                ConsoleOrPrintMessage(arg.Player(),
                                    LanguageEn
                                        ? "You can't unlock the chat by yourself"
                                        : "Вы не можете снять блокировку чата сами с себя");

                                return;
                            }
                        }
                    
                        Info.MuteInfo.UnMute(MuteType.Chat);
		   		 		  						  	   		  		 			  		  		  			 		   			
                        ConsoleOrPrintMessage(arg.Player(),
                            LanguageEn ? "You have unblocked the offline chat to the player" : "Вы разблокировали чат offline игроку");
                        return;
                    }
                    else
                    {
                        ConsoleOrPrintMessage(arg.Player(),
                            LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    ConsoleOrPrintMessage(arg.Player(),
                        LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }

            UnmutePlayer(target, MuteType.Chat, arg.Player(), true, true);
        }

        
        
        
        private const Boolean LanguageEn = false;
        //TODO: https://docs.rustapp.io/ru/dev/custom-alerts.html (добавить поддержку)
        //TODO: Добавить перевод префиксов
        /// <summary>
        /// Обновление 2.///
        /// - Добавлена возможность блокировать личный чат (pm, r) - если у игрока заблокирован чат, включается отдельно в конфигурации
        /// - Добавлен новый пункт в конфигурацию с возможностью отключить дополнительное логирование сообщений в RCON
        /// - Добавлена поддержка AutoEmoje by YaMang -w-
        /// - Добавлен новый хук : Object OnMessageIQChat(String message) - вызывается каждую отправку сообщения, позволяет получить и редактировать отправленное сообщение (нужно вернуть новый String)
        /// - Добавлен новый хук : void OnPlayerUnMuted(BasePlayer target, BasePlayer moderator) - вызывается после разблокировки чата игрока
        /// </summary>

                [PluginReference] Plugin ImageLibrary, IQFakeActive, IQRankSystem, XLevels, Clans, XPrison, TranslationAPI, RustApp, SkillTree, PlayerRanks;
        void IQRankSetRank(ulong userID, string RankKey) => IQRankSystem?.Call("API_SET_ACTIVE_RANK", userID, RankKey);
        String API_GET_PREFIX(UInt64 ID)
        {
            if (!UserInformation.ContainsKey(ID)) return String.Empty;
            Configuration.ControllerParameters ControllerParameter = config.ControllerParameter;

            User Info = UserInformation[ID];
            String Prefixes = String.Empty;

            if (ControllerParameter.Prefixes.TurnMultiPrefixes)
                Prefixes = String.Join("", Info.Info.PrefixList.Take(ControllerParameter.Prefixes.MaximumMultiPrefixCount));
            else Prefixes = Info.Info.Prefix;

            return Prefixes;
        }
        
        
        [ConsoleCommand("set")]
        private void CommandSet(ConsoleSystem.Arg args)
        {
            BasePlayer Sender = args.Player();

            if (Sender != null)
                if (!Sender.IsAdmin)
                    return;

            if (args == null || args.Args == null || args.Args.Length != 3)
            {
                if (Sender != null)
                    ReplySystem(Sender, LanguageEn ? "Use syntax correctly : set [Steam64ID] [prefix/chat/nick/custom] [Argument]" : "Используйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                else PrintWarning(LanguageEn ? "Use syntax correctly : set [Steam64ID] [prefix/chat/nick/custom] [Argument]" : "Используйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                return;
            }

            UInt64 Steam64ID = 0;
            BasePlayer player = null;

            if (UInt64.TryParse(args.Args[0], out Steam64ID))
                player = BasePlayer.FindByID(Steam64ID);

            if (player == null)
            {
                if (Sender != null)
                    ReplySystem(Sender, LanguageEn ? "Incorrect player Steam ID or syntax error\nUse syntax correctly : set [Steam64ID] [prefix/chat/nick/custom] [Argument]" : "Неверно указан SteamID игрока или ошибка в синтаксисе\nИспользуйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                else PrintWarning(LanguageEn ? "Incorrect player Steam ID or syntax error\nUse syntax correctly : set [Steam64ID] [prefix/chat/nick/custom] [Argument]" : "Неверно указан SteamID игрока или ошибка в синтаксисе\nИспользуйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                return;
            }
            if (!UserInformation.ContainsKey(player.userID))
            {
                if (Sender != null)
                    ReplySystem(Sender, LanguageEn ? $"Player not found!" : $"Игрок не найден!");
                else PrintWarning(LanguageEn ? $"Player not found!" : $"Игрок не найден!");
                return;
            }
            User Info = UserInformation[player.userID];

            Configuration.ControllerParameters ControllerParameter = config.ControllerParameter;

            switch (args.Args[1])
            {
                case "prefix":
                    {
                        String KeyPrefix = args.Args[2];
                        if (ControllerParameter.Prefixes.Prefixes.Count(prefix => prefix.Argument.Contains(KeyPrefix)) == 0)
                        {
                            if (Sender != null)
                                ReplySystem(Sender, LanguageEn ? "Argument not found in your configuration" : $"Аргумент не найден в вашей конфигурации!");
                            else PrintWarning(LanguageEn ? $"Argument not found in your configuration" : $"Аргумент не найден в вашей конфигурации");
                            return;
                        }

                        foreach (Configuration.ControllerParameters.AdvancedFuncion Prefix in ControllerParameter.Prefixes.Prefixes.Where(prefix => prefix.Argument.Contains(KeyPrefix)).Take(1))
                        {
                            if (ControllerParameter.Prefixes.TurnMultiPrefixes)
                                Info.Info.PrefixList.Add(Prefix.Argument);
                            else Info.Info.Prefix = Prefix.Argument;

                            if (Sender != null)
                                ReplySystem(Sender, LanguageEn ? $"Prefix successfully set to - {Prefix.Argument}" : $"Префикс успешно установлен на - {Prefix.Argument}");
                            else Puts(LanguageEn ? $"Prefix successfully set to - {Prefix.Argument}" : $"Префикс успешно установлен на - {Prefix.Argument}");
                        }
                        break;
                    }
                case "chat":
                    {
                        String KeyChatColor = args.Args[2];
                        if (ControllerParameter.MessageColorList.Count(color => color.Argument.Contains(KeyChatColor)) == 0)
                        {
                            if (Sender != null)
                                ReplySystem(Sender, LanguageEn ? $"Argument not found in your configuration!" : $"Аргумент не найден в вашей конфигурации!");
                            else PrintWarning(LanguageEn ? $"Argument not found in your configuration" : $"Аргумент не найден в вашей конфигурации");
                            return;
                        }

                        foreach (Configuration.ControllerParameters.AdvancedFuncion ChatColor in ControllerParameter.MessageColorList.Where(color => color.Argument.Contains(KeyChatColor)).Take(1))
                        {
                            Info.Info.ColorMessage = ChatColor.Argument;
                            if (Sender != null)
                                ReplySystem(Sender, LanguageEn ? $"Message color successfully set to - {ChatColor.Argument}" : $"Цвет сообщения успешно установлен на - {ChatColor.Argument}");
                            else Puts(LanguageEn ? $"Message color successfully set to - {ChatColor.Argument}" : $"Цвет сообщения успешно установлен на - {ChatColor.Argument}");
                        }
                        break;
                    }
                case "nick":
                    {
                        String KeyNickColor = args.Args[2];
                        if (ControllerParameter.NickColorList.Count(color => color.Argument.Contains(KeyNickColor)) == 0)
                        {
                            if (Sender != null)
                                ReplySystem(Sender, LanguageEn ? $"Argument not found in your configuration!" : $"Аргумент не найден в вашей конфигурации!");
                            else PrintWarning(LanguageEn ? "Argument not found in your configuration" : $"Аргумент не найден в вашей конфигурации");
                            return;
                        }

                        foreach (Configuration.ControllerParameters.AdvancedFuncion NickColor in ControllerParameter.NickColorList.Where(color => color.Argument.Contains(KeyNickColor)).Take(1))
                        {
                            Info.Info.ColorNick = NickColor.Argument;
                            if (Sender != null)
                                ReplySystem(Sender, LanguageEn ? $"Message color successfully set to - {NickColor.Argument}" : $"Цвет сообщения успешно установлен на - {NickColor.Argument}");
                            else Puts(LanguageEn ? $"Message color successfully set to - {NickColor.Argument}" : $"Цвет сообщения успешно установлен на - {NickColor.Argument}");
                        }
                        break;
                    }
                case "custom":
                    {
                        String CustomPrefix = args.Args[2];
                        if (ControllerParameter.Prefixes.TurnMultiPrefixes)
                            Info.Info.PrefixList.Add(CustomPrefix);
                        else Info.Info.Prefix = CustomPrefix;
                        if (Sender != null)
                            ReplySystem(Sender, LanguageEn ? $"Custom prefix successfully set to - {CustomPrefix}" : $"Кастомный префикс успешно установлен на - {CustomPrefix}");
                        else Puts(LanguageEn ? $"Custom prefix successfully set to - {CustomPrefix}" : $"Кастомный префикс успешно установлен на - {CustomPrefix}");

                        break;
                    }
                default:
                    {
                        if (Sender != null)
                            ReplySystem(Sender, LanguageEn ? "Use syntax correctly : set [Steam64ID] [prefix/chat/nick/custom] [Argument]" : "Используйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                        break;
                    }
            }

        }

        
        
                private new void LoadDefaultMessages()
        {
            PrintWarning(LanguageEn ? "Language file is loading..." : "Языковой файл загружается...");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FUNC_MESSAGE_MUTE_CHAT"] = "{0} muted {1}\nDuration : {2}\nReason : {3}",
                ["FUNC_MESSAGE_UNMUTE_CHAT"] = "{0} unmuted {1}",
                ["FUNC_MESSAGE_MUTE_VOICE"] = "{0} muted voice to {1}\nDuration : {2}\nReason : {3}",
                ["FUNC_MESSAGE_UNMUTE_VOICE"] = "{0} unmuted voice to {1}",
                ["FUNC_MESSAGE_UNMUTE_YOURSELF"] = "You can't unlock the chat by yourself",
                ["FUNC_MESSAGE_MUTE_ALL_CHAT"] = "Chat disabled",
                ["FUNC_MESSAGE_UNMUTE_ALL_CHAT"] = "Chat enabled",
                ["FUNC_MESSAGE_MUTE_ALL_VOICE"] = "Voice chat disabled",
                ["FUNC_MESSAGE_UNMUTE_ALL_VOICE"] = "Voice chat enabled",
                ["FUNC_MESSAGE_MUTE_ALL_ALERT"] = "Blocking by Administrator",
                ["FUNC_MESSAGE_PM_TURN_FALSE"] = "The player has forbidden to send himself private messages",
                ["FUNC_MESSAGE_ALERT_TURN_FALSE"] = "The player has not been allowed to notify himself",

                ["FUNC_MESSAGE_NO_ARG_BROADCAST"] = "You can not send an empty broadcast message!",

                ["UI_ALERT_TITLE"] = "<size=14><b>Notification</b></size>",

                ["COMMAND_NOT_PERMISSION"] = "You dont have permissions to use this command",
                ["COMMAND_RENAME_NOTARG"] = "For rename use : /rename [NewNickname] [NewID (Optional)]",
                ["COMMAND_RENAME_NOT_ID"] = "Incorrect ID for renaming! Use Steam64ID or leave blank",
                ["COMMAND_RENAME_SUCCES"] = "You have successfully changed your nickname!\nyour nickname : {0}\nYour ID : {1}",
                ["COMMAND_RENAME_RESET_SUCCES"] = "You have successfully reset your nickname changes, all data is reset to zero",
                ["COMMAND_RENAME_PROCESS_STEAM"] = "Requesting data from Steam, it may take some time",
                ["COMMAND_RENAME_NOTHING"] = "Your nickname has not changed",

                ["COMMAND_PM_NOTARG"] = "To send pm use : /pm Nickname Message",
                ["COMMAND_PM_NOT_NULL_MSG"] = "Message is empty!",
                ["COMMAND_PM_NOT_USER"] = "User not found or offline",
                ["COMMAND_PM_SUCCESS"] = "Your private message sent successful\n\nMessage : {0}\n\nDelivered : {1}",
                ["COMMAND_PM_SEND_MSG"] = "Message from {0}\n\n{1}",
                
                ["COMMAND_COLOR_CUSTOM_MAX_ARGUMENTS"] = "You specified more than 10 colors for setting the color, the first 10 colors were taken",
                ["COMMAND_COLOR_CUSTOM_NOT_ARG"] = "You need to specify colors in HEX format",
                ["COMMAND_COLOR_CUSTOM_NOT_HEX"] = "You incorrectly specified this color: {0}",
                ["COMMAND_COLOR_CUSTOM_ACCESS"] = "You have successfully set the color",

                ["COMMAND_R_NOTARG"] = "For reply use : /r Message",
                ["COMMAND_R_NOTMSG"] = "You dont have any private conversations yet!",

                ["FLOODERS_MESSAGE"] = "You're typing too fast! Please Wait {0} seconds",

                ["PREFIX_SETUP"] = "You have successfully removed the prefix {0}, it is already activated and installed",
                ["COLOR_CHAT_SETUP"] = "You have successfully picked up the {0}, it is already activated and installed",
                ["COLOR_CHAT_SETUP_TITLE"] = "chat color",
                ["COLOR_NICK_SETUP"] = "You have successfully taken the {0}, it is already activated and installed",
                ["COLOR_NICK_SETUP_TITLE"] = "nickname color",

                ["PREFIX_RETURNRED"] = "Your prefix {0} expired, it was reset automatically",
                ["COLOR_CHAT_RETURNRED"] = "Action of your {0} over, it is reset automatically",
                ["COLOR_NICK_RETURNRED"] = "Action of your {0} over, it is reset automatically",
                ["COLOR_NICK_RETURNRED_TITLE"] = "color nick",
                ["COLOR_CHAT_RETURNRED_TITLE"] = "color chat",
                ["WELCOME_PLAYER"] = "{0} came online",
                ["LEAVE_PLAYER"] = "{0} left",
                ["WELCOME_PLAYER_WORLD"] = "{0} came online. Country: {1}",
                ["LEAVE_PLAYER_REASON"] = "{0} left. Reason: {1}",

                ["IGNORE_ON_PLAYER"] = "You added {0} in black list",
                ["IGNORE_OFF_PLAYER"] = "You removed {0} from black list",
                ["IGNORE_NO_PM"] = "This player added you in black list. Your message has not been delivered.",
                ["IGNORE_NO_PM_ME"] = "You added this player in black list. Your message has not been delivered.",
                ["INGORE_NOTARG"] = "To ignore a player use : /ignore nickname",

                ["DISCORD_SEND_LOG_CHAT"] = "Player : {0}({1})\nFiltred message : {2}\nMessage : {3}",
                ["DISCORD_SEND_LOG_MUTE"] = "{0}({1}) give mute chat\nSuspect : {2}({3})\nReason : {4}",

                ["TITLE_FORMAT_DAYS"] = "D",
                ["TITLE_FORMAT_HOURSE"] = "H",
                ["TITLE_FORMAT_MINUTES"] = "M",
                ["TITLE_FORMAT_SECONDS"] = "S",

                ["IQCHAT_CONTEXT_TITLE"] = "SETTING UP A CHAT", ///"%TITLE%"
                ["IQCHAT_CONTEXT_SETTING_ELEMENT_TITLE"] = "CUSTOM SETTING", ///"%SETTING_ELEMENT%"
                ["IQCHAT_CONTEXT_INFORMATION_TITLE"] = "INFORMATION", ///"%INFORMATION%"
                ["IQCHAT_CONTEXT_SETTINGS_TITLE"] = "SETTINGS", ///"%SETTINGS%"
                ["IQCHAT_CONTEXT_SETTINGS_PM_TITLE"] = "Private messages", ///"%SETTINGS_PM%"
                ["IQCHAT_CONTEXT_SETTINGS_ALERT_TITLE"] = "Notification in the chat", ///"%SETTINGS_ALERT%"
                ["IQCHAT_CONTEXT_SETTINGS_ALERT_PM_TITLE"] = "Mention in the chat", ///"%SETTINGS_ALERT_PM%"
                ["IQCHAT_CONTEXT_SETTINGS_SOUNDS_TITLE"] = "Sound notification", ///"%SETTINGS_SOUNDS%"
                ["IQCHAT_CONTEXT_MUTE_STATUS_NOT"] = "NO", ///"%MUTE_STATUS_PLAYER%"
                ["IQCHAT_CONTEXT_MUTE_STATUS_TITLE"] = "Blocking the chat", ///"%MUTE_STATUS_TITLE%"
                ["IQCHAT_CONTEXT_IGNORED_STATUS_COUNT"] = "<size=11>{0}</size> human (а)", ///"%IGNORED_STATUS_COUNT%"
                ["IQCHAT_CONTEXT_IGNORED_STATUS_TITLE"] = "Ignoring", ///"%IGNORED_STATUS_TITLE%"
                ["IQCHAT_CONTEXT_NICK_DISPLAY_TITLE"] = "Your nickname", ///"%NICK_DISPLAY_TITLE%"
                ["IQCHAT_CONTEXT_NICK_DISPLAY_MESSAGE"] = "i love iqchat",
                ["IQCHAT_CONTEXT_SLIDER_PREFIX_TITLE"] = "Prefix", /// %SLIDER_PREFIX_TITLE%
                ["IQCHAT_CONTEXT_SLIDER_NICK_COLOR_TITLE"] = "Nick", /// %SLIDER_NICK_COLOR_TITLE%
                ["IQCHAT_CONTEXT_SLIDER_MESSAGE_COLOR_TITLE"] = "Message", /// %SLIDER_MESSAGE_COLOR_TITLE%
                ["IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE"] = "Rank",
                ["IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE_NULLER"] = "Absent",
                ["IQCHAT_CONTEXT_SLIDER_PREFIX_TITLE_DESCRIPTION"] = "Choosing a prefix", /// 
                ["IQCHAT_CONTEXT_SLIDER_CHAT_NICK_TITLE_DESCRIPTION"] = "Choosing a nickname color", /// 
                ["IQCHAT_CONTEXT_SLIDER_CHAT_MESSAGE_TITLE_DESCRIPTION"] = "Chat Color Selection", /// 
                ["IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE_DESCRIPTION"] = "Rank Selection", /// 
                ["IQCHAT_CONTEXT_DESCRIPTION_PREFIX"] = "Prefix Setting",
                ["IQCHAT_CONTEXT_DESCRIPTION_NICK"] = "Setting up a nickname",
                ["IQCHAT_CONTEXT_DESCRIPTION_CHAT"] = "Setting up a message",
                ["IQCHAT_CONTEXT_DESCRIPTION_RANK"] = "Setting up the rank",

                ["IQCHAT_ALERT_TITLE"] = "ALERT", /// %TITLE_ALERT%

                ["IQCHAT_TITLE_IGNORE_AND_MUTE_MUTED"] = "LOCK MANAGEMENT",
                ["IQCHAT_TITLE_IGNORE_AND_MUTE_IGNORED"] = "IGNORING MANAGEMENT",
                ["IQCHAT_TITLE_IGNORE_TITLES"] = "<b>DO YOU REALLY WANT TO IGNORE\n{0}?</b>",
                ["IQCHAT_TITLE_IGNORE_TITLES_UNLOCK"] = "<b>DO YOU WANT TO REMOVE THE IGNORING FROM THE PLAYER\n{0}?</b>",
                ["IQCHAT_TITLE_IGNORE_BUTTON_YES"] = "<b>YES, I WANT TO</b>",
                ["IQCHAT_TITLE_IGNORE_BUTTON_NO"] = "<b>NO, I CHANGED MY MIND</b>",
                ["IQCHAT_TITLE_MODERATION_PANEL"] = "MODERATOR PANEL",

                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU"] = "Lock Management",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT"] = "SELECT AN ACTION",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_REASON"] = "SELECT THE REASON FOR BLOCKING",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_CHAT"] = "Block chat",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_VOICE"] = "Block voice",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_MENU_TITLE_ALERT_CHAT"] = "Unblock chat",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_MENU_TITLE_ALERT_VOICE"] = "Unlock voice",
                ["IQCHAT_BUTTON_MODERATION_MUTE_ALL_CHAT"] = "Block all chat",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_ALL_CHAT"] = "Unblock all chat",
                ["IQCHAT_BUTTON_MODERATION_MUTE_ALL_VOICE"] = "Block everyone's voice",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_ALL_VOICE"] = "Unlock everyone's voice",

                ["IQCHAT_FUNCED_NO_SEND_CHAT_MUTED"] = "You have an active chat lock : {0}",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_CHAT"] = "The administrator blocked everyone's chat. Expect full unblocking",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_VOICE"] = "The administrator blocked everyone's voice chat. Expect full unblocking",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_UMMUTED_ALL_VOICE"] = "The administrator has unblocked the voice chat for everyone",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_UNMUTED_ALL_CHAT"] = "The administrator has unblocked the chat for everyone",

                ["IQCHAT_FUNCED_ALERT_TITLE"] = "<color=#a7f64f><b>[MENTION FROM {0}]</b></color>",
                ["IQCHAT_FUNCED_ALERT_TITLE_ISMUTED"] = "The player has already been muted!",
                ["IQCHAT_FUNCED_ALERT_TITLE_SERVER"] = "Administrator",
		   		 		  						  	   		  		 			  		  		  			 		   			
                ["IQCHAT_INFO_ONLINE"] = "Now on the server :\n{0}",

                ["IQCHAT_INFO_ANTI_NOOB"] = "You first connected to the server!\nPlay some more {0}\nTo get access to send messages to the global and team chat!",
                ["IQCHAT_INFO_ANTI_NOOB_PM"] = "You first connected to the server!\nPlay some more {0}\nTo access sending messages to private messages!",

                ["SKILLTREE_SYNTAX_PRESTIGE_LEVEL"] = "[{0} prestige]",
                ["SKILLTREE_SYNTAX_LEVEL"] = "[{0} Level]",
                ["SKILLTREE_SYNTAX_XP"] = "[{0} XP]",
                ["XLEVELS_SYNTAX_PREFIX"] = "[{0} Level]",
                ["CLANS_SYNTAX_PREFIX"] = "[{0}]",
                ["XPRISON_SYNTAX_PREFIX"] = "<color=orange>[{0}]</color>",

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FUNC_MESSAGE_MUTE_CHAT"] = "{0} заблокировал чат игроку {1}\nДлительность : {2}\nПричина : {3}",
                ["FUNC_MESSAGE_UNMUTE_CHAT"] = "{0} разблокировал чат игроку {1}",
                ["FUNC_MESSAGE_MUTE_VOICE"] = "{0} заблокировал голос игроку {1}\nДлительность : {2}\nПричина : {3}",
                ["FUNC_MESSAGE_UNMUTE_VOICE"] = "{0} разблокировал голос игроку {1}",
                ["FUNC_MESSAGE_UNMUTE_YOURSELF"] = "Вы не можете снять блокировку чата сами с себя",
                ["FUNC_MESSAGE_MUTE_ALL_CHAT"] = "Всем игрокам был заблокирован чат",
                ["FUNC_MESSAGE_UNMUTE_ALL_CHAT"] = "Всем игрокам был разблокирован чат",
                ["FUNC_MESSAGE_MUTE_ALL_VOICE"] = "Всем игрокам был заблокирован голос",
                ["FUNC_MESSAGE_MUTE_ALL_ALERT"] = "Блокировка Администратором",
                ["FUNC_MESSAGE_UNMUTE_ALL_VOICE"] = "Всем игрокам был разблокирован голос",

                ["FUNC_MESSAGE_PM_TURN_FALSE"] = "Игрок запретил присылать себе личные сообщения",
                ["FUNC_MESSAGE_ALERT_TURN_FALSE"] = "Игрок запретил уведомлять себя",

                ["FUNC_MESSAGE_NO_ARG_BROADCAST"] = "Вы не можете отправлять пустое сообщение в оповещение!",

                ["UI_ALERT_TITLE"] = "<size=14><b>Уведомление</b></size>",

                ["COMMAND_NOT_PERMISSION"] = "У вас недостаточно прав для данной команды",
                ["COMMAND_RENAME_NOTARG"] = "Используйте команду так : /rename [НовыйНик] [НовыйID (По желанию)]",
                ["COMMAND_RENAME_NOT_ID"] = "Неверно указан ID для переименования! Используйте Steam64ID, либо оставьте поле пустым",
                ["COMMAND_RENAME_SUCCES"] = "Вы успешно изменили ник!\nВаш ник : {0}\nВаш ID : {1}",
                ["COMMAND_RENAME_RESET_SUCCES"] = "Вы успешно сбросили изменения ника, все данные обнулены",
                ["COMMAND_RENAME_PROCESS_STEAM"] = "Запрашиваем данные у Steam, это может занять некоторое время",
                ["COMMAND_RENAME_NOTHING"] = "Ваш ник не изменялся",

                ["COMMAND_PM_NOTARG"] = "Используйте команду так : /pm Ник Игрока Сообщение",
                ["COMMAND_PM_NOT_NULL_MSG"] = "Вы не можете отправлять пустое сообщение",
                ["COMMAND_PM_NOT_USER"] = "Игрок не найден или не в сети",
                ["COMMAND_PM_SUCCESS"] = "Ваше сообщение успешно доставлено\n\nСообщение : {0}\n\nДоставлено : {1}",
                ["COMMAND_PM_SEND_MSG"] = "Сообщение от {0}\n\n{1}",

                ["COMMAND_COLOR_CUSTOM_MAX_ARGUMENTS"] = "Вы указали больше 10 цветов для установки цвета, были взяты первые 10 цветов",
                ["COMMAND_COLOR_CUSTOM_NOT_ARG"] = "Вам нужно указать цвета в формате HEX",
                ["COMMAND_COLOR_CUSTOM_NOT_HEX"] = "Вы некорректно указали данный цвет : {0}",
                ["COMMAND_COLOR_CUSTOM_ACCESS"] = "Вы успешно установили цвет",
                
                ["COMMAND_R_NOTARG"] = "Используйте команду так : /r Сообщение",
                ["COMMAND_R_NOTMSG"] = "Вам или вы ещё не писали игроку в личные сообщения!",

                ["FLOODERS_MESSAGE"] = "Вы пишите слишком быстро! Подождите {0} секунд",

                ["PREFIX_SETUP"] = "Вы успешно забрали префикс {0}, он уже активирован и установлен",
                ["COLOR_CHAT_SETUP"] = "Вы успешно забрали {0}, он уже активирован и установлен",
                ["COLOR_CHAT_SETUP_TITLE"] = "цвет чата",
                ["COLOR_NICK_SETUP"] = "Вы успешно забрали {0}, он уже активирован и установлен",
                ["COLOR_NICK_SETUP_TITLE"] = "цвет ника",

                ["PREFIX_RETURNRED"] = "Действие вашего префикса {0} окончено, он сброшен автоматически",
                ["COLOR_CHAT_RETURNRED"] = "Действие вашего {0} окончено, он сброшен автоматически",
                ["COLOR_CHAT_RETURNRED_TITLE"] = "цвета чата",
                ["COLOR_NICK_RETURNRED"] = "Действие вашего {0} окончено, он сброшен автоматически",
                ["COLOR_NICK_RETURNRED_TITLE"] = "цвета ника",

                ["WELCOME_PLAYER"] = "{0} зашел на сервер",
                ["LEAVE_PLAYER"] = "{0} вышел с сервера",
                ["WELCOME_PLAYER_WORLD"] = "{0} зашел на сервер.Из {1}",
                ["LEAVE_PLAYER_REASON"] = "{0} вышел с сервера.Причина {1}",

                ["IGNORE_ON_PLAYER"] = "Вы добавили игрока {0} в черный список",
                ["IGNORE_OFF_PLAYER"] = "Вы убрали игрока {0} из черного списка",
                ["IGNORE_NO_PM"] = "Данный игрок добавил вас в ЧС,ваше сообщение не будет доставлено",
                ["IGNORE_NO_PM_ME"] = "Вы добавили данного игрока в ЧС,ваше сообщение не будет доставлено",
                ["INGORE_NOTARG"] = "Используйте команду так : /ignore Ник Игрока",

                ["DISCORD_SEND_LOG_CHAT"] = "Игрок : {0}({1})\nФильтрованное сообщение : {2}\nИзначальное сообщение : {3}",
                ["DISCORD_SEND_LOG_MUTE"] = "{0}({1}) выдал блокировку чата\nИгрок : {2}({3})\nПричина : {4}",

                ["TITLE_FORMAT_DAYS"] = "Д",
                ["TITLE_FORMAT_HOURSE"] = "Ч",
                ["TITLE_FORMAT_MINUTES"] = "М",
                ["TITLE_FORMAT_SECONDS"] = "С",

                ["IQCHAT_CONTEXT_TITLE"] = "НАСТРОЙКА ЧАТА", ///"%TITLE%"
                ["IQCHAT_CONTEXT_SETTING_ELEMENT_TITLE"] = "ПОЛЬЗОВАТЕЛЬСКАЯ НАСТРОЙКА", ///"%SETTING_ELEMENT%"
                ["IQCHAT_CONTEXT_INFORMATION_TITLE"] = "ИНФОРМАЦИЯ", ///"%INFORMATION%"
                ["IQCHAT_CONTEXT_SETTINGS_TITLE"] = "НАСТРОЙКИ", ///"%SETTINGS%"
                ["IQCHAT_CONTEXT_SETTINGS_PM_TITLE"] = "Личные сообщения", ///"%SETTINGS_PM%"
                ["IQCHAT_CONTEXT_SETTINGS_ALERT_TITLE"] = "Оповещение в чате", ///"%SETTINGS_ALERT%"
                ["IQCHAT_CONTEXT_SETTINGS_ALERT_PM_TITLE"] = "Упоминание в чате", ///"%SETTINGS_ALERT_PM%"
                ["IQCHAT_CONTEXT_SETTINGS_SOUNDS_TITLE"] = "Звуковое оповещение", ///"%SETTINGS_SOUNDS%"
                ["IQCHAT_CONTEXT_MUTE_STATUS_NOT"] = "НЕТ", ///"%MUTE_STATUS_PLAYER%"
                ["IQCHAT_CONTEXT_MUTE_STATUS_TITLE"] = "Блокировка чата", ///"%MUTE_STATUS_TITLE%"
                ["IQCHAT_CONTEXT_IGNORED_STATUS_COUNT"] = "<size=11>{0}</size> человек (а)", ///"%IGNORED_STATUS_COUNT%"
                ["IQCHAT_CONTEXT_IGNORED_STATUS_TITLE"] = "Игнорирование", ///"%IGNORED_STATUS_TITLE%"
                ["IQCHAT_CONTEXT_NICK_DISPLAY_TITLE"] = "Ваш ник", ///"%NICK_DISPLAY_TITLE%"
                ["IQCHAT_CONTEXT_NICK_DISPLAY_MESSAGE"] = "люблю iqchat",
                ["IQCHAT_CONTEXT_SLIDER_PREFIX_TITLE"] = "Префикс", /// %SLIDER_PREFIX_TITLE%
                ["IQCHAT_CONTEXT_SLIDER_NICK_COLOR_TITLE"] = "Ник", /// %SLIDER_NICK_COLOR_TITLE%
                ["IQCHAT_CONTEXT_SLIDER_MESSAGE_COLOR_TITLE"] = "Чат", /// %SLIDER_MESSAGE_COLOR_TITLE%
                ["IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE"] = "Ранг",
                ["IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE_NULLER"] = "Отсутствует",
                ["IQCHAT_CONTEXT_SLIDER_PREFIX_TITLE_DESCRIPTION"] = "Выбор префикса", /// 
                ["IQCHAT_CONTEXT_SLIDER_CHAT_NICK_TITLE_DESCRIPTION"] = "Выбор цвета ника", /// 
                ["IQCHAT_CONTEXT_SLIDER_CHAT_MESSAGE_TITLE_DESCRIPTION"] = "Выбор цвета чата", /// 
                ["IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE_DESCRIPTION"] = "Выбор ранга", /// 
                ["IQCHAT_CONTEXT_DESCRIPTION_PREFIX"] = "Настройка префикса",
                ["IQCHAT_CONTEXT_DESCRIPTION_NICK"] = "Настройка ника",
                ["IQCHAT_CONTEXT_DESCRIPTION_CHAT"] = "Настройка сообщения",
                ["IQCHAT_CONTEXT_DESCRIPTION_RANK"] = "Настройка ранга",


                ["IQCHAT_ALERT_TITLE"] = "УВЕДОМЛЕНИЕ", /// %TITLE_ALERT%
                ["IQCHAT_TITLE_IGNORE_AND_MUTE_MUTED"] = "УПРАВЛЕНИЕ БЛОКИРОВКАМИ",
                ["IQCHAT_TITLE_IGNORE_AND_MUTE_IGNORED"] = "УПРАВЛЕНИЕ ИГНОРИРОВАНИЕМ",
                ["IQCHAT_TITLE_IGNORE_TITLES"] = "<b>ВЫ ДЕЙСТВИТЕЛЬНО ХОТИТЕ ИГНОРИРОВАТЬ\n{0}?</b>",
                ["IQCHAT_TITLE_IGNORE_TITLES_UNLOCK"] = "<b>ВЫ ХОТИТЕ СНЯТЬ ИГНОРИРОВАНИЕ С ИГРОКА\n{0}?</b>",
                ["IQCHAT_TITLE_IGNORE_BUTTON_YES"] = "<b>ДА, ХОЧУ</b>",
                ["IQCHAT_TITLE_IGNORE_BUTTON_NO"] = "<b>НЕТ, ПЕРЕДУМАЛ</b>",
                ["IQCHAT_TITLE_MODERATION_PANEL"] = "ПАНЕЛЬ МОДЕРАТОРА",

                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU"] = "Управление блокировками",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT"] = "ВЫБЕРИТЕ ДЕЙСТВИЕ",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_REASON"] = "ВЫБЕРИТЕ ПРИЧИНУ БЛОКИРОВКИ",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_CHAT"] = "Заблокировать чат",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_VOICE"] = "Заблокировать голос",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_MENU_TITLE_ALERT_CHAT"] = "Разблокировать чат",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_MENU_TITLE_ALERT_VOICE"] = "Разблокировать голос",
                ["IQCHAT_BUTTON_MODERATION_MUTE_ALL_CHAT"] = "Заблокировать всем чат",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_ALL_CHAT"] = "Разблокировать всем чат",
                ["IQCHAT_BUTTON_MODERATION_MUTE_ALL_VOICE"] = "Заблокировать всем голос",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_ALL_VOICE"] = "Разблокировать всем голос",

                ["IQCHAT_FUNCED_NO_SEND_CHAT_MUTED"] = "У вас имеется активная блокировка чата : {0}",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_CHAT"] = "Администратор заблокировал всем чат. Ожидайте полной разблокировки",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_VOICE"] = "Администратор заблокировал всем голосоввой чат. Ожидайте полной разблокировки",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_UMMUTED_ALL_VOICE"] = "Администратор разрблокировал всем голосоввой чат",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_UNMUTED_ALL_CHAT"] = "Администратор разрблокировал всем чат",

                ["IQCHAT_FUNCED_ALERT_TITLE"] = "<color=#a7f64f><b>[УПОМИНАНИЕ ОТ {0}]</b></color>",
                ["IQCHAT_FUNCED_ALERT_TITLE_ISMUTED"] = "Игрок уже был замучен!",
                ["IQCHAT_FUNCED_ALERT_TITLE_SERVER"] = "Администратор",

                ["IQCHAT_INFO_ONLINE"] = "Сейчас на сервере :\n{0}",

                ["IQCHAT_INFO_ANTI_NOOB"] = "Вы впервые подключились на сервер!\nОтыграйте еще {0}\nЧтобы получить доступ к отправке сообщений в глобальный и командный чат!",
                ["IQCHAT_INFO_ANTI_NOOB_PM"] = "Вы впервые подключились на сервер!\nОтыграйте еще {0}\nЧтобы получить доступ к отправке сообщений в личные сообщения!",

                ["SKILLTREE_SYNTAX_LEVEL"] = "[{0} Level]",
                ["SKILLTREE_SYNTAX_PRESTIGE_LEVEL"] = "[{0} престиж]",
                ["SKILLTREE_SYNTAX_XP"] = "[{0} XP]",
                ["XLEVELS_SYNTAX_PREFIX"] = "[{0} Level]",
                ["CLANS_SYNTAX_PREFIX"] = "[{0}]",
                ["XPRISON_SYNTAX_PREFIX"] = "<color=orange>[{0}]</color>",
		   		 		  						  	   		  		 			  		  		  			 		   			
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FUNC_MESSAGE_MUTE_CHAT"] = "{0} silenciado {1}\n Duración: {2}\nRazón: {3}",
                ["FUNC_MESSAGE_UNMUTE_CHAT"] = "{0} sin silenciar {1}",
                ["FUNC_MESSAGE_MUTE_VOICE"] = "{0} voz apagada a {1}\n Duracion : {2}\n Razon : {3}",
                ["FUNC_MESSAGE_UNMUTE_VOICE"] = "{0} voz no silenciada a {1}",
                ["FUNC_MESSAGE_UNMUTE_YOURSELF"] = "No puedes desbloquear el chat por ti mismo",
                ["FUNC_MESSAGE_MUTE_ALL_CHAT"] = "Chat desactivado",
                ["FUNC_MESSAGE_UNMUTE_ALL_CHAT"] = "Chat habilitado",
                ["FUNC_MESSAGE_MUTE_ALL_VOICE"] = "Chat de voz desactivado",
                ["FUNC_MESSAGE_UNMUTE_ALL_VOICE"] = "Chat de voz habilitado",
                ["FUNC_MESSAGE_MUTE_ALL_ALERT"] = "Bloqueo por parte del administrador",
                ["FUNC_MESSAGE_PM_TURN_FALSE"] = "El jugador tiene prohibido enviarse mensajes privados",
                ["FUNC_MESSAGE_ALERT_TURN_FALSE"] = "El jugador no ha podido notificarse a sí mismo",
                ["FUNC_MESSAGE_NO_ARG_BROADCAST"] = "No se puede enviar un mensaje vacío.",
                ["UI_ALERT_TITLE"] = "<size=14><b>Notificación</b></size>",
                ["COMMAND_NOT_PERMISSION"] = "No tienes permisos para usar este comando",
                ["COMMAND_RENAME_NOTARG"] = "Para renombrar utilice : /rename [NewNickname] [NewID (Optional)]",
                ["COMMAND_RENAME_NOT_ID"] = "¡ID incorrecto para renombrar! Utilice Steam64ID o déjelo en blanco",
                ["COMMAND_RENAME_SUCCES"] = "Has cambiado con éxito tu nombre de usuario. \n Tu nombre de usuario: {0}. \nTu ID: {1}.",
                ["COMMAND_RENAME_RESET_SUCCES"] = "Has restablecido con éxito los cambios de Nick, todos los datos están a cero",
                ["COMMAND_RENAME_PROCESS_STEAM"] = "Solicitamos datos a Steam, esto puede llevar algún tiempo",
                ["COMMAND_RENAME_NOTHING"] = "Su apodo no ha cambiado",
                ["COMMAND_PM_NOTARG"] = "Para enviar pm utilice : /pm [Nombre] [Mensaje]",
                ["COMMAND_PM_NOT_NULL_MSG"] = "¡El mensaje está vacío!",
                ["COMMAND_PM_NOT_USER"] = "Usuario no encontrado o desconectado",
                ["COMMAND_PM_SUCCESS"] = "Su mensaje privado enviado con éxito \n Mensage : {0}\n : Entregado{1}",
                ["COMMAND_PM_SEND_MSG"] = "Mensaje de {0}\n{1}",
                ["COMMAND_COLOR_CUSTOM_MAX_ARGUMENTS"] = "Has especificado más de 10 colores para configurar el color, se han tomado los primeros 10 colores",
                ["COMMAND_COLOR_CUSTOM_NOT_ARG"] = "Necesitas especificar colores en formato HEX",
                ["COMMAND_COLOR_CUSTOM_NOT_HEX"] = "Has especificado incorrectamente este color: {0}",
                ["COMMAND_COLOR_CUSTOM_ACCESS"] = "Has establecido el color con éxito",
                ["COMMAND_R_NOTARG"] = "Para responder utilice : /r Mensaje",
                ["COMMAND_R_NOTMSG"] = "Todavía no tienes ninguna conversación privada.",
                ["FLOODERS_MESSAGE"] = "¡Estás escribiendo demasiado rápido! Por favor, espere {0} segundos",
                ["PREFIX_SETUP"] = "Has eliminado con éxito el prefijo {0}.",
                ["COLOR_CHAT_SETUP"] = "Has obtenido un nuevo color en el chat",
                ["COLOR_CHAT_SETUP_TITLE"] = "chat color",
                ["COLOR_NICK_SETUP"] = "Has cambiado tu nick correctamente del chat",
                ["COLOR_NICK_SETUP_TITLE"] = "nick color",
                ["PREFIX_RETURNRED"] = "Su prefijo {0} ha caducado, se ha restablecido automáticamente",
                ["COLOR_CHAT_RETURNRED"] = "Acción de su {0} más, se restablece automáticamente",
                ["COLOR_NICK_RETURNRED"] = "Acción de su {0} sobre, se restablece automáticamente",
                ["COLOR_NICK_RETURNRED_TITLE"] = "color nick",
                ["COLOR_CHAT_RETURNRED_TITLE"] = "color chat",
                ["WELCOME_PLAYER"] = "{0} Se ha conectado",
                ["LEAVE_PLAYER"] = "{0} izquierda",
                ["WELCOME_PLAYER_WORLD"] = "{0} Se ha conectado del Pais: {1}",
                ["LEAVE_PLAYER_REASON"] = "{0} Se ha desconectado. Razon: {1}",
                ["IGNORE_ON_PLAYER"] = "Has añadido {0} en la lista negra",
                ["IGNORE_OFF_PLAYER"] = "Has eliminado el jugador {0} de la lista negra",
                ["IGNORE_NO_PM"] = "Este jugador te ha añadido a la lista negra. Su mensaje no ha sido entregado.",
                ["IGNORE_NO_PM_ME"] = "Has añadido a este jugador en la lista negra. Su mensaje no ha sido entregado.",
                ["INGORE_NOTARG"] = "Para ignorar a un jugador utiliza : /ignore nickname",
                ["DISCORD_SEND_LOG_CHAT"] = "JUgador : {0}({1})\nMensaje filtrado : {2}\nMensages : {3}",
                ["DISCORD_SEND_LOG_MUTE"] = "{0}({1}) give mute chat\nSuspect : {2}({3})\nReason : {4}",
                ["TITLE_FORMAT_DAYS"] = "D",
                ["TITLE_FORMAT_HOURSE"] = "H",
                ["TITLE_FORMAT_MINUTES"] = "M",
                ["TITLE_FORMAT_SECONDS"] = "S",
                ["IQCHAT_CONTEXT_TITLE"] = "ESTABLECER UN CHAT",
                ["IQCHAT_CONTEXT_SETTING_ELEMENT_TITLE"] = "AJUSTE PERSONALIZADO",
                ["IQCHAT_CONTEXT_INFORMATION_TITLE"] = "INFORMACIÓN",
                ["IQCHAT_CONTEXT_SETTINGS_TITLE"] = "AJUSTES",
                ["IQCHAT_CONTEXT_SETTINGS_PM_TITLE"] = "Mensajes privados",
                ["IQCHAT_CONTEXT_SETTINGS_ALERT_TITLE"] = "Notificación en el chat",
                ["IQCHAT_CONTEXT_SETTINGS_ALERT_PM_TITLE"] = "Mención en el chat",
                ["IQCHAT_CONTEXT_SETTINGS_SOUNDS_TITLE"] = "Notificación sonora",
                ["IQCHAT_CONTEXT_MUTE_STATUS_NOT"] = "NO",
                ["IQCHAT_CONTEXT_MUTE_STATUS_TITLE"] = "Bloqueo del chat",
                ["IQCHAT_CONTEXT_IGNORED_STATUS_COUNT"] = "<size=11>{0}</size> humano (а)",
                ["IQCHAT_CONTEXT_IGNORED_STATUS_TITLE"] = "Ignorando",
                ["IQCHAT_CONTEXT_NICK_DISPLAY_TITLE"] = "Su apodo",
                ["IQCHAT_CONTEXT_NICK_DISPLAY_MESSAGE"] = "Me encanta Zoxiland",
                ["IQCHAT_CONTEXT_SLIDER_PREFIX_TITLE"] = "Prefijo",
                ["IQCHAT_CONTEXT_SLIDER_NICK_COLOR_TITLE"] = "Nick",
                ["IQCHAT_CONTEXT_SLIDER_MESSAGE_COLOR_TITLE"] = "Mensaje",
                ["IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE"] = "Rango",
                ["IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE_NULLER"] = "Ausente",
                ["IQCHAT_CONTEXT_SLIDER_PREFIX_TITLE_DESCRIPTION"] = "Elegir un prefijo",
                ["IQCHAT_CONTEXT_SLIDER_CHAT_NICK_TITLE_DESCRIPTION"] = "Elegir un color de apodo",
                ["IQCHAT_CONTEXT_SLIDER_CHAT_MESSAGE_TITLE_DESCRIPTION"] = "Selección del color del chat",
                ["IQCHAT_CONTEXT_SLIDER_IQRANK_TITLE_DESCRIPTION"] = "Selección de rangos",
                ["IQCHAT_CONTEXT_DESCRIPTION_PREFIX"] = "Ajuste del prefijo",
                ["IQCHAT_CONTEXT_DESCRIPTION_NICK"] = "Configurar un apodo",
                ["IQCHAT_CONTEXT_DESCRIPTION_CHAT"] = "Configurar un mensaje",
                ["IQCHAT_CONTEXT_DESCRIPTION_RANK"] = "Establecimiento del rango",
                ["IQCHAT_ALERT_TITLE"] = "ALERTA",
                ["IQCHAT_TITLE_IGNORE_AND_MUTE_MUTED"] = "GESTIÓN MUTEADOS",
                ["IQCHAT_TITLE_IGNORE_AND_MUTE_IGNORED"] = "GESTIÓN IGNORE",
                ["IQCHAT_TITLE_IGNORE_TITLES"] = "<b>¿REALMENTE QUIERES IGNORAR\n{0}?</b>",
                ["IQCHAT_TITLE_IGNORE_TITLES_UNLOCK"] = "<b>¿QUIERES QUITARLE AL JUGADOR LO DE IGNORAR?\n{0}?</b>",
                ["IQCHAT_TITLE_IGNORE_BUTTON_YES"] = "<b>SÍ, QUIERO</b>",
                ["IQCHAT_TITLE_IGNORE_BUTTON_NO"] = "<b>NO, HE CAMBIADO DE OPINIÓN</b>",
                ["IQCHAT_TITLE_MODERATION_PANEL"] = "PANEL DE MODERADORES",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU"] = "Menu de muteados",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT"] = "SELECCIONE UNA ACCIÓN",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_REASON"] = "SELECCIONE EL MOTIVO DEL BLOQUEO",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_CHAT"] = "Bloquear el Chat",
                ["IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_VOICE"] = "Bloquear Voz",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_MENU_TITLE_ALERT_CHAT"] = "Desbloquear Chat",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_MENU_TITLE_ALERT_VOICE"] = "Desbloquear Voz",
                ["IQCHAT_BUTTON_MODERATION_MUTE_ALL_CHAT"] = "Bloquear todos los chats",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_ALL_CHAT"] = "Desbloquear todo el chat",
                ["IQCHAT_BUTTON_MODERATION_MUTE_ALL_VOICE"] = "Bloquear la voz de todos",
                ["IQCHAT_BUTTON_MODERATION_UNMUTE_ALL_VOICE"] = "Desbloquear la voz de todos",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_MUTED"] = "Tienes un bloqueo de chat activo : {0}",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_CHAT"] = "El administrador ha bloqueado el chat. Espera el desbloqueo completo",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_VOICE"] = "El administrador ha bloqueado el chat de voz. Espera el desbloqueo completo",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_UMMUTED_ALL_VOICE"] = "El administrador ha desbloqueado el chat de voz.",
                ["IQCHAT_FUNCED_NO_SEND_CHAT_UNMUTED_ALL_CHAT"] = "El administrador ha desbloqueado el chat",
                ["IQCHAT_FUNCED_ALERT_TITLE"] = "<color=#a7f64f><b>[MENCIÓN de {0}]</b></color>",
                ["IQCHAT_FUNCED_ALERT_TITLE_ISMUTED"] = "El jugador ya ha sido silenciado.",
                ["IQCHAT_FUNCED_ALERT_TITLE_SERVER"] = "Administrador",
                ["IQCHAT_INFO_ONLINE"] = "Now on the server :\n{0}",
                ["IQCHAT_INFO_ANTI_NOOB"] = "Tienes que jugar un poco mas para poder hablar por el chat {0}.",
                ["IQCHAT_INFO_ANTI_NOOB_PM"] = "No puedes enviar un privado por que es un jugador nuevo.",
                ["SKILLTREE_SYNTAX_LEVEL"] = "[{0} Nivel]",
                ["SKILLTREE_SYNTAX_PRESTIGE_LEVEL"] = "[{0} prestige]",
                ["SKILLTREE_SYNTAX_XP"] = "[{0} XP]",
                ["XLEVELS_SYNTAX_PREFIX"] = "[{0} Nivel]",
                ["CLANS_SYNTAX_PREFIX"] = "[{0}]",
                ["XPRISON_SYNTAX_PREFIX"] = "<color=orange>[{0}]</color>",


            }, this, "es-ES");

            PrintWarning(LanguageEn ? "Language file uploaded successfully" : "Языковой файл загружен успешно");
        }

        
                private void DrawUI_IQChat_Sliders(BasePlayer player, String Name, String OffsetMin, String OffsetMax, TakeElementUser ElementType)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Slider");
            if (Interface == null) return;

            Interface = Interface.Replace("%OFFSET_MIN%", OffsetMin);
            Interface = Interface.Replace("%OFFSET_MAX%", OffsetMax);
            Interface = Interface.Replace("%NAME%", Name);
            Interface = Interface.Replace("%COMMAND_LEFT_SLIDE%", $"newui.cmd slider.controller {ElementType} -");
            Interface = Interface.Replace("%COMMAND_RIGHT_SLIDE%", $"newui.cmd slider.controller {ElementType} +");

            CuiHelper.DestroyUi(player, Name);
            CuiHelper.AddUi(player, Interface);

            DrawUI_IQChat_Slider_Update_Argument(player, ElementType);
        }
        
                private void MutePlayer(BasePlayer Target, MuteType Type, Int32 ReasonIndex, BasePlayer Moderator = null, String ReasonCustom = null, Int32 TimeCustom = 0, Boolean HideMute = false, Boolean Command = false, String fakeUserId = "")
        {
            Configuration.ControllerMute ControllerMutes = config.ControllerMutes;

            if (IsReadyIQFakeActive() && Target == null && !String.IsNullOrWhiteSpace(fakeUserId))
            {
                ReplySystem(Moderator, GetLang(Type == MuteType.Chat ? "FUNC_MESSAGE_MUTE_CHAT" : "FUNC_MESSAGE_MUTE_VOICE", Moderator != null ? Moderator.displayName : Moderator.UserIDString, GetLang("IQCHAT_FUNCED_ALERT_TITLE_SERVER"), GetFakeName(fakeUserId), FormatTime(TimeCustom == 0 ? config.ControllerMutes.MuteChatReasons[ReasonIndex].SecondMute : TimeCustom), ReasonCustom ?? config.ControllerMutes.MuteChatReasons[ReasonIndex].Reason));
                SetMuteFakeUser(fakeUserId, true);
                return;
            }
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (!UserInformation.ContainsKey(Target.userID)) return;
            User Info = UserInformation[Target.userID];

            String LangMessage = String.Empty;
            String Reason = String.Empty;
            Int32 MuteTime = 0;
            
            String NameModerator = GetLang("IQCHAT_FUNCED_ALERT_TITLE_SERVER", Target.UserIDString);

            if (Moderator != null)
            {
                if (!permission.UserHasPermission(Moderator.UserIDString, PermissionHideMuteName))
                {
                    GeneralInformation.RenameInfo ModeratorRename = GeneralInfo.GetInfoRename(Moderator.userID);
                    NameModerator = ModeratorRename != null ? $"{ModeratorRename.RenameNick ?? Moderator.displayName}" : Moderator.displayName;
                }
            }

            GeneralInformation.RenameInfo TagetRename = GeneralInfo.GetInfoRename(Target.userID);
            String TargetName = TagetRename != null ? $"{TagetRename.RenameNick ?? Target.displayName}" : Target.displayName;

            if (Target == null || !Target.IsConnected)
            {
                if (Moderator != null && !Command)
                    ReplySystem(Moderator, GetLang("UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_NOT_CONNNECTED", Moderator.UserIDString));
                return;
            }

            if (Moderator != null && !Command)
                if (Info.MuteInfo.IsMute(Type))
                {
                    ReplySystem(Moderator, GetLang("IQCHAT_FUNCED_ALERT_TITLE_ISMUTED", Moderator.UserIDString));
                    return;
                }

            switch (Type)
            {
                case MuteType.Chat:
                    {
                        Reason = ReasonCustom ?? ControllerMutes.MuteChatReasons[ReasonIndex].Reason;
                        MuteTime = TimeCustom == 0 ? ControllerMutes.MuteChatReasons[ReasonIndex].SecondMute : TimeCustom;
                        LangMessage = "FUNC_MESSAGE_MUTE_CHAT";
                        break;
                    }
                case MuteType.Voice:
                    {
                        Reason = ReasonCustom ?? ControllerMutes.MuteVoiceReasons[ReasonIndex].Reason;
                        MuteTime = TimeCustom == 0 ? ControllerMutes.MuteVoiceReasons[ReasonIndex].SecondMute : TimeCustom;
                        LangMessage = "FUNC_MESSAGE_MUTE_VOICE";
                        break;
                    }
            }

            Info.MuteInfo.SetMute(Type, MuteTime);
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (Moderator != null && Moderator != Target)
                Interface.Oxide.CallHook("OnPlayerMuted", Target, Moderator, MuteTime, Reason);

            if (!HideMute)
                ReplyBroadcast(null, null, false, LangMessage, NameModerator, TargetName, FormatTime(MuteTime, Target.UserIDString), Reason);
               // ReplyBroadcast(GetLang(LangMessage, Target.UserIDString, NameModerator, TargetName, FormatTime(MuteTime, Target.UserIDString), Reason));
            else
            {
                if (Target != null)
                    ReplySystem(Target, GetLang(LangMessage, Target.UserIDString, NameModerator, TargetName, FormatTime(MuteTime, Target.UserIDString), Reason));

                if (Moderator != null)
                    ReplySystem(Moderator, GetLang(LangMessage, Target.UserIDString, NameModerator, TargetName, FormatTime(MuteTime, Target.UserIDString), Reason));
            }

            DiscordLoggMuted(Target, Type, Reason, FormatTime(MuteTime, Target.UserIDString), Moderator);
        }
        private String SkillTree_GetLevel(BasePlayer player)
        {
            String[] infoSkills = GetInfoSkillTree(player);
            if(infoSkills == null) return String.Empty;
            String levelSkillTree = GetLang("SKILLTREE_SYNTAX_LEVEL", player.UserIDString, infoSkills.First());
            
            Object resultColorMessage = IsGradientColorValue(config.ReferenceSetting.skillTreeSettings.colorTag);
            if (resultColorMessage is List<String> gradientColors)
                levelSkillTree = ApplyGradientToText(levelSkillTree, gradientColors);
            else levelSkillTree = $"<color={config.ReferenceSetting.skillTreeSettings.colorTag}>{levelSkillTree}</color>";

            return levelSkillTree;
        }
        
        private void AlertController(BasePlayer player)
        {
            Object status = Interface.CallHook("OnChatAlertConnected", player);
            if (status != null)
                return;
            
            Configuration.ControllerAlert.AdminSession AlertSessionAdmin = config.ControllerAlertSetting.AdminSessionSetting;
            Configuration.ControllerAlert.PlayerSession AlertSessionPlayer = config.ControllerAlertSetting.PlayerSessionSetting;
            Configuration.ControllerAlert.PersonalAlert AlertPersonal = config.ControllerAlertSetting.PersonalAlertSetting;
            GeneralInformation.RenameInfo RenameInformation = GeneralInfo.GetInfoRename(player.userID);

            String DisplayName = player.displayName;

            UInt64 UserID = player.userID;
            if (RenameInformation != null)
            {
                DisplayName = RenameInformation.RenameNick;
                UserID = RenameInformation.RenameID;
            }

            if (AlertSessionPlayer.ConnectedAlert)
            {
                if (!AlertSessionAdmin.ConnectedAlertAdmin)
                    if (player.IsAdmin) return;
		   		 		  						  	   		  		 			  		  		  			 		   			
                String Avatar = AlertSessionPlayer.ConnectedAvatarUse ? UserID.ToString() : String.Empty;

                if (AlertSessionPlayer.ConnectedWorld)
                {
                    String ipPlayer = player.IPlayer.Address;

                    if (player.net?.connection != null)
                    {
                        String[] ipPortPlayer = player.net.connection.ipaddress.Split(':');
                        if (ipPortPlayer.Length >= 1)
                            ipPlayer = ipPortPlayer[0]; 
                    }
                    
                    webrequest.Enqueue("http://ip-api.com/json/" + ipPlayer, null, (code, response) =>
                    {
                        if (code != 200 || response == null)
                            return;

                        String country = JsonConvert.DeserializeObject<Response>(response).Country;

                        if (!permission.UserHasPermission(player.UserIDString, PermissionHideConnection))
                        {
                            if (AlertSessionPlayer.ConnectionAlertRandom)
                                ReplyBroadcast(null, Avatar, false, AlertSessionPlayer.RandomConnectionAlert.LanguageMessages,DisplayName, country ?? "none");
                            else ReplyBroadcast(null, Avatar, false, "WELCOME_PLAYER_WORLD", DisplayName, country ?? "none");
                        }

                        Log($"[{player.userID}] {GetLang("WELCOME_PLAYER_WORLD", "", DisplayName, country ?? "none")}");
                    }, this);
                }
                else
                {
                    if (!permission.UserHasPermission(player.UserIDString, PermissionHideConnection))
                    {
                        if (AlertSessionPlayer.ConnectionAlertRandom)
                            ReplyBroadcast(null, Avatar, false,AlertSessionPlayer.RandomConnectionAlert.LanguageMessages, DisplayName);
                        else ReplyBroadcast(null, Avatar, false, "WELCOME_PLAYER", DisplayName);
                    }

                    Log($"[{player.userID}] {GetLang("WELCOME_PLAYER", "", DisplayName)}");
                }
            }
            if (AlertPersonal.UseWelcomeMessage)
            {
                String WelcomeMessage = GetMessages(player, AlertPersonal.WelcomeMessage.LanguageMessages);
                if (String.IsNullOrWhiteSpace(WelcomeMessage)) return;
                ReplySystem(player, WelcomeMessage);
            }
        }
        public Dictionary<BasePlayer, BasePlayer> PMHistory = new Dictionary<BasePlayer, BasePlayer>();

        
                
        private void Log(String LoggedMessage) => LogToFile("IQChatLogs", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {LoggedMessage}", this);
        private const String PermissionHideConnection = "iqchat.hideconnection";

        [ConsoleCommand("online")]
        private void ShowPlayerOnlineConsole(ConsoleSystem.Arg arg)
        {
            if (!config.OtherSetting.UseCommandOnline) return;

            BasePlayer player = arg.Player();
            String Message = String.Empty;
            
            if (config.OtherSetting.UseCommandShortOnline)
            {
                Int32 shortCount = GetPlayersOnlineShort();
                Message = GetLang("IQCHAT_INFO_ONLINE", player != null ? player.UserIDString : null, $"{shortCount}");
            }
            else
            {
                List<String> PlayerNames = GetPlayersOnline();
                Message = GetLang("IQCHAT_INFO_ONLINE", player != null ? player.UserIDString : null, String.Join($"\n", PlayerNames));
            }

            if (player != null)
                player.ConsoleMessage(Message);
            else
            {
                String Pattern = @"</?size.*?>|</?color.*?>";
                String Messages = Regex.IsMatch(Message, Pattern) ? Regex.Replace(Message, Pattern, "") : Message;
                Puts(Messages);
            }
        }
        public String FormatTime(Double Second, String UserID = null)
        {
            TimeSpan time = TimeSpan.FromSeconds(Second);
            String Result = String.Empty;
            String Days = GetLang("TITLE_FORMAT_DAYS", UserID);
            String Hourse = GetLang("TITLE_FORMAT_HOURSE", UserID);
            String Minutes = GetLang("TITLE_FORMAT_MINUTES", UserID);
            String Seconds = GetLang("TITLE_FORMAT_SECONDS", UserID);

            if (time.Seconds != 0)
                Result = $"{Format(time.Seconds, Seconds, Seconds, Seconds)}";
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (time.Minutes != 0)
                Result = $"{Format(time.Minutes, Minutes, Minutes, Minutes)}";

            if (time.Hours != 0)
                Result = $"{Format(time.Hours, Hourse, Hourse, Hourse)}";
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (time.Days != 0)
                Result = $"{Format(time.Days, Days, Days, Days)}";

            return Result;
        }
        private class Configuration
        {
                        [JsonProperty(LanguageEn ? "Setting up player information" : "Настройка информации о игроке")]
            public ControllerConnection ControllerConnect = new ControllerConnection();
            internal class ControllerConnection
            {
                [JsonProperty(LanguageEn ? "Function switches" : "Перключатели функций")]
                public Turned Turneds = new Turned();
                [JsonProperty(LanguageEn ? "Setting Standard Values" : "Настройка стандартных значений")]
                public SetupDefault SetupDefaults = new SetupDefault();

                internal class SetupDefault
                {
                    [JsonProperty(LanguageEn ? "This prefix will be set if the player entered the server for the first time or in case of expiration of the rights to the prefix that he had earlier" : "Данный префикс установится если игрок впервые зашел на сервер или в случае окончания прав на префикс, который у него стоял ранее")]
                    public String PrefixDefault = "<color=#CC99FF>[ИГРОК]</color>";
                    [JsonProperty(LanguageEn ? "This nickname color will be set if the player entered the server for the first time or in case of expiration of the rights to the nickname color that he had earlier" : "Данный цвет ника установится если игрок впервые зашел на сервер или в случае окончания прав на цвет ника, который у него стоял ранее")]
                    public String NickDefault = "#33CCCC";
                    [JsonProperty(LanguageEn ? "This chat color will be set if the player entered the server for the first time or in case of expiration of the rights to the chat color that he had earlier" : "Данный цвет чата установится если игрок впервые зашел на сервер или в случае окончания прав на цвет чата, который у него стоял ранее")]
                    public String MessageDefault = "#0099FF";
                }
                internal class Turned
                {
                    [JsonProperty(LanguageEn ? "Set automatically a prefix to a player when he got the rights to it" : "Устанавливать автоматически префикс игроку, когда он получил права на него")]
                    public Boolean TurnAutoSetupPrefix;
                    [JsonProperty(LanguageEn ? "Set automatically the color of the nickname to the player when he got the rights to it" : "Устанавливать автоматически цвет ника игроку, когда он получил права на него")]
                    public Boolean TurnAutoSetupColorNick;
                    [JsonProperty(LanguageEn ? "Set the chat color automatically to the player when he got the rights to it" : "Устанавливать автоматически цвет чата игроку, когда он получил права на него")]
                    public Boolean TurnAutoSetupColorChat;
                    [JsonProperty(LanguageEn ? "Automatically reset the prefix when the player's rights to it expire" : "Сбрасывать автоматически префикс при окончании прав на него у игрока")]
                    public Boolean TurnAutoDropPrefix;
                    [JsonProperty(LanguageEn ? "Automatically reset the color of the nickname when the player's rights to it expire" : "Сбрасывать автоматически цвет ника при окончании прав на него у игрока")]
                    public Boolean TurnAutoDropColorNick;
                    [JsonProperty(LanguageEn ? "Automatically reset the color of the chat when the rights to it from the player expire" : "Сбрасывать автоматически цвет чата при окончании прав на него у игрока")]
                    public Boolean TurnAutoDropColorChat;
                }
            }
            
                        [JsonProperty(LanguageEn ? "Setting options for the player" : "Настройка параметров для игрока")]
            public ControllerParameters ControllerParameter = new ControllerParameters();
            internal class ControllerParameters
            {
                [JsonProperty(LanguageEn ? "Setting the display of options for player selection" : "Настройка отображения параметров для выбора игрока")]
                public VisualSettingParametres VisualParametres = new VisualSettingParametres();
                [JsonProperty(LanguageEn ? "List and customization of colors for a nickname" : "Список и настройка цветов для ника")]
                public List<AdvancedFuncion> NickColorList = new List<AdvancedFuncion>();
                [JsonProperty(LanguageEn ? "List and customize colors for chat messages" : "Список и настройка цветов для сообщений в чате")]
                public List<AdvancedFuncion> MessageColorList = new List<AdvancedFuncion>();
                [JsonProperty(LanguageEn ? "List and configuration of prefixes in chat" : "Список и настройка префиксов в чате")]
                public PrefixSetting Prefixes = new PrefixSetting();
                internal class PrefixSetting
                {
                    [JsonProperty(LanguageEn ? "Enable support for multiple prefixes at once (true - multiple prefixes can be set/false - only 1 can be set to choose from)" : "Включить поддержку нескольких префиксов сразу (true - можно установить несколько префиксов/false - установить можно только 1 на выбор)")]
                    public Boolean TurnMultiPrefixes;
                    [JsonProperty(LanguageEn ? "The maximum number of prefixes that can be set at a time (This option only works if setting multiple prefixes is enabled)" : "Максимальное количество префиксов, которое можно установить за раз(Данный параметр работает только если включена установка нескольких префиксов)")]
                    public Int32 MaximumMultiPrefixCount;
                    [JsonProperty(LanguageEn ? "List of prefixes and their settings" : "Список префиксов и их настройка")]
                    public List<AdvancedFuncion> Prefixes = new List<AdvancedFuncion>();
                }

                internal class AdvancedFuncion
                {
                    [JsonProperty(LanguageEn ? "Permission" : "Права")]
                    public String Permissions;
                    [JsonProperty(LanguageEn ? "Argument" : "Значение")]
                    public String Argument;
                    [JsonProperty(LanguageEn ? "Block the player's ability to select this parameter in the plugin menu (true - yes/false - no)" : "Заблокировать возможность выбрать данный параметр игроком в меню плагина (true - да/false - нет)")]
                    public Boolean IsBlockSelected;
                }

                internal class VisualSettingParametres
                {
                    [JsonProperty(LanguageEn ? "Player prefix selection display type - (0 - dropdown list, 1 - slider (Please note that if you have multi-prefix enabled, the dropdown list will be set))" : "Тип отображения выбора префикса для игрока - (0 - выпадающий список, 1 - слайдер (Учтите, что если у вас включен мульти-префикс, будет установлен выпадающий список))")]
                    public SelectedParametres PrefixType;
                    [JsonProperty(LanguageEn ? "Display type of player's nickname color selection - (0 - drop-down list, 1 - slider)" : "Тип отображения выбора цвета ника для игрока - (0 - выпадающий список, 1 - слайдер)")]
                    public SelectedParametres NickColorType;
                    [JsonProperty(LanguageEn ? "Display type of message color choice for the player - (0 - drop-down list, 1 - slider)" : "Тип отображения выбора цвета сообщения для игрока - (0 - выпадающий список, 1 - слайдер)")]
                    public SelectedParametres ChatColorType;
                    [JsonProperty(LanguageEn ? "IQRankSystem : Player rank selection display type - (0 - drop-down list, 1 - slider)" : "IQRankSystem : Тип отображения выбора ранга для игрока - (0 - выпадающий список, 1 - слайдер)")]
                    public SelectedParametres IQRankSystemType;
                }
            }
            
                        [JsonProperty(LanguageEn ? "Plugin mute settings" : "Настройка мута в плагине")]
            public ControllerMute ControllerMutes = new ControllerMute();
            internal class ControllerMute
            {
                [JsonProperty(LanguageEn ? "Prohibit sending messages in /pm and /r if the player's chat is blocked" : "Запрещать отправлять сообщения в /pm, /r - если у игрока заблокирован чат")]
                public Boolean mutedPM;
                [JsonProperty(LanguageEn ? "Setting up automatic muting" : "Настройка автоматического мута")]
                public AutoMute AutoMuteSettings = new AutoMute();
                internal class AutoMute
                {
                    [JsonProperty(LanguageEn ? "Enable automatic muting for forbidden words (true - yes/false - no)" : "Включить автоматический мут по запрещенным словам(true - да/false - нет)")]
                    public Boolean UseAutoMute;
                    [JsonProperty(LanguageEn ? "Reason for automatic muting" : "Причина автоматического мута")]
                    public Muted AutoMuted;
                }
                [JsonProperty(LanguageEn ? "Additional setting for logging about mutes in discord" : "Дополнительная настройка для логирования о мутах в дискорд")]
                public LoggedFuncion LoggedMute = new LoggedFuncion();
                internal class LoggedFuncion
                {
                    [JsonProperty(LanguageEn ? "Support for logging the last N messages (Discord logging about mutes must be enabled)" : "Поддержка логирования последних N сообщений (Должно быть включено логирование в дискорд о мутах)")]
                    public Boolean UseHistoryMessage;
                    [JsonProperty(LanguageEn ? "How many latest player messages to send in logging" : "Сколько последних сообщений игрока отправлять в логировании")]
                    public Int32 CountHistoryMessage;
                }

                [JsonProperty(LanguageEn ? "Reasons to block chat" : "Причины для блокировки чата")]
                public List<Muted> MuteChatReasons = new List<Muted>();
                [JsonProperty(LanguageEn ? "Reasons to block your voice" : "Причины для блокировки голоса")]
                public List<Muted> MuteVoiceReasons = new List<Muted>();
                internal class Muted
                {
                    [JsonProperty(LanguageEn ? "Reason for blocking" : "Причина для блокировки")]
                    public String Reason;
                    [JsonProperty(LanguageEn ? "Block time (in seconds)" : "Время блокировки(в секундах)")]
                    public Int32 SecondMute;
                }
            }
            
                        [JsonProperty(LanguageEn ? "Configuring Message Processing" : "Настройка обработки сообщений")]
            public ControllerMessage ControllerMessages = new ControllerMessage();
            internal class ControllerMessage
            {
                [JsonProperty(LanguageEn ? "Basic settings for chat messages from the plugin" : "Основная настройка сообщений в чат от плагина")]
                public GeneralSettings GeneralSetting = new GeneralSettings();
                [JsonProperty(LanguageEn ? "Configuring functionality switching in chat" : "Настройка переключения функционала в чате")]
                public TurnedFuncional TurnedFunc = new TurnedFuncional();
                [JsonProperty(LanguageEn ? "Player message formatting settings" : "Настройка форматирования сообщений игроков")]
                public FormattingMessage Formatting = new FormattingMessage();
  
                
                internal class GeneralSettings
                {
                    [JsonProperty(LanguageEn ? "Notify the player in chat about receiving a prefix/nickname color/chat color (true - yes/false - no)" : "Уведомлять игрока в чате о получении префикса/цвета ника/цвета чата (true - да/false - нет)")]
                    public Boolean alertArgumentsInfoSetup;
                    [JsonProperty(LanguageEn ? "Notify the player in chat about the end of a prefix/nickname color/chat color (true - yes/false - no)" : "Уведомлять игрока в чате о окончании префикса/цвета ника/цвета чата (true - да/false - нет)")]
                    public Boolean alertArgumentsInfoRemove;
                    [JsonProperty(LanguageEn ? "Customizing the chat alert format" : "Настройка формата оповещения в чате")]
                    public BroadcastSettings BroadcastFormat = new BroadcastSettings();
                    [JsonProperty(LanguageEn ? "Setting the mention format in the chat, via @" : "Настройка формата упоминания в чате, через @")]
                    public AlertSettings AlertFormat = new AlertSettings();
                    [JsonProperty(LanguageEn ? "Additional setting" : "Дополнительная настройка")]
                    public OtherSettings OtherSetting = new OtherSettings();

                    internal class BroadcastSettings
                    {
                        [JsonProperty(LanguageEn ? "The name of the notification in the chat" : "Наименование оповещения в чат")]
                        public String BroadcastTitle;
                        [JsonProperty(LanguageEn ? "Chat alert message color" : "Цвет сообщения оповещения в чат")]
                        public String BroadcastColor;
                        [JsonProperty(LanguageEn ? "Steam64ID for chat avatar" : "Steam64ID для аватарки в чате")]
                        public String Steam64IDAvatar;
                    }
                    internal class AlertSettings
                    {
                        [JsonProperty(LanguageEn ? "The color of the player mention message in the chat" : "Цвет сообщения упоминания игрока в чате")]
                        public String AlertPlayerColor;
                        [JsonProperty(LanguageEn ? "Sound when receiving and sending a mention via @" : "Звук при при получении и отправки упоминания через @")]
                        public String SoundAlertPlayer;
                    }
                    internal class OtherSettings
                    {
                        [JsonProperty(LanguageEn ? "Time after which the message will be deleted from the UI from the administrator" : "Время,через которое удалится сообщение с UI от администратора")]
                        public Int32 TimeDeleteAlertUI;

                        [JsonProperty(LanguageEn ? "The size of the message from the player in the chat" : "Размер сообщения от игрока в чате")]
                        public Int32 SizeMessage = 14;
                        [JsonProperty(LanguageEn ? "Player nickname size in chat" : "Размер ника игрока в чате")]
                        public Int32 SizeNick = 14;
                        [JsonProperty(LanguageEn ? "The size of the player's prefix in the chat (will be used if <size=N></size> is not set in the prefix itself)" : "Размер префикса игрока в чате (будет использовано, если в самом префиксе не установвлен <size=N></size>)")]
                        public Int32 SizePrefix = 14;

                        [JsonProperty(LanguageEn ? "Nickname size according to privilege [permission] = size" : "Размер ника по привилегии [permission] = размер")]
                        public Dictionary<String, Int32> sizeNickPrivilages = new Dictionary<String, Int32>();
                        [JsonProperty(LanguageEn ? "Chat message size according to privilege [permission] = size" : "Размер сообщения в чате по привилегии [permission] = размер")]
                        public Dictionary<String, Int32> sizeMessagePrivilages = new Dictionary<String, Int32>();

                        public Int32 GetSizeNickOrMessage(BasePlayer player, Boolean nickOrMessage)
                        {
                            Dictionary<String, Int32> sizePrivilage =
                                nickOrMessage ? sizeNickPrivilages : sizeMessagePrivilages;
		   		 		  						  	   		  		 			  		  		  			 		   			
                            if(sizePrivilage != null && sizePrivilage.Count != 0)
                                foreach (KeyValuePair<String, Int32> privilage in sizePrivilage)
                                {
                                    if (_.permission.UserHasPermission(player.UserIDString, privilage.Key))
                                        return privilage.Value;
                                }

                            return nickOrMessage ? SizeNick : SizeMessage;
                        }
                    }
                }
                internal class TurnedFuncional
                {
                    [JsonProperty(LanguageEn ? "Configuring spam protection" : "Настройка защиты от спама")]
                    public AntiSpam AntiSpamSetting = new AntiSpam();
                    [JsonProperty(LanguageEn ? "Setting up a temporary chat block for newbies (who have just logged into the server)" : "Настройка временной блокировки чата новичкам (которые только зашли на сервер)")]
                    public AntiNoob AntiNoobSetting = new AntiNoob();
                    [JsonProperty(LanguageEn ? "Setting up private messages" : "Настройка личных сообщений")]
                    public PM PMSetting = new PM();

                    internal class AntiNoob
                    {
                        [JsonProperty(LanguageEn ? "Newbie protection in PM/R" : "Защита от новичка в PM/R")]
                        public Settings AntiNoobPM = new Settings();
                        [JsonProperty(LanguageEn ? "Newbie protection in global and team chat" : "Защита от новичка в глобальном и коммандном чате")]
                        public Settings AntiNoobChat = new Settings();
                        internal class Settings
                        {
                            [JsonProperty(LanguageEn ? "Enable protection?" : "Включить защиту?")]
                            public Boolean AntiNoobActivate = false;
                            [JsonProperty(LanguageEn ? "Newbie Chat Lock Time" : "Время блокировки чата для новичка")]
                            public Int32 TimeBlocked = 1200;
                        }
                    }
                    internal class AntiSpam
                    {
                        [JsonProperty(LanguageEn ? "Enable spam protection (Anti-spam)" : "Включить защиту от спама (Анти-спам)")]
                        public Boolean AntiSpamActivate;
                        [JsonProperty(LanguageEn ? "Time after which a player can send a message (AntiSpam)" : "Время через которое игрок может отправлять сообщение (АнтиСпам)")]
                        public Int32 FloodTime;
                        [JsonProperty(LanguageEn ? "Additional Anti-Spam settings" : "Дополнительная настройка Анти-Спама")]
                        public AntiSpamDuples AntiSpamDuplesSetting = new AntiSpamDuples();
                        internal class AntiSpamDuples
                        {
                            [JsonProperty(LanguageEn ? "Enable additional spam protection (Anti-duplicates, duplicate messages)" : "Включить дополнительную защиту от спама (Анти-дубликаты, повторяющие сообщения)")]
                            public Boolean AntiSpamDuplesActivate = true;
                            [JsonProperty(LanguageEn ? "How many duplicate messages does a player need to make to be confused by the system" : "Сколько дублирующих сообщений нужно сделать игроку чтобы его замутила система")]
                            public Int32 TryDuples = 3;
                            [JsonProperty(LanguageEn ? "Setting up automatic muting for duplicates" : "Настройка автоматического мута за дубликаты")]
                            public ControllerMute.Muted MuteSetting = new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Blocking for duplicate messages (SPAM)" : "Блокировка за дублирующие сообщения (СПАМ)",
                                SecondMute = 300,
                            };
                        }
                    }
                    internal class PM
                    {
                        [JsonProperty(LanguageEn ? "Enable Private Messages" : "Включить личные сообщения")]
                        public Boolean PMActivate;
                        [JsonProperty(LanguageEn ? "Sound when receiving a private message" : "Звук при при получении личного сообщения")]
                        public String SoundPM;
                    }
                    [JsonProperty(LanguageEn ? "Enable PM ignore for players (/ignore nick or via interface)" : "Включить игнор ЛС игрокам(/ignore nick или через интерфейс)")]
                    public Boolean IgnoreUsePM;
                    [JsonProperty(LanguageEn ? "Hide the issue of items to the Admin from the chat" : "Скрыть из чата выдачу предметов Админу")]
                    public Boolean HideAdminGave;
                    [JsonProperty(LanguageEn ? "Move mute to team chat (In case of a mute, the player will not be able to write even to the team chat)" : "Переносить мут в командный чат(В случае мута, игрок не сможет писать даже в командный чат)")]
                    public Boolean MuteTeamChat;
                }
                internal class FormattingMessage
                {
                    [JsonProperty(LanguageEn ? "Enable message formatting [Will control caps, message format] (true - yes/false - no)" : "Включить форматирование сообщений [Будет контроллировать капс, формат сообщения] (true - да/false - нет)")]
                    public Boolean FormatMessage;
                    [JsonProperty(LanguageEn ? "Use a list of banned words (true - yes/false - no)" : "Использовать список запрещенных слов (true - да/false - нет)")]
                    public Boolean UseBadWords;
                    [JsonProperty(LanguageEn ? "The word that will replace the forbidden word" : "Слово которое будет заменять запрещенное слово")]
                    public String ReplaceBadWord;
                    [JsonProperty(LanguageEn ? "The list of forbidden words [Forbidden Word] = Whether to replace it in part of the word (for example, Vasya Fucking => Vasya ***) (true - yes/false - no)" : "Список запрещенных слов [ЗапрещенноеСлово] = Заменять ли его в части слова (например ВасяБля => Вася***) (true - да/false - нет)")]
                    public Dictionary<String, Boolean> BadWords = new Dictionary<String, Boolean>();

                    [JsonProperty(LanguageEn ? "Nickname controller setup" : "Настройка контроллера ников")]
                    public NickController ControllerNickname = new NickController();
                    internal class NickController
                    {
                        [JsonProperty(LanguageEn ? "Enable player nickname formatting (message formatting must be enabled)" : "Включить форматирование ников игроков (должно быть включено форматирование сообщений)")]
                        public Boolean UseNickController = true;
                        [JsonProperty(LanguageEn ? "The word that will replace the forbidden word (You can leave it blank and it will just delete)" : "Слово которое будет заменять запрещенное слово (Вы можете оставить пустым и будет просто удалять)")]
                        public String ReplaceBadNick = "****";
                        [JsonProperty(LanguageEn ? "The list of forbidden nicknames [Forbidden Word] = Whether to replace it in part of the word (for example, Vasya Fucking => Vasya ***) (true - yes/false - no)" : "Список запрещенных ников [ЗапрещенноеСлово] = Заменять ли его в части слова (например ВасяБля => Вася***) (true - да/false - нет)")]
                        public Dictionary<String, Boolean> BadNicks = new Dictionary<String, Boolean>();
                        [JsonProperty(LanguageEn ? "List of allowed links in nicknames" : "Список разрешенных ссылок в никах")]
                        public List<String> AllowedLinkNick = new List<String>();
                    }
                }
            }

            
            
            [JsonProperty(LanguageEn ? "Setting up chat alerts" : "Настройка оповещений в чате")]
            public ControllerAlert ControllerAlertSetting;

            internal class ControllerAlert
            {
                [JsonProperty(LanguageEn ? "Setting up chat alerts" : "Настройка оповещений в чате")]
                public Alert AlertSetting;
                [JsonProperty(LanguageEn ? "Setting notifications about the status of the player's session" : "Настройка оповещений о статусе сессии игрока")]
                public PlayerSession PlayerSessionSetting;
                [JsonProperty(LanguageEn ? "Configuring administrator session status alerts" : "Настройка оповещений о статусе сессии администратора")]
                public AdminSession AdminSessionSetting;
                [JsonProperty(LanguageEn ? "Setting up personal notifications to the player when connecting" : "Настройка персональных оповоещений игроку при коннекте")]
                public PersonalAlert PersonalAlertSetting;
                internal class Alert
                {
                    [JsonProperty(LanguageEn ? "Enable automatic messages in chat (true - yes/false - no)" : "Включить автоматические сообщения в чат (true - да/false - нет)")]
                    public Boolean AlertMessage;
                    [JsonProperty(LanguageEn ? "Type of automatic messages : true - sequential / false - random" : "Тип автоматических сообщений : true - поочередные/false - случайные")]
                    public Boolean AlertMessageType;

                    [JsonProperty(LanguageEn ? "List of automatic messages in chat" : "Список автоматических сообщений в чат")]
                    public LanguageController MessageList = new LanguageController();
                    [JsonProperty(LanguageEn ? "Interval for sending messages to chat (Broadcaster) (in seconds)" : "Интервал отправки сообщений в чат (Броадкастер) (в секундах)")]
                    public Int32 MessageListTimer;
                }
                internal class PlayerSession
                {
                    [JsonProperty(LanguageEn ? "When a player is notified about the entry / exit of the player, display his avatar opposite the nickname (true - yes / false - no)" : "При уведомлении о входе/выходе игрока отображать его аватар напротив ника (true - да/false - нет)")]
                    public Boolean ConnectedAvatarUse;

                    [JsonProperty(LanguageEn ? "Notify in chat when a player enters (true - yes/false - no)" : "Уведомлять в чате о входе игрока (true - да/false - нет)")]
                    public Boolean ConnectedAlert;
                    [JsonProperty(LanguageEn ? "Enable random notifications when a player from the list enters (true - yes / false - no)" : "Включить случайные уведомления о входе игрока из списка (true - да/false - нет)")]
                    public Boolean ConnectionAlertRandom;
                    [JsonProperty(LanguageEn ? "Show the country of the entered player (true - yes/false - no)" : "Отображать страну зашедшего игрока (true - да/false - нет")]
                    public Boolean ConnectedWorld;

                    [JsonProperty(LanguageEn ? "Notify when a player enters the chat (selected from the list) (true - yes/false - no)" : "Уведомлять о выходе игрока в чат(выбираются из списка) (true - да/false - нет)")]
                    public Boolean DisconnectedAlert;
                    [JsonProperty(LanguageEn ? "Enable random player exit notifications (true - yes/false - no)" : "Включить случайные уведомления о выходе игрока (true - да/false - нет)")]
                    public Boolean DisconnectedAlertRandom;
                    [JsonProperty(LanguageEn ? "Display reason for player exit (true - yes/false - no)" : "Отображать причину выхода игрока (true - да/false - нет)")]
                    public Boolean DisconnectedReason;

                    [JsonProperty(LanguageEn ? "Random player entry notifications({0} - player's nickname, {1} - country (if country display is enabled)" : "Случайные уведомления о входе игрока({0} - ник игрока, {1} - страна(если включено отображение страны)")]
                    public LanguageController RandomConnectionAlert = new LanguageController();
                    [JsonProperty(LanguageEn ? "Random notifications about the exit of the player ({0} - player's nickname, {1} - the reason for the exit (if the reason is enabled)" : "Случайные уведомления о выходе игрока({0} - ник игрока, {1} - причина выхода(если включена причина)")]
                    public LanguageController RandomDisconnectedAlert = new LanguageController();
                }
                internal class AdminSession
                {
                    [JsonProperty(LanguageEn ? "Notify admin on the server in the chat (true - yes/false - no)" : "Уведомлять о входе админа на сервер в чат (true - да/false - нет)")]
                    public Boolean ConnectedAlertAdmin;
                    [JsonProperty(LanguageEn ? "Notify about admin leaving the server in chat (true - yes/false - no)" : "Уведомлять о выходе админа на сервер в чат (true - да/false - нет)")]
                    public Boolean DisconnectedAlertAdmin;
                }
                internal class PersonalAlert
                {
                    [JsonProperty(LanguageEn ? "Enable random message to the player who has logged in (true - yes/false - no)" : "Включить случайное сообщение зашедшему игроку (true - да/false - нет)")]
                    public Boolean UseWelcomeMessage;
                    [JsonProperty(LanguageEn ? "List of messages to the player when entering" : "Список сообщений игроку при входе")]
                    public LanguageController WelcomeMessage = new LanguageController();
                }
            }
		   		 		  						  	   		  		 			  		  		  			 		   			
            public class LanguageController
            {
                [JsonProperty(LanguageEn ? "Setting up Multilingual Messages [Language Code] = Translation Variations" : "Настройка мультиязычных сообщений [КодЯзыка] = ВариацииПеревода")]
                public Dictionary<String, List<String>> LanguageMessages = new Dictionary<String, List<String>>();
            }

            
                        [JsonProperty(LanguageEn ? "Settings Rust+" : "Настройка Rust+")]
            public RustPlus RustPlusSettings;
            internal class RustPlus
            {
                [JsonProperty(LanguageEn ? "Use Rust+" : "Использовать Rust+")]
                public Boolean UseRustPlus;
                [JsonProperty(LanguageEn ? "Title for notification Rust+" : "Название для уведомления Rust+")]
                public String DisplayNameAlert;
            }
            
                        [JsonProperty(LanguageEn ? "Configuring support plugins" : "Настройка плагинов поддержки")]
            public ReferenceSettings ReferenceSetting = new ReferenceSettings();
            internal class ReferenceSettings
            {
                [JsonProperty(LanguageEn ? "Settings XLevels" : "Настройка XLevels")]
                public XLevels XLevelsSettings = new XLevels();
                [JsonProperty(LanguageEn ? "Settings IQFakeActive" : "Настройка IQFakeActive")]
                public IQFakeActive IQFakeActiveSettings = new IQFakeActive();
                [JsonProperty(LanguageEn ? "Settings IQRankSystem" : "Настройка IQRankSystem")]
                public IQRankSystem IQRankSystems = new IQRankSystem();
                [JsonProperty(LanguageEn ? "Settings Clans" : "Настройка Clans")]
                public Clans ClansSettings = new Clans();
                [JsonProperty(LanguageEn ? "Settings TranslationAPI" : "Настройка TranslationAPI")]
                public TranslataionApi translationApiSettings = new TranslataionApi();    
                [JsonProperty(LanguageEn ? "Settings SkillTree" : "Настройка SkillTree")]
                public SkillTree skillTreeSettings = new SkillTree();
                [JsonProperty(LanguageEn ? "Settings PlayerRanks" : "Настройка PlayerRanks")]
                public PlayerRanks playerRanksSettings = new PlayerRanks();
                [JsonProperty(LanguageEn ? "Settings XPrison" : "Настройка XPrison")]
                public XPrison xPrisonSettings = new XPrison();
                internal class TranslataionApi
                {
                    [JsonProperty(LanguageEn ? "To use automatic message translation using the TranslationAPI" : "Использовать автоматический перевод сообщений с помощью TranslataionAPI")]
                    public Boolean useTranslationApi;
                    [JsonProperty(LanguageEn ? "Translate team chat" : "Переводить командный чат")]
                    public Boolean translateTeamChat;
                    [JsonProperty(LanguageEn ? "Translate chat in private messages." : "Переводить чат в личных сообщениях")] 
                    public Boolean translatePmChat;
                    [JsonProperty(LanguageEn ? "The code for the preferred language (leave it empty, and then the translation will be done in each player's language)" : "Код приоритетного языка (оставьте пустым и тогда для каждого игрока будет переводиться на его языке клиента)")]
                    public String codeLanguagePrimary;
                }
                internal class Clans
                {
                    [JsonProperty(LanguageEn ? "Display a clan tag in the chat (if Clans are installed)" : "Отображать в чате клановый тэг (если установлены Clans)")]
                    public Boolean UseClanTag;
                    [JsonProperty(LanguageEn ? "The color of the additional tag" : "Цвет дополнительного тэга")]
                    public String colorTag;
                }
                internal class IQRankSystem
                {
                    [JsonProperty(LanguageEn ? "Rank display format in chat ( {0} is the user's rank, do not delete this value)" : "Формат отображения ранга в чате ( {0} - это ранг юзера, не удаляйте это значение)")]
                    public String FormatRank = "[{0}]";
                    [JsonProperty(LanguageEn ? "Time display format with IQRank System in chat ( {0} is the user's time, do not delete this value)" : "Формат отображения времени с IQRankSystem в чате ( {0} - это время юзера, не удаляйте это значение)")]
                    public String FormatRankTime = "[{0}]";
                    [JsonProperty(LanguageEn ? "Use support IQRankSystem" : "Использовать поддержку рангов")]
                    public Boolean UseRankSystem;
                    [JsonProperty(LanguageEn ? "Show players their played time next to their rank" : "Отображать игрокам их отыгранное время рядом с рангом")]
                    public Boolean UseTimeStandart;
                }
                internal class IQFakeActive
                {
                    [JsonProperty(LanguageEn ? "Use support IQFakeActive" : "Использовать поддержку IQFakeActive")]
                    public Boolean UseIQFakeActive;
                }
                internal class XLevels
                {
                    [JsonProperty(LanguageEn ? "Use support XLevels" : "Использовать поддержку XLevels")]
                    public Boolean UseXLevels;
                    [JsonProperty(LanguageEn ? "Use full prefix with level from XLevel (true) otherwise only level (false)" : "Использовать полный префикс с уровнем из XLevel (true) иначе только уровень (false)")]
                    public Boolean UseFullXLevels;
                    [JsonProperty(LanguageEn ? "The color of the additional tag" : "Цвет дополнительного тэга")]
                    public String colorTag;
                }
                
                internal class XPrison
                {
                    [JsonProperty(LanguageEn ? "Use support XPrison" : "Использовать поддержку XPrison")]
                    public Boolean UseXPrison;
                    [JsonProperty(LanguageEn ? "The color of the additional tag" : "Цвет дополнительного тэга")]
                    public String colorTag;
                }
                
                internal class SkillTree
                {
                    [JsonProperty(LanguageEn ? "Use support SkillTree" : "Использовать поддержку SkillTree")]
                    public Boolean UseSkillTree;
                    [JsonProperty(LanguageEn ? "Use full XP + Level information output (true), use only Level (false)" : "Использовать полный вывод информации XP + Level (true), использовать только Level (false)")]
                    public Boolean UseFullSkillTree;
                    [JsonProperty(LanguageEn ? "Use prestige" : "Использовать престиж")]
                    public Boolean UsePrestigeSkillTree;
                    [JsonProperty(LanguageEn ? "The color of the additional tag" : "Цвет дополнительного тэга")]
                    public String colorTag;
                }
                
                internal class PlayerRanks
                {
                    [JsonProperty(LanguageEn ? "Use support PlayerRanks" : "Использовать поддержку PlayerRanks")]
                    public Boolean UsePlayerRanks;
                    [JsonProperty(LanguageEn ? "The color of the additional tag" : "Цвет дополнительного тэга")]
                    public String colorTag;
                }
            }
            
            
            [JsonProperty(LanguageEn ? "Setting up an answering machine" : "Настройка автоответчика")]
            public AnswerMessage AnswerMessages = new AnswerMessage();

            internal class AnswerMessage
            {
                [JsonProperty(LanguageEn ? "Enable auto-reply? (true - yes/false - no)" : "Включить автоответчик?(true - да/false - нет)")]
                public bool UseAnswer;
                [JsonProperty(LanguageEn ? "Customize Messages [Keyword] = Reply" : "Настройка сообщений [Ключевое слово] = Ответ")]
                public Dictionary<String, LanguageController> AnswerMessageList = new Dictionary<String, LanguageController>();
            }

            
            
            [JsonProperty(LanguageEn ? "Disable additional chat duplication in RCON" : "Отключить дополнительное дублированиеи чата в RCON")]
            public Boolean disableRconBroadcast;
            [JsonProperty(LanguageEn ? "Additional setting" : "Дополнительная настройка")]
            public OtherSettings OtherSetting;

            internal class OtherSettings
            {
                [JsonProperty("SteamApiKey (https://steamcommunity.com/dev/apikey)")]
                public String renameSteamApiKey;
                [JsonProperty(LanguageEn ? "Enable the /online command (true - yes / false - no)" : "Включить команду /online (true - да/ false - нет)")]
                public Boolean UseCommandOnline;
                [JsonProperty(LanguageEn ? "Use shortened format /online (will only display quantity)" : "Использовать сокращенный формат /online (будет отображать только количество)")]
                public Boolean UseCommandShortOnline;
                [JsonProperty(LanguageEn ? "Compact logging of messages" : "Компактное логирование сообщений")]
                public CompactLoggetChat CompactLogsChat = new CompactLoggetChat();
                [JsonProperty(LanguageEn ? "Setting up message logging" : "Настройка логирования сообщений")]
                public LoggedChat LogsChat = new LoggedChat();
                [JsonProperty(LanguageEn ? "Setting up logging of personal messages of players" : "Настройка логирования личных сообщений игроков")]
                public General LogsPMChat = new General();
                [JsonProperty(LanguageEn ? "Setting up chat/voice lock/unlock logging" : "Настройка логирования блокировок/разблокировок чата/голоса")]
                public General LogsMuted = new General();
                [JsonProperty(LanguageEn ? "Setting up logging of chat commands from players" : "Настройка логирования чат-команд от игроков")]
                public General LogsChatCommands = new General();

                internal class CompactLoggetChat
                {
                    [JsonProperty(LanguageEn ? "Display Steam64ID in the log (true - yes/false - no)" : "Отображать в логе Steam64ID (true - да/false - нет)")]
                    public Boolean ShowSteamID;
                    [JsonProperty(LanguageEn ? "Setting up compact message logging" : "Настройка компактного логирования сообщений")]
                    public LoggedChat LogsCompactChat = new LoggedChat();
                }
                internal class LoggedChat
                {
                    [JsonProperty(LanguageEn ? "Setting up general chat logging" : "Настройка логирования общего чата")]
                    public General GlobalChatSettings = new General();
                    [JsonProperty(LanguageEn ? "Setting up team chat logging" : "Настройка логирования тим чата")]
                    public General TeamChatSettings = new General();
                }
                internal class General
                {
                    [JsonProperty(LanguageEn ? "Enable logging (true - yes/false - no)" : "Включить логирование (true - да/false - нет)")]
                    public Boolean UseLogged = false;
                    [JsonProperty(LanguageEn ? "Webhooks channel for logging" : "Webhooks канала для логирования")]
                    public String Webhooks = "";
                }
            }
            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                                        ControllerParameter = new ControllerParameters
                    {
                        VisualParametres = new ControllerParameters.VisualSettingParametres
                        {
                            PrefixType = SelectedParametres.DropList,
                            ChatColorType = SelectedParametres.DropList,
                            NickColorType = SelectedParametres.Slider,
                            IQRankSystemType = SelectedParametres.Slider,
                        },
                        Prefixes = new ControllerParameters.PrefixSetting
                        {
                            TurnMultiPrefixes = false,
                            MaximumMultiPrefixCount = 5,
                            Prefixes = new List<ControllerParameters.AdvancedFuncion>
                              {
                                  new ControllerParameters.AdvancedFuncion
                                  {
                                      Argument = LanguageEn ? "<color=#CC99FF>[PLAYER]</color>" : "<color=#CC99FF>[ИГРОК]</color>",
                                      Permissions = "iqchat.default",
                                      IsBlockSelected = false,
                                  },
                                  new ControllerParameters.AdvancedFuncion
                                  {
                                      Argument = "<color=#ffff99>[VIP]</color>",
                                      Permissions = "iqchat.admin",
                                      IsBlockSelected = false,
                                  },
                                  new ControllerParameters.AdvancedFuncion
                                  {
                                      Argument = LanguageEn ? "<color=#ff9999>[ADMIN]</color>" : "<color=#ff9999>[АДМИН]</color>",
                                      Permissions = "iqchat.admin",
                                      IsBlockSelected = false,
                                  },
                            },
                        },
                        MessageColorList = new List<ControllerParameters.AdvancedFuncion>
                        {
                               new ControllerParameters.AdvancedFuncion
                               {
                                    Argument = "#CC99FF",
                                    Permissions = "iqchat.default",
                                    IsBlockSelected = false,
                               },
                               new ControllerParameters.AdvancedFuncion
                               {
                                    Argument = "#ffff99",
                                    Permissions = "iqchat.admin",
                                    IsBlockSelected = false,
                               },
                               new ControllerParameters.AdvancedFuncion
                               {
                                    Argument = "#ff9999",
                                    Permissions = "iqchat.admin",
                                    IsBlockSelected = false,
                               },
                        },
                        NickColorList = new List<ControllerParameters.AdvancedFuncion>
                        {
                               new ControllerParameters.AdvancedFuncion
                               {
                                    Argument = "#CC99FF",
                                    Permissions = "iqchat.default",
                                    IsBlockSelected = false,
                               },
                               new ControllerParameters.AdvancedFuncion
                               {
                                    Argument = "#ffff99",
                                    Permissions = "iqchat.admin",
                                    IsBlockSelected = false,
                               },
                               new ControllerParameters.AdvancedFuncion
                               {
                                    Argument = "#ff9999",
                                    Permissions = "iqchat.admin",
                                    IsBlockSelected = false,
                               },
                        },
                    },
                    
                    
                    ControllerConnect = new ControllerConnection
                    {
                        SetupDefaults = new ControllerConnection.SetupDefault
                        {
                            PrefixDefault = LanguageEn ? "<color=#CC99FF>[PLAYER]</color>" : "<color=#CC99FF>[ИГРОК]</color>",
                            MessageDefault = "#eee",
                            NickDefault = "#0099FF",
                        },
                        Turneds = new ControllerConnection.Turned
                        {
                            TurnAutoDropColorChat = true,
                            TurnAutoDropColorNick = true,
                            TurnAutoDropPrefix = true,
                            TurnAutoSetupColorChat = true,
                            TurnAutoSetupColorNick = true,
                            TurnAutoSetupPrefix = true,
                        }
                    },

                    
                    
                    ControllerMutes = new ControllerMute
                    {
                        mutedPM = false,
                        LoggedMute = new ControllerMute.LoggedFuncion
                        {
                            UseHistoryMessage = false,
                            CountHistoryMessage = 10,
                        },
                        AutoMuteSettings = new ControllerMute.AutoMute
                        {
                            UseAutoMute = true,
                            AutoMuted = new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Automatic chat blocking" : "Автоматическая блокировка чата",
                                SecondMute = 300,
                            }
                        },
                        MuteChatReasons = new List<ControllerMute.Muted>
                        {
                            new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Aggressive behavior" : "Агрессивное поведение",
                                SecondMute = 100,
                            },
                            new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Insults" : "Оскорбления",
                                SecondMute = 300,
                            },
                            new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Insult (repeated violation)" : "Оскорбление (повторное нарушение)",
                                SecondMute = 1000,
                            },
                            new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Advertising" : "Реклама",
                                SecondMute = 5000,
                            },
                            new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Humiliation" : "Унижение",
                                SecondMute = 300,
                            },
                            new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Spam" : "Спам",
                                SecondMute = 60,
                            },
                        },
                        MuteVoiceReasons = new List<ControllerMute.Muted>
                        {
                            new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Aggressive behavior" : "Агрессивное поведение",
                                SecondMute = 100,
                            },
                            new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Insults" : "Оскорбления",
                                SecondMute = 300,
                            },
                            new ControllerMute.Muted
                            {
                                Reason = LanguageEn ? "Disruption of the event by shouting" : "Срыв мероприятия криками",
                                SecondMute = 300,
                            },
                        }
                    },

                    
                    
                    ControllerMessages = new ControllerMessage
                    {
                        Formatting = new ControllerMessage.FormattingMessage
                        {
                            UseBadWords = true,
                            BadWords = LanguageEn ? new Dictionary<String, Boolean>() { ["fucking"] = false, ["sucking"] = false, ["fuckyou"] = false, ["fuck"] = true } : new Dictionary<String, Boolean>() { ["бля"] = false, ["сука"] = false, ["говно"] = false, ["нахуй"] = true },
                            FormatMessage = true, 
                            ReplaceBadWord = "***",
                            ControllerNickname = new ControllerMessage.FormattingMessage.NickController
                            {
                                BadNicks = LanguageEn ? new Dictionary<String, Boolean>() { ["Admin"] = true, ["Owner"] = true, ["Moderator"] = true, ["Helper"] = true } : new Dictionary<String, Boolean>() { ["Админ"] = true, ["Модератор"] = true, ["Модер"] = true, ["Хелпер"] = true },
                                AllowedLinkNick = new List<String> { "mysite.com" },
                                ReplaceBadNick = "",
                                UseNickController = true,
                            },
                        },
                        TurnedFunc = new ControllerMessage.TurnedFuncional
                        {
                            HideAdminGave = true,
                            IgnoreUsePM = true,
                            MuteTeamChat = true,
                            AntiNoobSetting = new ControllerMessage.TurnedFuncional.AntiNoob
                            {
                                AntiNoobChat = new ControllerMessage.TurnedFuncional.AntiNoob.Settings
                                {
                                    AntiNoobActivate = false,
                                    TimeBlocked = 1200,
                                },
                                AntiNoobPM = new ControllerMessage.TurnedFuncional.AntiNoob.Settings
                                {
                                    AntiNoobActivate = false,
                                    TimeBlocked = 1200,
                                },
                            },
                            AntiSpamSetting = new ControllerMessage.TurnedFuncional.AntiSpam
                            {
                                AntiSpamActivate = true,
                                FloodTime = 10,
                                AntiSpamDuplesSetting = new ControllerMessage.TurnedFuncional.AntiSpam.AntiSpamDuples
                                {
                                    AntiSpamDuplesActivate = true,
                                    MuteSetting = new ControllerMute.Muted
                                    {
                                        Reason = LanguageEn ? "Duplicate messages (SPAM)" : "Повторяющиеся сообщения (СПАМ)",
                                        SecondMute = 300,
                                    },
                                    TryDuples = 3,
                                }
                            },
                            PMSetting = new ControllerMessage.TurnedFuncional.PM
                            {
                                PMActivate = true,
                                SoundPM = "assets/bundled/prefabs/fx/notice/stack.world.fx.prefab",
                            },
                        },
                        GeneralSetting = new ControllerMessage.GeneralSettings
                        {
                            alertArgumentsInfoRemove = true,
                            alertArgumentsInfoSetup = true,
                            BroadcastFormat = new ControllerMessage.GeneralSettings.BroadcastSettings
                            {
                                BroadcastColor = "#efedee",
                                BroadcastTitle = LanguageEn ? "<color=#68cacd><b>[Alert]</b></color>" : "<color=#68cacd><b>[ОПОВЕЩЕНИЕ]</b></color>",
                                Steam64IDAvatar = "0",
                            },
                            AlertFormat = new ControllerMessage.GeneralSettings.AlertSettings
                            {
                                AlertPlayerColor = "#efedee",
                                SoundAlertPlayer = "assets/bundled/prefabs/fx/notice/item.select.fx.prefab",
                            },
                            OtherSetting = new ControllerMessage.GeneralSettings.OtherSettings
                            {
                                TimeDeleteAlertUI = 5,
                                SizePrefix = 14,
                                SizeMessage = 14,
                                SizeNick = 14,
                                sizeMessagePrivilages = new Dictionary<String, Int32>()
                                {
                                    ["iqchat.bigBoy"] = 16,
                                },
                                sizeNickPrivilages = new Dictionary<String, Int32>()
                                {
                                    ["iqchat.bigBoy"] = 16,
                                }
                            }
                        },
                    },

                    
                    
                    ControllerAlertSetting = new ControllerAlert
                    {
                        AlertSetting = new ControllerAlert.Alert
                        {
                            AlertMessage = true,
                            AlertMessageType = false,
                            MessageList = new LanguageController()
                            {
                                LanguageMessages = new Dictionary<String, List<String>>()
                                {
                                    ["en"] = new List<String>()
                                    {
                                        "Automatic message #1 (Edit in configuration)",
                                        "Automatic message #2 (Edit in configuration)",
                                        "Automatic message #3 (Edit in configuration)",
                                        "Automatic message #4 (Edit in configuration)",
                                        "Automatic message #5 (Edit in configuration)",
                                        "Automatic message #6 (Edit in configuration)",
                                    },
                                    ["ru"] = new List<String>()
                                    {
                                        "Автоматическое сообщение #1 (Редактировать в конфигурации)",
                                        "Автоматическое сообщение #2 (Редактировать в конфигурации)",
                                        "Автоматическое сообщение #3 (Редактировать в конфигурации)",
                                        "Автоматическое сообщение #4 (Редактировать в конфигурации)",
                                        "Автоматическое сообщение #5 (Редактировать в конфигурации)",
                                        "Автоматическое сообщение #6 (Редактировать в конфигурации)",
                                    }
                                },
                            },
                            MessageListTimer = 60,
                        },
                        AdminSessionSetting = new ControllerAlert.AdminSession
                        {
                            ConnectedAlertAdmin = false,
                            DisconnectedAlertAdmin = false,
                        },
                        PlayerSessionSetting = new ControllerAlert.PlayerSession
                        {
                            ConnectedAlert = true,
                            ConnectedAvatarUse = true,
                            ConnectedWorld = true,
                            ConnectionAlertRandom = false,

                            DisconnectedAlert = true,
                            DisconnectedAlertRandom = false,
                            DisconnectedReason = true,

                            RandomConnectionAlert = new LanguageController
                            {
                                LanguageMessages = new Dictionary<String, List<String>>()
                                {
                                    ["en"] = new List<String>()
                                    {
                                        "{0} flew in from {1}",
                                        "{0} flew into the server from{1}",
                                        "{0} jumped on a server"
                                    },
                                    ["ru"] = new List<String>()
                                    {
                                        "{0} влетел как дурачок из {1}",
                                        "{0} залетел на сервер из {1}, соболезнуем",
                                        "{0} прыгнул на сервачок"
                                    }
                                }
                            },
                            RandomDisconnectedAlert = new LanguageController()
                            {
                                LanguageMessages = new Dictionary<String, List<String>>()
                                {
                                    ["en"] = new List<String>()
                                    {
                                        "{0} gone to another world",
                                        "{0} left the server with a reason {1}",
                                        "{0} went to another server"
                                    },
                                    ["ru"] = new List<String>()
                                    {
                                        "{0} ушел в мир иной",
                                        "{0} вылетел с сервера с причиной {1}",
                                        "{0} пошел на другой сервачок"
                                    }
                                }
                            },
                        },
                        PersonalAlertSetting = new ControllerAlert.PersonalAlert
                        {
                            UseWelcomeMessage = true,
                            WelcomeMessage = new LanguageController
                            {
                                LanguageMessages = new Dictionary<String, List<String>>()
                                {
                                    ["en"] = new List<String>()
                                    {
                                        "Welcome to the server SUPERSERVER\nWe are glad that you chose us!",
                                        "Welcome back to the server!\nWe wish you good luck",
                                        "Welcome to the server\nWe have the best plugins",
                                    },
                                    ["ru"] = new List<String>()
                                    {
                                        "Добро пожаловать на сервер SUPERSERVER\nРады,что выбрал именно нас!",
                                        "С возвращением на сервер!\nЖелаем тебе удачи",
                                        "Добро пожаловать на сервер\nУ нас самые лучшие плагины",
                                    }
                                }
                            },
                        }
                    },

                    
                    
                    ReferenceSetting = new ReferenceSettings
                    {
                        playerRanksSettings = new ReferenceSettings.PlayerRanks()
                        {
                            UsePlayerRanks = false,
                            colorTag = "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00",
                        },
                        skillTreeSettings = new ReferenceSettings.SkillTree
                        {
                            UseSkillTree = false,
                            UseFullSkillTree = false,
                            UsePrestigeSkillTree = false,
                            colorTag = "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00",
                        },
                        translationApiSettings = new ReferenceSettings.TranslataionApi()
                        {
                            useTranslationApi = false,
                            translateTeamChat = false,
                            translatePmChat = true,
                            codeLanguagePrimary = "",
                        },
                        IQFakeActiveSettings = new ReferenceSettings.IQFakeActive
                        {
                            UseIQFakeActive = true,
                        },
                        IQRankSystems = new ReferenceSettings.IQRankSystem
                        {
                            FormatRank = "[{0}]",
                            FormatRankTime = "[{0}]",
                            UseRankSystem = false,
                            UseTimeStandart = true
                        },
                        xPrisonSettings = new ReferenceSettings.XPrison()
                        {
                            UseXPrison = false,
                            colorTag = "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00",
                        },
                        XLevelsSettings = new ReferenceSettings.XLevels()
                        {
                            UseXLevels = false,
                            UseFullXLevels = false,
                            colorTag = "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00",
                        },
                        ClansSettings = new ReferenceSettings.Clans()
                        {
                            UseClanTag = false,
                            colorTag = "#FF0000, #FF3300, #FF6600, #FF9900, #FFCC00, #FFFF00, #CCFF00, #99FF00, #66FF00, #33FF00",
                        }
                    },

                    
                    
                    RustPlusSettings = new RustPlus
                    {
                        UseRustPlus = true,
                        DisplayNameAlert = LanguageEn ? "SUPER SERVER" : "СУПЕР СЕРВЕР",
                    },

                    
                    
                    AnswerMessages = new AnswerMessage
                    {
                        UseAnswer = true,
                        AnswerMessageList = new Dictionary<String, LanguageController>()
                        {
                            ["wipe"] = new LanguageController()
                            {
                                LanguageMessages = new Dictionary<String, List<String>>()
                                {
                                    ["en"] = new List<String>()
                                    {
                                        "Wipe will be 27.06"
                                    },
                                    ["ru"] = new List<String>()
                                    {
                                        "Вайп будет 27.06"
                                    }
                                }
                            },
                            ["читер"] = new LanguageController()
                            {
                                LanguageMessages = new Dictionary<String, List<String>>()
                                {
                                    ["en"] = new List<String>()
                                    {
                                        "Found a cheater? Write /report and send a complaint"
                                    },
                                    ["ru"] = new List<String>()
                                    {
                                        "Нашли читера?Напиши /report и отправь жалобу"
                                    }
                                }
                            }
                        },
                    },

                    
                                        disableRconBroadcast = true,
                    OtherSetting = new OtherSettings
                    {
                        renameSteamApiKey = "",
                        UseCommandOnline = false,
                        UseCommandShortOnline = true,
                        CompactLogsChat = new OtherSettings.CompactLoggetChat
                        {
                            ShowSteamID = false,
                            LogsCompactChat = new OtherSettings.LoggedChat
                            {
                                GlobalChatSettings = new OtherSettings.General
                                {
                                    UseLogged = false,
                                    Webhooks = "",
                                },
                                TeamChatSettings = new OtherSettings.General
                                {
                                    UseLogged = false,
                                    Webhooks = "",
                                }
                            }
                        },
                        LogsChat = new OtherSettings.LoggedChat
                        {
                            GlobalChatSettings = new OtherSettings.General
                            {
                                UseLogged = false,
                                Webhooks = "",
                            },
                            TeamChatSettings = new OtherSettings.General
                            {
                                UseLogged = false,
                                Webhooks = "",
                            }
                        },
                        LogsChatCommands = new OtherSettings.General
                        {
                            UseLogged = false,
                            Webhooks = "",
                        },
                        LogsPMChat = new OtherSettings.General
                        {
                            UseLogged = false,
                            Webhooks = "",
                        },
                        LogsMuted = new OtherSettings.General
                        {
                            UseLogged = false,
                            Webhooks = "",
                        },
                    },

                                    };
            }
        }
        public Object IsGradientColorValue(String value)
        {
            if (String.IsNullOrEmpty(value)) return value?.Trim();
            if (!value.Contains(",")) return value.Trim();
            List<String> colors = new(value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            for (Int32 i = 0; i < colors.Count; i++)
                colors[i] = colors[i].Trim();
    
            return colors;
        }
        private void DrawUI_IQChat_Update_MuteVoice_All(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionMutedAdmin)) return;

            String InterfaceAdministratorVoice = InterfaceBuilder.GetInterface("UI_Chat_Administation_AllVoce");
            if (InterfaceAdministratorVoice == null) return;

            InterfaceAdministratorVoice = InterfaceAdministratorVoice.Replace("%TEXT_MUTE_ALLVOICE%", GetLang(!GeneralInfo.TurnMuteAllVoice ? "IQCHAT_BUTTON_MODERATION_MUTE_ALL_VOICE" : "IQCHAT_BUTTON_MODERATION_UNMUTE_ALL_VOICE", player.UserIDString));
            InterfaceAdministratorVoice = InterfaceAdministratorVoice.Replace("%COMMAND_MUTE_ALLVOICE%", $"newui.cmd action.mute.ignore mute.controller {SelectedAction.Mute} mute.all.voice");

            CuiHelper.DestroyUi(player, "ModeratorMuteAllVoice");
            CuiHelper.AddUi(player, InterfaceAdministratorVoice);
        }
        
        [ConsoleCommand("mutevoice")]
        void MuteCustomVoiceAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermissionMute)) return;
            if (arg == null || arg.Args == null || arg.Args.Length != 3 || arg.Args.Length > 3)
            {
                PrintWarning(LanguageEn ? "Invalid syntax, use : mutevoice Steam64ID/Nick Reason Time(seconds)" : "Неверный синтаксис,используйте : mutevoice Steam64ID/Ник Причина Время(секунды)");
                return;
            }
            string NameOrID = arg.Args[0];
            string Reason = arg.Args[1];
            Int32 TimeMute = 0;
            if (!Int32.TryParse(arg.Args[2], out TimeMute))
            {
                PrintWarning(LanguageEn ? "Enter time in numbers!" : "Введите время цифрами!");
                return;
            }
            
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (Info.MuteInfo.IsMute(MuteType.Voice))
                        {
                            PrintWarning(LanguageEn ? "The player already has a voice lock" : "Игрок уже имеет блокировку голосового чата");
                            return;
                        }

                        Info.MuteInfo.SetMute(MuteType.Voice, TimeMute);
                        PrintWarning(LanguageEn ? "Voice blocking issued to offline player" : "Блокировка голосового чата выдана offline-игроку");
                        return;
                    }
                    else
                    {
                        PrintWarning(LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    PrintWarning(LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }

            MutePlayer(target, MuteType.Voice, 0, arg.Player(), Reason, TimeMute, false, true);
            Puts(LanguageEn ? "Successfully" : "Успешно");
        }

        [ConsoleCommand("alertui")]
        private void AlertUIConsoleCommand(ConsoleSystem.Arg args)
        {
            BasePlayer Sender = args.Player();
            if (Sender != null)
                if (!permission.UserHasPermission(Sender.UserIDString, PermissionAlert)) return;
            AlertUI(Sender, args.Args);
        }
        private const String PermissionHideOnline = "iqchat.onlinehide";
        private void DrawUI_IQChat_Mute_And_Ignore_Player(BasePlayer player, SelectedAction Action, IEnumerable<BasePlayer> PlayerList, IEnumerable<FakePlayer> FakePlayerList = null)
        {
            User MyInfo = UserInformation[player.userID];
            if (MyInfo == null) return;
            Int32 X = 0, Y = 0;
            String ColorGreen = "0.5803922 1 0.5372549 1";
            String ColorRed = "0.8962264 0.2578764 0.3087685 1";
            String Color = String.Empty;

            if (IsReadyIQFakeActive() && FakePlayerList != null)
            {
                foreach (FakePlayer playerInList in FakePlayerList)
                {
                    String Interface = InterfaceBuilder.GetInterface("UI_Chat_Mute_And_Ignore_Player");
                    if (Interface == null) return;

                    String DisplayName = playerInList.displayName;
                    if (!UInt64.TryParse(playerInList.userId, out UInt64 userIdAsUlong)) return;
                    
                    if (GeneralInfo.RenameList.ContainsKey(userIdAsUlong))
                        if (!String.IsNullOrWhiteSpace(GeneralInfo.RenameList[userIdAsUlong].RenameNick))
                            DisplayName = GeneralInfo.RenameList[userIdAsUlong].RenameNick;

                    Interface = Interface.Replace("%OFFSET_MIN%", $"{-385.795 - (-281.17 * X)} {97.54 - (46.185 * Y)}");
                    Interface = Interface.Replace("%OFFSET_MAX%", $"{-186.345 - (-281.17 * X)} {132.03 - (46.185 * Y)}");
                    Interface = Interface.Replace("%DISPLAY_NAME%", $"{DisplayName}");
                    Interface = Interface.Replace("%COMMAND_ACTION%", $"newui.cmd action.mute.ignore ignore.and.mute.controller {Action} confirm.alert {userIdAsUlong}");

                    switch (Action)
                    {
                        case SelectedAction.Mute:
                            if (playerInList.isMuted || (UserInformation.ContainsKey(userIdAsUlong) && UserInformation[userIdAsUlong] != null && (UserInformation[userIdAsUlong].MuteInfo.IsMute(MuteType.Chat) || UserInformation[userIdAsUlong].MuteInfo.IsMute(MuteType.Voice))))
                                Color = ColorRed;
                            else Color = ColorGreen;
                            break;
                        case SelectedAction.Ignore:
                            if (MyInfo.Settings.IsIgnored(userIdAsUlong))
                                Color = ColorRed;
                            else Color = ColorGreen;
                            break;
                        default:
                            break;
                    }

                    Interface = Interface.Replace("%COLOR%", Color);
		   		 		  						  	   		  		 			  		  		  			 		   			
		   		 		  						  	   		  		 			  		  		  			 		   			
                    X++;
                    if (X == 3)
                    {
                        X = 0;
                        Y++;
                    }

                    CuiHelper.AddUi(player, Interface);
                }
            }
            else
            {
                foreach (var playerInList in PlayerList)
                {
                    String Interface = InterfaceBuilder.GetInterface("UI_Chat_Mute_And_Ignore_Player");
                    if (Interface == null) return;
                    User Info = UserInformation[playerInList.userID];
                    if (Info == null) continue;

                    String DisplayName = playerInList.displayName;
                    if (GeneralInfo.RenameList.ContainsKey(playerInList.userID))
                        if (!String.IsNullOrWhiteSpace(GeneralInfo.RenameList[playerInList.userID].RenameNick))
                            DisplayName = GeneralInfo.RenameList[playerInList.userID].RenameNick;

                    Interface = Interface.Replace("%OFFSET_MIN%", $"{-385.795 - (-281.17 * X)} {97.54 - (46.185 * Y)}");
                    Interface = Interface.Replace("%OFFSET_MAX%", $"{-186.345 - (-281.17 * X)} {132.03 - (46.185 * Y)}");
                    Interface = Interface.Replace("%DISPLAY_NAME%", $"{DisplayName}");
                    Interface = Interface.Replace("%COMMAND_ACTION%", $"newui.cmd action.mute.ignore ignore.and.mute.controller {Action} confirm.alert {playerInList.userID}");

                    switch (Action)
                    {
                        case SelectedAction.Mute:
                            if (Info.MuteInfo.IsMute(MuteType.Chat) || Info.MuteInfo.IsMute(MuteType.Voice))
                                Color = ColorRed;
                            else Color = ColorGreen;
                            break;
                        case SelectedAction.Ignore:
                            if (MyInfo.Settings.IsIgnored(playerInList.userID))
                                Color = ColorRed;
                            else Color = ColorGreen;
                            break;
                        default:
                            break;
                    }

                    Interface = Interface.Replace("%COLOR%", Color);


                    X++;
                    if (X == 3)
                    {
                        X = 0;
                        Y++;
                    }

                    CuiHelper.AddUi(player, Interface);
                }
            }
        }
        private const String PermissionUseCmdMsg = "iqchat.cmsg";
        
        
        void API_SEND_PLAYER(BasePlayer player, String PlayerFormat, String Message, String Avatar, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            Configuration.ControllerMessage ControllerMessages = config.ControllerMessages;

            String OutMessage = Message; 
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (ControllerMessages.Formatting.FormatMessage)
                OutMessage = $"{Message.ToLower().Substring(0, 1).ToUpper()}{Message.Remove(0, 1).ToLower()}";

            if (ControllerMessages.Formatting.UseBadWords)
                foreach (String DetectedMessage in OutMessage.Split(' '))
                    if (ControllerMessages.Formatting.BadWords.ContainsKey(DetectedMessage.ToLower()))
                        OutMessage = OutMessage.Replace(DetectedMessage, ControllerMessages.Formatting.ReplaceBadWord);
            
            player.SendConsoleCommand("chat.add", channel, ulong.Parse(Avatar), $"{PlayerFormat}: {OutMessage}");
            player.ConsoleMessage($"{PlayerFormat}: {OutMessage}");
        }

        private void DrawUI_IQChat_Mute_And_Ignore_Player_Panel(BasePlayer player, SelectedAction Action, Int32 Page = 0, String SearchName = null)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Mute_And_Ignore_Panel_Content");
            if (Interface == null) return;

            CuiHelper.DestroyUi(player, "MuteIgnorePanelContent");
            CuiHelper.AddUi(player, Interface);
            
            if (IsReadyIQFakeActive())
            {
                List<FakePlayer> filteredPlayers = GetFilteredPlayers(SearchName);
		   		 		  						  	   		  		 			  		  		  			 		   			
                DrawUI_IQChat_Mute_And_Ignore_Pages(player, HasMorePages(filteredPlayers, Page), Action, Page);
                DrawUI_IQChat_Mute_And_Ignore_Player(player, Action, null, GetPageOfPlayers(filteredPlayers, Page));
            }
            else
            {
                IOrderedEnumerable<BasePlayer> playerList = GetPlayerList(SearchName, Action);

                DrawUI_IQChat_Mute_And_Ignore_Pages(player, HasMorePages(playerList, Page), Action, Page);
                DrawUI_IQChat_Mute_And_Ignore_Player(player, Action, playerList.Skip(18 * Page).Take(18));
            }
        }
        public Dictionary<BasePlayer, List<String>> LastMessagesChat = new Dictionary<BasePlayer, List<String>>();
        private String GetMessageInArgs(BasePlayer Sender, String[] arg)
        {
            if (arg == null || arg.Length == 0)
            {
                if (Sender != null)
                    ReplySystem(Sender, GetLang("FUNC_MESSAGE_NO_ARG_BROADCAST", Sender.UserIDString));
                else PrintWarning(GetLang("FUNC_MESSAGE_NO_ARG_BROADCAST"));
                return null;
            }
            String Message = String.Empty;
            foreach (String msg in arg)
                Message += " " + msg;

            return Message;
        }

        void OnUserGroupAdded(string id, string groupName)
        {
            String[] PermissionsGroup = permission.GetGroupPermissions(groupName);
            if (PermissionsGroup == null) return;
            foreach (String permName in PermissionsGroup)
                SetupParametres(id, permName);
        }
        
        private void SeparatorChat(Chat.ChatChannel channel, BasePlayer player, String Message)
        {
            Configuration.ControllerMessage.TurnedFuncional.AntiNoob.Settings antiNoob = config.ControllerMessages.TurnedFunc.AntiNoobSetting.AntiNoobChat;
            if (antiNoob.AntiNoobActivate)
                if (IsNoob(player.userID, antiNoob.TimeBlocked))
                {
                    ReplySystem(player, GetLang("IQCHAT_INFO_ANTI_NOOB", player.UserIDString, FormatTime(UserInformationConnection[player.userID].LeftTime(antiNoob.TimeBlocked), player.UserIDString)));
                    return;
                }

            Configuration.ControllerMessage ControllerMessage = config.ControllerMessages;
            User Info = UserInformation[player.userID];
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (ControllerMessage.TurnedFunc.AntiSpamSetting.AntiSpamActivate)
                if (!permission.UserHasPermission(player.UserIDString, PermissionAntiSpam))
                {
                    if (!Info.MuteInfo.IsMute(MuteType.Chat))
                    {
                        if (!Flooders.ContainsKey(player.userID))
                            Flooders.Add(player.userID, new FlooderInfo { Time = CurrentTime + ControllerMessage.TurnedFunc.AntiSpamSetting.FloodTime, LastMessage = Message });
                        else
                        {
                            if (Flooders[player.userID].Time > CurrentTime)
                            {
                                ReplySystem(player, GetLang("FLOODERS_MESSAGE", player.UserIDString, Convert.ToInt32(Flooders[player.userID].Time - CurrentTime)));
                                return;
                            }

                            if (ControllerMessage.TurnedFunc.AntiSpamSetting.AntiSpamDuplesSetting.AntiSpamDuplesActivate)
                            {
                                if (Flooders[player.userID].LastMessage == Message)
                                {
                                    if (Flooders[player.userID].TryFlood >= ControllerMessage.TurnedFunc.AntiSpamSetting.AntiSpamDuplesSetting.TryDuples)
                                    {
                                        MutePlayer(player, MuteType.Chat, 0, null, ControllerMessage.TurnedFunc.AntiSpamSetting.AntiSpamDuplesSetting.MuteSetting.Reason, ControllerMessage.TurnedFunc.AntiSpamSetting.AntiSpamDuplesSetting.MuteSetting.SecondMute);
                                        Flooders[player.userID].TryFlood = 0;
                                        return;
                                    }
                                    Flooders[player.userID].TryFlood++;
                                }
                            }
                        }
                        Flooders[player.userID].Time = ControllerMessage.TurnedFunc.AntiSpamSetting.FloodTime + CurrentTime;
                        Flooders[player.userID].LastMessage = Message;
                    }
                }

            GeneralInformation General = GeneralInfo;
            GeneralInformation.RenameInfo RenameInformation = General.GetInfoRename(player.userID);

            Configuration.ControllerParameters ControllerParameter = config.ControllerParameter;
            Configuration.ControllerMute ControllerMutes = config.ControllerMutes;
            Configuration.ControllerMessage.GeneralSettings.OtherSettings OtherController = config.ControllerMessages.GeneralSetting.OtherSetting;

            if (General.TurnMuteAllChat)
            {
                ReplySystem(player, GetLang("IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_CHAT", player.UserIDString));
                return;
            }

            if (channel == Chat.ChatChannel.Team && !ControllerMessage.TurnedFunc.MuteTeamChat) { }
            else if (Info.MuteInfo.IsMute(MuteType.Chat))
            {
                ReplySystem(player, GetLang("IQCHAT_FUNCED_NO_SEND_CHAT_MUTED", player.UserIDString, FormatTime(Info.MuteInfo.GetTime(MuteType.Chat), player.UserIDString)));
                return;
            }

            String Prefixes = String.Empty;
            String FormattingMessage = Message;
            String DisplayName = player.displayName;

            UInt64 UserID = player.userID;
            if (RenameInformation != null)
            {
                DisplayName = RenameInformation.RenameNick;
                UserID = RenameInformation.RenameID;
            }

            String ColorNickPlayer = !String.IsNullOrWhiteSpace(Info.Info.CustomColorNick) ? Info.Info.CustomColorNick : String.IsNullOrWhiteSpace(Info.Info.ColorNick) ? player.IsAdmin ? "#a8fc55" : "#54aafe" : Info.Info.ColorNick;
            
            Object resultColorNick = IsGradientColorValue(ColorNickPlayer);
            if (resultColorNick is List<String> gradientColors)
                DisplayName = ApplyGradientToText(DisplayName, gradientColors);
            else DisplayName = $"<color={ColorNickPlayer}>{DisplayName}</color>";
            
            String ChannelMessage = channel == Chat.ChatChannel.Cards ? "<color=#AA8234>[Cards]</color>" :  channel == Chat.ChatChannel.Clan ? "<color=#a5e664>[Clan]</color>" : "";

            if (ControllerMessage.Formatting.UseBadWords)
            {
                Tuple<String, Boolean> GetTuple = BadWordsCleaner(Message, ControllerMessage.Formatting.ReplaceBadWord, ControllerMessage.Formatting.BadWords);
                FormattingMessage = GetTuple.Item1;

                if (GetTuple.Item2 && channel == Chat.ChatChannel.Global)
                {
                    if (permission.UserHasPermission(player.UserIDString, PermissionMute))
                        Interface.Oxide.CallHook("OnModeratorSendBadWords", player, GetTuple.Item1);

                    Interface.Oxide.CallHook("OnPlayerSendBadWords", player, GetTuple.Item1);
		   		 		  						  	   		  		 			  		  		  			 		   			
                    if (ControllerMutes.AutoMuteSettings.UseAutoMute)
                        MutePlayer(player, MuteType.Chat, 0, null, ControllerMutes.AutoMuteSettings.AutoMuted.Reason, ControllerMutes.AutoMuteSettings.AutoMuted.SecondMute);
                }
            }

            Object OnMessageIQChat = Interface.CallHook("OnMessageIQChat", FormattingMessage);
            if (OnMessageIQChat is String)
                FormattingMessage = (String)OnMessageIQChat;
            
            if (ControllerMessage.Formatting.FormatMessage)
                FormattingMessage = $"{FormattingMessage.Substring(0, 1).ToUpper()}{FormattingMessage.Remove(0, 1).ToLower()}";
            
            if (ControllerParameter.Prefixes.TurnMultiPrefixes)
            {
                if (Info.Info.PrefixList != null)
                    Prefixes = String.Join("", Info.Info.PrefixList.Take(ControllerParameter.Prefixes.MaximumMultiPrefixCount));

            }
            else Prefixes = Info.Info.Prefix;

            Int32 SizeNick = OtherController.GetSizeNickOrMessage(player, true);
            Int32 SizeMessage = OtherController.GetSizeNickOrMessage(player, false);

            String FormatMessage = $"<size={SizeMessage}>" + "{0}</size>";
            
            // String FormatMessage = String.IsNullOrWhiteSpace(Info.Info.ColorMessage) ? $"<size={SizeMessage}>" + "{0}</size>" :
            //     $"<color={Info.Info.ColorMessage}><size={SizeMessage}>" + "{0}</size></color>";

            String ResultReference = GetReferenceTags(player); 
            String SendFormat = !String.IsNullOrWhiteSpace(Prefixes) ? 
                $"{ChannelMessage} {ResultReference}<size={OtherController.SizePrefix}>{Prefixes}</size> <size={SizeNick}>{DisplayName}</size>" :
                $"{ChannelMessage} {ResultReference} <size={SizeNick}>{DisplayName}</size>";
            
            if (config.RustPlusSettings.UseRustPlus)
                if (channel == Chat.ChatChannel.Team)
                {
                    RelationshipManager.PlayerTeam Team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if (Team == null) return;
                    Util.BroadcastTeamChat(player.Team, player.userID, player.displayName, FormattingMessage, Info.Info.ColorMessage);
                }

            if (ControllerMutes.LoggedMute.UseHistoryMessage && config.OtherSetting.LogsMuted.UseLogged)
                AddHistoryMessage(player, FormattingMessage);
            
            ReplyChat(channel, player, FormattingMessage, FormatMessage, SendFormat);
            AnwserMessage(player, FormattingMessage.ToLower());
            Puts($"{player.displayName}({player.UserIDString}): {FormattingMessage}");
            Log(LanguageEn ? $"CHAT MESSAGE : {player}: {ChannelMessage} {FormattingMessage}" : $"СООБЩЕНИЕ В ЧАТ : {player}: {ChannelMessage} {FormattingMessage}");
            DiscordLoggChat(player, channel, Message);
            DiscordCompactLoggChat(player, channel, Message);

            if (!config.disableRconBroadcast)
            {
                RCon.Broadcast(RCon.LogType.Chat, new Chat.ChatEntry
                {
                    Message = $"{player.displayName} : {FormattingMessage}",
                    UserId = player.UserIDString,
                    Username = player.displayName,
                    Channel = channel,
                    Time = (DateTime.UtcNow.Hour * 3600) + (DateTime.UtcNow.Minute * 60),
                });
            }
        }
        
        
        
        [ChatCommand("alert")]
        private void AlertChatCommand(BasePlayer Sender, String cmd, String[] args)
        {
            if (!permission.UserHasPermission(Sender.UserIDString, PermissionAlert)) return;
            Alert(Sender, args, false);
        }
        private String SkillTree_GetXP(BasePlayer player)
        {
            String[] infoSkills = GetInfoSkillTree(player);
            if(infoSkills == null) return String.Empty;
            String xpSkillTree = GetLang("SKILLTREE_SYNTAX_XP", player.UserIDString, infoSkills.Last());

            Object resultColorMessage = IsGradientColorValue(config.ReferenceSetting.skillTreeSettings.colorTag);
            if (resultColorMessage is List<String> gradientColors)
                xpSkillTree = ApplyGradientToText(xpSkillTree, gradientColors);
            else xpSkillTree = $"<color={config.ReferenceSetting.skillTreeSettings.colorTag}>{xpSkillTree}</color>";
            
            return xpSkillTree;
        }
        private void UserConnecteionData(BasePlayer player)
        {
            if (config.ControllerMessages.TurnedFunc.AntiNoobSetting.AntiNoobPM.AntiNoobActivate || config.ControllerMessages.TurnedFunc.AntiNoobSetting.AntiNoobChat.AntiNoobActivate)
            {
                if (!UserInformationConnection.ContainsKey(player.userID))
                    UserInformationConnection.Add(player.userID, new AntiNoob());
            }

            if (GeneralInfo.RenameList.TryGetValue(player.userID, out GeneralInformation.RenameInfo value))
            {
                String nickname = value.RenameNick;
                RenameUpdate(player, nickname);
            }
            
            Configuration.ControllerConnection ControllerConntect = config.ControllerConnect;
            Configuration.ControllerParameters ControllerParameter = config.ControllerParameter;
            if (ControllerConntect == null || ControllerParameter == null) return;

            if (!UserInformation.ContainsKey(player.userID))
            {
                User Info = new User();
                if (ControllerConntect.Turneds.TurnAutoSetupPrefix)
                {
                    if (ControllerParameter.Prefixes.TurnMultiPrefixes)
                        Info.Info.PrefixList.Add(ControllerConntect.SetupDefaults.PrefixDefault ?? String.Empty);
                    else Info.Info.Prefix = ControllerConntect.SetupDefaults.PrefixDefault ?? String.Empty;
                }
                
                Info.Info.ColorNick = ControllerConntect.SetupDefaults.NickDefault ?? String.Empty;
                Info.Info.ColorMessage = ControllerConntect.SetupDefaults.MessageDefault ?? String.Empty;
                
                // if (ControllerConntect.Turneds.TurnAutoSetupColorNick)
                //     Info.Info.ColorNick = ControllerConntect.SetupDefaults.NickDefault ?? "#ffffff";
                // else Info.Info.ColorNick = "#ff514";
                //
                // if (ControllerConntect.Turneds.TurnAutoSetupColorChat)
                //     Info.Info.ColorMessage = ControllerConntect.SetupDefaults.MessageDefault ?? "#ffffff";
                // else Info.Info.ColorMessage = "#ffffff";

                Info.Info.Rank = String.Empty;

                UserInformation.Add(player.userID, Info);
            }
            else
            {
                User User = UserInformation[player.userID];
                User.Info.ColorNick ??= ControllerConntect.SetupDefaults.NickDefault ?? String.Empty;
                User.Info.ColorMessage ??= ControllerConntect.SetupDefaults.MessageDefault ?? String.Empty;
            }
        }

        void OnUserGroupRemoved(string id, string groupName)
        {
            String[] PermissionsGroup = permission.GetGroupPermissions(groupName);
            if (PermissionsGroup == null) return;
		   		 		  						  	   		  		 			  		  		  			 		   			
            foreach (String permName in PermissionsGroup)
                RemoveParametres(id, permName);
        }
        
        void ReplyBroadcast(String CustomPrefix = null, String CustomAvatar = null, Boolean AdminAlert = false, Dictionary<String, List<String>> Messages = null, params object[] args)
        {
            foreach (BasePlayer p in !AdminAlert ? BasePlayer.activePlayerList.Where(p => UserInformation[p.userID].Settings.TurnBroadcast) : BasePlayer.activePlayerList)
            {
                sb.Clear();
                String templateText = GetMessages(p, Messages);
                if(String.IsNullOrWhiteSpace(templateText)) continue;
                ReplySystem(p, sb.AppendFormat(templateText, args).ToString(), CustomPrefix, CustomAvatar);
            }
        }
        private const String PermissionAntiSpam = "iqchat.antispamabuse";

        void OnPlayerDisconnected(BasePlayer player, string reason) => AlertDisconnected(player, reason);

                
                
        private String GetPrestigeLevel(UInt64 player)
        {
            if (!config.ReferenceSetting.skillTreeSettings.UsePrestigeSkillTree || !SkillTree) return String.Empty;
            Object prestige = SkillTree.Call("GetPrestigeLevel", player);
            if(prestige == null) return String.Empty;
            Int32 levelPrestige = (Int32)prestige;
            String prestigeResult = GetLang("SKILLTREE_SYNTAX_PRESTIGE_LEVEL", player.ToString(), levelPrestige);
            
            Object resultColorMessage = IsGradientColorValue(config.ReferenceSetting.skillTreeSettings.colorTag);
            if (resultColorMessage is List<String> gradientColors)
                prestigeResult = ApplyGradientToText(prestigeResult, gradientColors);
            else prestigeResult = $"<color={config.ReferenceSetting.skillTreeSettings.colorTag}>{prestigeResult}</color>";
            
            return prestigeResult;
        }

        
        
        private String PlayerRanks_GetRanks(BasePlayer player)
        {
            if (!PlayerRanks || !config.ReferenceSetting.playerRanksSettings.UsePlayerRanks) return String.Empty;
            String pRank = PlayerRanks.Call<String>("GetPlayerTitle", player.IPlayer);
            
            Object resultColorMessage = IsGradientColorValue(config.ReferenceSetting.playerRanksSettings.colorTag);
            if (resultColorMessage is List<String> gradientColors)
                pRank = ApplyGradientToText(pRank, gradientColors);
            else pRank = $"<color={config.ReferenceSetting.playerRanksSettings.colorTag}>{pRank}</color>";

            return pRank;
        }

        
        
        [ChatCommand("online")]
        private void ShowPlayerOnline(BasePlayer player)
        {
            if (!config.OtherSetting.UseCommandOnline) return;
            
            String Message = String.Empty;
            if (config.OtherSetting.UseCommandShortOnline)
            {
                Int32 shortCount = GetPlayersOnlineShort();
                Message = GetLang("IQCHAT_INFO_ONLINE", player.UserIDString, $"{shortCount}");
            }
            else
            {
                List<String> PlayerNames = GetPlayersOnline();
                Message = GetLang("IQCHAT_INFO_ONLINE", player.UserIDString, String.Join($"\n", PlayerNames));
            }

            ReplySystem(player, Message);
            player.ConsoleMessage(Message);
        }

        
                void OnGroupPermissionGranted(string name, string perm)
        {
            String[] PlayerGroups = permission.GetUsersInGroup(name);
            if (PlayerGroups == null) return;

            foreach (String playerInfo in PlayerGroups)
            {
                BasePlayer player = BasePlayer.FindByID(UInt64.Parse(playerInfo.Substring(0, 17)));
                if (player == null) return;

                SetupParametres(player.UserIDString, perm);
            }
        }
        private enum SelectedParametres
        {
            DropList,
            Slider
        }
        private const String PermissionRename = "iqchat.renameuse";
        String IQRankGetTimeGame(ulong userID) => (string)(IQRankSystem?.Call("API_GET_TIME_GAME", userID));
        public static String ApplyGradientToText(String text, List<String> colors)
        {
            String result = string.Empty;
            Int32 colorCount = colors.Count;
            Int32 textLength = text.Length;
            
            MatchCollection matches = regex.Matches(text);
            
            for (Int32 i = 0; i < textLength; i++)
            {
                Boolean isMatch = false;
                foreach (Match match in matches)
                {
                    if (match.Index == i)
                    {
                        result += match.Value;
                        i += match.Length - 1; 
                        isMatch = true;
                        break;
                    }
                }

                if (!isMatch)
                {
                    int gradientColorIndex = (int)((float)i / textLength * colorCount);
                    result += $"<color={colors[gradientColorIndex]}>{text[i]}</color>";
                }
            }

            return result;
        }
        class Response
        {
            [JsonProperty("country")]
            public string Country { get; set; }
        }
        private String JoinStringList(List<String> inputList) => String.Join(", ", inputList);
        
        
        
        private void DiscordLoggCommand(BasePlayer player, String Command, String[] Args)
        {
            Configuration.OtherSettings.General Commands = config.OtherSetting.LogsChatCommands;
            if (!Commands.UseLogged || String.IsNullOrWhiteSpace(Commands.Webhooks)) return;

            List<Fields> fields = new List<Fields>
                        {
                            new Fields(LanguageEn ? "Nick" : "Ник", player.displayName, true),
                            new Fields("Steam64ID", player.UserIDString, true),
                            new Fields(LanguageEn ? "Command" : "Команда", $"/{Command} ", true),
                        };

            String Arguments = String.Join(" ", Args);
            if (Args != null && Arguments != null && Arguments.Length != 0 && !String.IsNullOrWhiteSpace(Arguments))
                fields.Insert(fields.Count, new Fields(LanguageEn ? "Arguments" : "Аргументы", Arguments, false));

            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, 10710525, fields, new Authors("IQChat Command-History", null, "https://i.postimg.cc/SshGgy52/xiwsg5m.png", null), null) });

            Request($"{Commands.Webhooks}", newMessage.toJSON());
        }
        String API_GET_CHAT_COLOR(UInt64 ID)
        {
            if (!UserInformation.ContainsKey(ID)) return String.Empty;
		   		 		  						  	   		  		 			  		  		  			 		   			
            return UserInformation[ID].Info.ColorMessage;
        }

        [ChatCommand("hmute")]
        void HideMute(BasePlayer Moderator, string cmd, string[] arg)
        {
            if (!permission.UserHasPermission(Moderator.UserIDString, PermissionMute)) return;
            if (arg == null || arg.Length != 3 || arg.Length > 3)
            {
                ReplySystem(Moderator, LanguageEn ? "Invalid syntax, use : hmute Steam64ID/Nick Reason Time(seconds)" : "Неверный синтаксис,используйте : hmute Steam64ID/Ник Причина Время(секунды)");
                return;
            }
            string NameOrID = arg[0];
            string Reason = arg[1];
            Int32 TimeMute = 0;
            if (!Int32.TryParse(arg[2], out TimeMute))
            {
                ReplySystem(Moderator, LanguageEn ? "Enter the time in numbers!" : "Введите время цифрами!");
                return;
            }
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (Info.MuteInfo.IsMute(MuteType.Chat))
                        {
                            ReplySystem(Moderator, LanguageEn ? "The player already has a chat lock" : "Игрок уже имеет блокировку чата");
                            return;
                        }

                        Info.MuteInfo.SetMute(MuteType.Chat, TimeMute);
                        ReplySystem(Moderator, LanguageEn ? "Chat blocking issued to offline player" : "Блокировка чата выдана offline-игроку");
                        return;
                    }
                    else
                    {
                        ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }
		   		 		  						  	   		  		 			  		  		  			 		   			
            MutePlayer(target, MuteType.Chat, 0, Moderator, Reason, TimeMute, true, true);
        }
        
        
        
                [ChatCommand("rename")]
        private void ChatCommandRename(BasePlayer Renamer, string command, string[] args)
        {
            if (!permission.UserHasPermission(Renamer.UserIDString, PermissionRename)) return;
            GeneralInformation General = GeneralInfo;
            if (General == null) return;

            if (Renamer == null)
            {
                ReplySystem(Renamer, LanguageEn ? "You can only use this command while on the server" : "Вы можете использовать эту команду только находясь на сервере");
                return;
            }
            if (args.Length == 0 || args == null)
            {
                ReplySystem(Renamer, lang.GetMessage("COMMAND_RENAME_NOTARG", this, Renamer.UserIDString));
                return;
            }

            String Name = args[0];
            UInt64 ID = Renamer.userID;
            if (args.Length == 2 && args[1] != null && !String.IsNullOrWhiteSpace(args[1]))
                if (!UInt64.TryParse(args[1], out ID))
                {
                    ReplySystem(Renamer, GetLang("COMMAND_RENAME_NOT_ID", Renamer.UserIDString));
                    return;
                }

            if (General.RenameList.ContainsKey(Renamer.userID))
            {
                General.RenameList[Renamer.userID].RenameNick = Name;
                General.RenameList[Renamer.userID].RenameID = ID;
            }
            else General.RenameList.Add(Renamer.userID, new GeneralInformation.RenameInfo { RenameNick = Name, RenameID = ID });

            ReplySystem(Renamer, GetLang("COMMAND_RENAME_SUCCES", Renamer.UserIDString, Name, ID));
            RenameUpdate(Renamer, Name);
        }

        Boolean API_IS_IGNORED(BasePlayer.EncryptedValue<UInt64> UserHas, BasePlayer.EncryptedValue<UInt64> User) => API_IS_IGNORED(UserHas.Get(), User.Get());
        
        private Boolean IsValidHexColor(String color)
        {
            const String pattern = @"^#(?:[0-9a-fA-F]{3}){1,2}$";
            return Regex.IsMatch(color, pattern);
        }
        String API_GET_NICK_COLOR(ulong ID)
        {
            if (!UserInformation.ContainsKey(ID)) return String.Empty;

            return UserInformation[ID].Info.ColorNick;
        }
        public string GetLang(string LangKey, string userID = null, params object[] args)
        {
            sb.Clear();
            if (args != null)
            {
                sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
                return sb.ToString();
            }
            return lang.GetMessage(LangKey, this, userID);
        }
        
        private List<FakePlayer> GetFilteredPlayers(string searchName)
        {
            var combinedPlayerList = GetCombinedPlayerList();
            var filteredPlayers = combinedPlayerList;

            if (searchName != null)
                filteredPlayers = filteredPlayers.Where(p => p.displayName.ToLower().Contains(searchName.ToLower())).ToList();

            filteredPlayers = filteredPlayers.OrderByDescending(p => !IsFakeUser(p.userId) &&
                                                                     TryGetUserIdAsUlong(p.userId, out UInt64 userIdAsUlong) &&
                                                                     UserInformation.ContainsKey(userIdAsUlong) &&
                                                                     (UserInformation[userIdAsUlong].MuteInfo.IsMute(MuteType.Chat) || UserInformation[userIdAsUlong].MuteInfo.IsMute(MuteType.Voice))).ToList();

            return filteredPlayers;
        }
        Boolean API_CHECK_VOICE_CHAT(UInt64 ID)
        {
            if (!UserInformation.ContainsKey(ID)) return false;
            return UserInformation[ID].MuteInfo.IsMute(MuteType.Voice);
        }
        void Unload()
        {
            InterfaceBuilder.DestroyAll();
            
            if (_imageUI != null)
            {
                _imageUI.UnloadImages();
                _imageUI = null;
            }
            
            WriteData();
            _ = null;
        }
        
        [ChatCommand("rename.reset")]
        private void ChatCommandRenameReset(BasePlayer Renamer)
        {
            if (Renamer == null)
            {
                ReplySystem(Renamer, LanguageEn ? "You can only use this command while on the server" : "Вы можете использовать эту команду только находясь на сервере");
                return;
            }
            
            if (!permission.UserHasPermission(Renamer.UserIDString, PermissionRename)) return;
            GeneralInformation General = GeneralInfo;
            if (General == null) return;

            if (!General.RenameList.ContainsKey(Renamer.userID))
            {
                ReplySystem(Renamer, GetLang("COMMAND_RENAME_NOTHING", Renamer.UserIDString));
                return;
            }
            
            General.RenameList.Remove(Renamer.userID);
            RenameReset(Renamer);
        }

        private static IQChat _;
        private const String PermissionHideMuteName = "iqchat.hidemutename";

        private void RegisteredPermissions()
        {
            Configuration.ControllerParameters Controller = config.ControllerParameter;
            IEnumerable<Configuration.ControllerParameters.AdvancedFuncion> Parametres = Controller.Prefixes.Prefixes
                .Concat(Controller.NickColorList).Concat(Controller.MessageColorList);

            foreach (Configuration.ControllerParameters.AdvancedFuncion Permission in Parametres.Where(perm =>
                         !permission.PermissionExists(perm.Permissions, this)))
                permission.RegisterPermission(Permission.Permissions, this);
            
            foreach (KeyValuePair<String, Int32> sizeMessages in config.ControllerMessages.GeneralSetting.OtherSetting.sizeMessagePrivilages.Where(perm =>
                         !permission.PermissionExists(perm.Key, this)))
                permission.RegisterPermission(sizeMessages.Key, this);
            
            foreach (KeyValuePair<String, Int32> sizeNick in config.ControllerMessages.GeneralSetting.OtherSetting.sizeNickPrivilages.Where(perm =>
                         !permission.PermissionExists(perm.Key, this)))
                permission.RegisterPermission(sizeNick.Key, this);

            if (!permission.PermissionExists(PermissionUseCmdCnick, this))
                permission.RegisterPermission(PermissionUseCmdCnick, this);      
            if (!permission.PermissionExists(PermissionUseCmdMsg, this))
                permission.RegisterPermission(PermissionUseCmdMsg, this);  
            if (!permission.PermissionExists(PermissionTranslationIgnore, this))
                permission.RegisterPermission(PermissionTranslationIgnore, this);  
            if (!permission.PermissionExists(PermissionHideMuteName, this))
                permission.RegisterPermission(PermissionHideMuteName, this); 
            if (!permission.PermissionExists(PermissionHideOnline, this))
                permission.RegisterPermission(PermissionHideOnline, this);
            if (!permission.PermissionExists(PermissionRename, this))
                permission.RegisterPermission(PermissionRename, this);
            if (!permission.PermissionExists(PermissionMute, this))
                permission.RegisterPermission(PermissionMute, this);
            if (!permission.PermissionExists(PermissionAlert, this))
                permission.RegisterPermission(PermissionAlert, this);
            if (!permission.PermissionExists(PermissionAntiSpam, this))
                permission.RegisterPermission(PermissionAntiSpam, this);
            if (!permission.PermissionExists(PermissionHideConnection, this))
                permission.RegisterPermission(PermissionHideConnection, this);
            if (!permission.PermissionExists(PermissionHideDisconnection, this))
                permission.RegisterPermission(PermissionHideDisconnection, this);
            if (!permission.PermissionExists(PermissionMutedAdmin, this))
                permission.RegisterPermission(PermissionMutedAdmin, this);

            PrintWarning("Permissions - completed");
        }

        [ConsoleCommand("unmute")]
        void UnMuteCustomAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermissionMute)) return;

            if (arg?.Args == null || arg.Args.Length != 1 || arg.Args.Length > 1)
            {
                PrintWarning(LanguageEn ? "Invalid syntax, please use : unmute Steam64ID" : "Неверный синтаксис,используйте : unmute Steam64ID");
                return;
            }

            string NameOrID = arg.Args[0];
            BasePlayer target = GetPlayerNickOrID(NameOrID);

            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (!Info.MuteInfo.IsMute(MuteType.Chat))
                        {
                            ConsoleOrPrintMessage(arg.Player(),
                                LanguageEn ? "The player does not have a chat lock" : "У игрока нет блокировки чата");
                            return;
                        }

                        if (arg.Player()!= null)
                        {
                            if (!arg.Player().IsAdmin && arg.Player().userID == Steam64ID)
                            {
                                ConsoleOrPrintMessage(arg.Player(),
                                    LanguageEn
                                        ? "You can't unlock the chat by yourself"
                                        : "Вы не можете снять блокировку чата сами с себя");

                                return;
                            }
                        }

                        Info.MuteInfo.UnMute(MuteType.Chat);

                        ConsoleOrPrintMessage(arg.Player(),
                            LanguageEn ? "You have unblocked the offline chat to the player" : "Вы разблокировали чат offline игроку");
                        return;
                    }
                    else
                    {
                        ConsoleOrPrintMessage(arg.Player(),
                            LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    ConsoleOrPrintMessage(arg.Player(),
                        LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }

            UnmutePlayer(target, MuteType.Chat, arg.Player(), false, true);
            Puts(LanguageEn ? "Successfully" : "Успешно");
        }
        
        
        private void ReplyTranslationPM(BasePlayer Sender, BasePlayer TargetUser, String Message, String DisplayNameSender, String TargetDisplayName)
        {
            if (!String.IsNullOrWhiteSpace(config.ReferenceSetting.translationApiSettings.codeLanguagePrimary))
            {
                Action<String> callback = translation =>
                {
                    ReplySystem(TargetUser, GetLang("COMMAND_PM_SEND_MSG", TargetUser.UserIDString, DisplayNameSender, !permission.UserHasPermission(TargetUser.UserIDString, PermissionTranslationIgnore) ? translation : Message));
                    ReplySystem(Sender, GetLang("COMMAND_PM_SUCCESS", Sender.UserIDString, Message, TargetDisplayName));
                };

                TranslationAPI.Call("Translate", Message, config.ReferenceSetting.translationApiSettings.codeLanguagePrimary, "auto", callback);
            }
            else
            {
                String codeResult = lang.GetLanguage(TargetUser.UserIDString);
                saveTranslate.TryAdd(codeResult, new TranslationState());
		   		 		  						  	   		  		 			  		  		  			 		   			
                Action<String> callback = translation =>
                {
                    saveTranslate[codeResult].IsProcessed = true;
                    saveTranslate[codeResult].Translation = translation;
                    saveTranslate[codeResult].DoTranslation = Message;

                    ReplySystem(TargetUser, GetLang("COMMAND_PM_SEND_MSG", TargetUser.UserIDString, DisplayNameSender, !permission.UserHasPermission(TargetUser.UserIDString, PermissionTranslationIgnore) ? translation : Message));
                    ReplySystem(Sender, GetLang("COMMAND_PM_SUCCESS", Sender.UserIDString, Message, TargetDisplayName));
                };

                if (lang.GetLanguage(Sender.UserIDString) == codeResult)
                {
                    ReplySystem(TargetUser, GetLang("COMMAND_PM_SEND_MSG", TargetUser.UserIDString, DisplayNameSender, Message));
                    ReplySystem(Sender, GetLang("COMMAND_PM_SUCCESS", Sender.UserIDString, Message, TargetDisplayName));
                }
                else if (saveTranslate[codeResult].IsProcessed && !string.IsNullOrWhiteSpace(saveTranslate[codeResult].Translation) && Message == saveTranslate[codeResult].DoTranslation)
                {
                    ReplySystem(TargetUser, GetLang("COMMAND_PM_SEND_MSG", TargetUser.UserIDString, DisplayNameSender, saveTranslate[codeResult].Translation));
                    ReplySystem(Sender, GetLang("COMMAND_PM_SUCCESS", Sender.UserIDString, Message, TargetDisplayName));
                }
                else TranslationAPI.Call("Translate", Message, codeResult, "auto", callback);
            }
        }
        private void RenameReset(BasePlayer Renamer)
        {
            String apiKey = config.OtherSetting.renameSteamApiKey;
            if (String.IsNullOrWhiteSpace(apiKey))
            {
                ReplySystem(Renamer, LanguageEn ? "You do not have a Steam API Key specified in the configuration - it is impossible to get an up-to-date nickname" : "У вас не указан Steam API Key в конфигурации - невозможно получить актуальный ник");
                return;
            }
            webrequest.Enqueue($"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={apiKey}&steamids={Renamer.userID}", null, (code, response) =>
            {
			    if(code == 200 && response != null)
                {
                    try
                    {
                        JObject json = JObject.Parse(response);
                        JToken players = json["response"]["players"];
                        if (players is { HasValues: true })
                        {
                            String nickname = players[0]["personaname"].ToString();
                            RenameUpdate(Renamer, nickname);
                            ReplySystem(Renamer, GetLang("COMMAND_RENAME_RESET_SUCCES", Renamer.UserIDString));
                        }
                        else
                        {
                            Puts("No player data found for the provided Steam64ID.");
                        }
                    }
                    catch (Exception ex)
                    {
                        PrintWarning($"Failed to parse the Steam API response! Error: \"{ex.Message}\"");
                    }
                }
                else
                {
                    PrintWarning($"Steam API didn't respond (down?) Code: {code}");
                }
            }, this, RequestMethod.GET, null, 5f);
            
            ReplySystem(Renamer, GetLang("COMMAND_RENAME_PROCESS_STEAM", Renamer.UserIDString));
        }

        
                public void BroadcastAuto()
        {
            Configuration.ControllerAlert.Alert Broadcast = config.ControllerAlertSetting.AlertSetting;

            if (Broadcast.AlertMessage)
            {
                Int32 IndexBroadkastNow = 0;
                String RandomMsg = String.Empty;

                timer.Every(Broadcast.MessageListTimer, () =>
                {
                    if (Broadcast.AlertMessageType)
                    {
                        foreach (BasePlayer p in BasePlayer.activePlayerList)
                        {
                            List<String> MessageList = GetMesagesList(p, Broadcast.MessageList.LanguageMessages);
                            if (MessageList.Count == 0) continue;
                            
                            if (IndexBroadkastNow >= MessageList.Count)
                                IndexBroadkastNow = 0;
                            RandomMsg = MessageList[IndexBroadkastNow];
		   		 		  						  	   		  		 			  		  		  			 		   			
                            ReplySystem(p, RandomMsg);
                        }

                        IndexBroadkastNow++;
                    }
                    else
                    {
                        foreach (BasePlayer p in BasePlayer.activePlayerList)
                        {
                            String templateText = GetMessages(p, Broadcast.MessageList.LanguageMessages);
                            if(String.IsNullOrWhiteSpace(templateText)) continue;
                            ReplySystem(p, templateText);
                        }
                    }
                });

            }
        }
        private enum ElementsSettingsType
        {
            PM,
            Broadcast,
            Alert,
            Sound
        }
        
        [ChatCommand("cmsg")]
        private void ColoredMsgSetup(BasePlayer player, String cmd, String[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUseCmdMsg)) return;
            if (player == null)
                return;

            if (args.Length == 0)
            {
                ReplySystem(player, GetLang("COMMAND_COLOR_CUSTOM_NOT_ARG", player.UserIDString));
                return;
            }

            String color = String.Join("", args);
            List<String> correctedColors = Pool.GetList<String>();
		   		 		  						  	   		  		 			  		  		  			 		   			
            correctedColors = ConvertStringToList(color);
            if (HasInvalidHexColor(correctedColors, out String invalidHex))
            {
                ReplySystem(player, GetLang("COMMAND_COLOR_CUSTOM_NOT_HEX", player.UserIDString, invalidHex));
                return;
            }

            if (correctedColors.Count > 10)
                ReplySystem(player, GetLang("COMMAND_COLOR_CUSTOM_MAX_ARGUMENTS", player.UserIDString));
            
		   		 		  						  	   		  		 			  		  		  			 		   			
            User User = UserInformation[player.userID];
            String resultColor = JoinStringList(correctedColors);
            
            User.Info.CustomColorMessage = resultColor;
            
            Pool.FreeList(ref correctedColors);
            
            ReplySystem(player, GetLang("COMMAND_COLOR_CUSTOM_ACCESS", player.UserIDString));
        }
        List<String> IQRankListKey(BasePlayer.EncryptedValue<UInt64> userID) => IQRankListKey(userID.Get());
        String IQRankGetNameRankKey(string Key) => (string)(IQRankSystem?.Call("API_GET_RANK_NAME", Key));

        private void RemoveParametres(String ID, String Permissions)
        {
            UInt64 UserID = UInt64.Parse(ID);
            BasePlayer player = BasePlayer.FindByID(UserID);

            Configuration.ControllerConnection Controller = config.ControllerConnect;
            Configuration.ControllerParameters Parameters = config.ControllerParameter;

            if (!UserInformation.ContainsKey(UserID)) return;
            User Info = UserInformation[UserID];

            if (Controller.Turneds.TurnAutoDropPrefix)
            {
                if (Parameters.Prefixes.TurnMultiPrefixes)
                {
                    foreach (Configuration.ControllerParameters.AdvancedFuncion Prefixes in
                             Parameters.Prefixes.Prefixes.Where(prefix =>
                                 Info.Info.PrefixList.Contains(prefix.Argument) && prefix.Permissions == Permissions))
                    {
                        Info.Info.PrefixList.Remove(Prefixes.Argument);

                        if (player != null && config.ControllerMessages.GeneralSetting.alertArgumentsInfoRemove)
                            ReplySystem(player, GetLang("PREFIX_RETURNRED", player.UserIDString, Prefixes.Argument));

                        Log(LanguageEn
                            ? $"Player ({UserID}) expired prefix {Prefixes.Argument}"
                            : $"У игрока ({UserID}) истек префикс {Prefixes.Argument}");
                    }
                }
                else
                {
                    Configuration.ControllerParameters.AdvancedFuncion Prefixes = Parameters.Prefixes.Prefixes.FirstOrDefault(prefix => prefix.Argument == Info.Info.Prefix && prefix.Permissions == Permissions);
                    if (Prefixes != null)
                    {
                        Info.Info.Prefix = Controller.SetupDefaults.PrefixDefault;

                        if (player != null && config.ControllerMessages.GeneralSetting.alertArgumentsInfoRemove)
                            ReplySystem(player, GetLang("PREFIX_RETURNRED", player.UserIDString, Prefixes.Argument));

                        Log(LanguageEn
                            ? $"Player ({UserID}) expired prefix {Prefixes.Argument}"
                            : $"У игрока ({UserID}) истек префикс {Prefixes.Argument}");
                    }
                }
            }
            if (Controller.Turneds.TurnAutoDropColorNick)
            {
                Configuration.ControllerParameters.AdvancedFuncion ColorNick = Parameters.NickColorList.FirstOrDefault(nick => Info.Info.ColorNick == nick.Argument && nick.Permissions == Permissions);
                if (ColorNick != null)
                {
                    Info.Info.ColorNick = Controller.SetupDefaults.NickDefault;
                    
                    if (player != null && config.ControllerMessages.GeneralSetting.alertArgumentsInfoRemove)
                    {
                        String nickColorTitle = GetLang("COLOR_NICK_RETURNRED_TITLE", player.UserIDString);
                        Object resultColorMessage = IsGradientColorValue(Info.Info.ColorNick);
                        if (resultColorMessage is List<String> gradientColors)
                            nickColorTitle = ApplyGradientToText(nickColorTitle, gradientColors);
                        else nickColorTitle = $"<color={Info.Info.ColorNick}>{nickColorTitle}</color>";
                        
                        ReplySystem(player, GetLang("COLOR_NICK_RETURNRED", player.UserIDString, nickColorTitle));
                    }

                    Log(LanguageEn
                        ? $"Player ({UserID}) expired nick color {ColorNick.Argument}"
                        : $"У игрока ({UserID}) истек цвет ника {ColorNick.Argument}");
                }
            }
            if (Controller.Turneds.TurnAutoDropColorChat)
            {
                Configuration.ControllerParameters.AdvancedFuncion ColorChat = Parameters.MessageColorList.FirstOrDefault(message => Info.Info.ColorMessage == message.Argument && message.Permissions == Permissions);
                if (ColorChat == null) return;
                
                Info.Info.ColorMessage = Controller.SetupDefaults.MessageDefault;
                
                if (player != null && config.ControllerMessages.GeneralSetting.alertArgumentsInfoRemove)
                {
                    String chatColorTitle = GetLang("COLOR_CHAT_RETURNRED_TITLE", player.UserIDString);
                    Object resultColorMessage = IsGradientColorValue(Info.Info.ColorMessage);
                    if (resultColorMessage is List<String> gradientColors)
                        chatColorTitle = ApplyGradientToText(chatColorTitle, gradientColors);
                    else chatColorTitle = $"<color={Info.Info.ColorMessage}>{chatColorTitle}</color>";
                    
                    ReplySystem(player, GetLang("COLOR_CHAT_RETURNRED", player.UserIDString, chatColorTitle));
                }

                Log(LanguageEn ? $"Player ({UserID}) chat color expired {ColorChat.Argument}" : $"У игрока ({UserID}) истек цвет чата {ColorChat.Argument}");
            }
        }
        private const String PermissionAlert = "iqchat.alertuse";
        
        [ConsoleCommand("rename.reset")]
        private void ConsoleCommandRenameReset(ConsoleSystem.Arg args)
        {
            BasePlayer Renamer = args.Player();
            if (Renamer == null)
            {
                ReplySystem(Renamer, LanguageEn ? "You can only use this command while on the server" : "Вы можете использовать эту команду только находясь на сервере");
                return;
            }
            
            if (!permission.UserHasPermission(Renamer.UserIDString, PermissionRename)) return;
            GeneralInformation General = GeneralInfo;
            if (General == null) return;

            if (!General.RenameList.ContainsKey(Renamer.userID))
            {
                ReplySystem(Renamer, GetLang("COMMAND_RENAME_NOTHING", Renamer.UserIDString));
                return;
            }
            General.RenameList.Remove(Renamer.userID);
            RenameReset(Renamer);
        }

        String API_GET_CHAT_COLOR(BasePlayer.EncryptedValue<UInt64> ID) => API_GET_CHAT_COLOR(ID.Get());
        private List<String> ConvertStringToList(String input)
        {
            String pattern = @"[,\.\;\:\s]+";
        
            return Regex.Split(input, pattern) 
                .Select(item => item.Trim()) 
                .Where(item => !string.IsNullOrEmpty(item))
                .Take(10)
                .ToList();
        }

        private const String PermissionUseCmdCnick = "iqchat.cnick";
        private void DrawUI_IQChat_OpenDropListArgument(BasePlayer player, Int32 Count)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_OpenDropListArgument_Taked");
            if (Interface == null) return;

            Interface = Interface.Replace("%COUNT%", Count.ToString());

            CuiHelper.DestroyUi(player, $"TAKED_INFO_{Count}");
            CuiHelper.AddUi(player, Interface);
        }
        
        [ConsoleCommand("unmutevoice")]
        void UnMuteVoiceCustomAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermissionMute)) return;

            if (arg?.Args == null || arg.Args.Length != 1 || arg.Args.Length > 1)
            {
                PrintWarning(LanguageEn ? "Invalid syntax, please use : unmutevoice Steam64ID" : "Неверный синтаксис,используйте : unmutevoice Steam64ID");
                return;
            }

            string NameOrID = arg.Args[0];
            BasePlayer target = GetPlayerNickOrID(NameOrID);

            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (!Info.MuteInfo.IsMute(MuteType.Voice))
                        {
                            ConsoleOrPrintMessage(arg.Player(),
                                LanguageEn ? "The player does not have a chat lock" : "У игрока нет блокировки чата");
                            return;
                        }
                        
                        if (arg.Player()!= null)
                        {
                            if (!arg.Player().IsAdmin && arg.Player().userID == Steam64ID)
                            {
                                ConsoleOrPrintMessage(arg.Player(),
                                    LanguageEn
                                        ? "You can't unlock the chat by yourself"
                                        : "Вы не можете снять блокировку чата сами с себя");

                                return;
                            }
                        }
                        
                        Info.MuteInfo.UnMute(MuteType.Voice);

                        ConsoleOrPrintMessage(arg.Player(),
                            LanguageEn ? "You have unblocked the offline chat to the player" : "Вы разблокировали чат offline игроку");
                        return;
                    }
                    else
                    {
                        ConsoleOrPrintMessage(arg.Player(),
                            LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    ConsoleOrPrintMessage(arg.Player(),
                        LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }

            UnmutePlayer(target, MuteType.Voice, arg.Player(), false, true);
            Puts(LanguageEn ? "Successfully" : "Успешно");
        }
        private BasePlayer GetPlayerNickOrID(String Info)
        {
            String NameOrID = String.Empty;

            KeyValuePair<UInt64, GeneralInformation.RenameInfo> RenameInformation = GeneralInfo.RenameList.FirstOrDefault(x => x.Value.RenameNick.Contains(Info) || x.Value.RenameID.ToString() == Info);
            if (RenameInformation.Value == null)
                NameOrID = Info;
            else NameOrID = RenameInformation.Key.ToString();

            foreach (BasePlayer Finder in BasePlayer.activePlayerList)
            {
                if (Finder.displayName.ToLower().Contains(NameOrID.ToLower()) || Finder.userID.ToString() == NameOrID)
                    return Finder;
            }

            return null;
        }
        String IQRankGetTimeGame(BasePlayer.EncryptedValue<UInt64> userID) => IQRankGetTimeGame(userID.Get());
        private const String PermissionHideDisconnection = "iqchat.hidedisconnection";
        
        private Tuple<String, Boolean> BadWordsCleaner(String formattingMessage, String replaceBadWord, Dictionary<String, Boolean> badWords)
        {
            String resultMessage = formattingMessage;
            Boolean isBadWords = false;

            foreach (KeyValuePair<String, Boolean> word in badWords.Where(x => !x.Key.Contains("*")))
            {
                if (word.Value)
                {
                    String pattern = Regex.Escape(word.Key);
                    resultMessage = Regex.Replace(resultMessage, pattern, replaceBadWord, RegexOptions.IgnoreCase);
                    if (Regex.IsMatch(formattingMessage, pattern, RegexOptions.IgnoreCase))
                        isBadWords = true;
                }
                else
                {
                    String pattern = $@"\b{Regex.Escape(word.Key)}\b";
                    resultMessage = Regex.Replace(resultMessage, pattern, replaceBadWord);
                    if (Regex.IsMatch(formattingMessage, pattern))
                        isBadWords = true;
                }
            }

            return Tuple.Create(resultMessage, isBadWords);
        }

        void ReadData()
        {
            if (!Oxide.Core.Interface.Oxide.DataFileSystem.ExistsDatafile("IQSystem/IQChat/Users") && Oxide.Core.Interface.Oxide.DataFileSystem.ExistsDatafile("IQChat/Users"))
            {
                GeneralInfo = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<GeneralInformation>("IQChat/Information");
                UserInformation = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, User>>("IQChat/Users");

                Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQChat/Information", GeneralInfo);
                Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQChat/Users", UserInformation);
		   		 		  						  	   		  		 			  		  		  			 		   			
                PrintWarning(LanguageEn ? "Your player data has been moved to a new directory - IQSystem/IQChat , you can delete old data files!" : "Ваши данные игроков были перенесены в новую директорию - IQSystem/IQChat , вы можете удалить старые дата-файлы!");
            }

            GeneralInfo = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<GeneralInformation>("IQSystem/IQChat/Information");
            UserInformation = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, User>>("IQSystem/IQChat/Users");
            UserInformationConnection = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<UInt64, AntiNoob>>("IQSystem/IQChat/AntiNoob");
        }
        private List<String> GetPlayersOnline()
        {
            List<String> PlayerNames = new List<String>();
            Int32 Count = 1;

            if (IsReadyIQFakeActive())
            {
                List<FakePlayer> fakePlayerList = GetCombinedPlayerList();
                if (fakePlayerList != null)
                {
                    String prefix = API_GET_DEFAULT_PREFIX();
                    String colorNick = API_GET_DEFAULT_NICK_COLOR();
		   		 		  						  	   		  		 			  		  		  			 		   			
                    foreach (FakePlayer combinedPlayer in fakePlayerList)
                    {
                        String resultName = IsFakeUser(combinedPlayer.userId)
                            ? $"{Count} - {prefix}<color={colorNick}>{combinedPlayer.displayName}</color>"
                            : $"{Count} - {GetPlayerFormat(combinedPlayer.displayName, combinedPlayer.userId)}";
                        PlayerNames.Add(resultName);

                        Count++;
                    }

                    return PlayerNames;
                }
            }
            
            foreach (BasePlayer playerInList in BasePlayer.activePlayerList.Where(p => !permission.UserHasPermission(p.UserIDString, PermissionHideOnline)))
            {
                PlayerNames.Add($"{Count} - {GetPlayerFormat(playerInList.displayName, playerInList.UserIDString)}");
                Count++;
            }
            
            return PlayerNames;
        }

        void ReplyBroadcast(String Message, String CustomPrefix = null, String CustomAvatar = null, Boolean AdminAlert = false)
        {
            foreach (BasePlayer p in !AdminAlert ? BasePlayer.activePlayerList.Where(p => UserInformation[p.userID].Settings.TurnBroadcast) : BasePlayer.activePlayerList)
                ReplySystem(p, Message, CustomPrefix, CustomAvatar);
        }
        
        
        [ChatCommand("chat")]
        private void ChatCommandOpenedUI(BasePlayer player)
        {
            if (_interface == null)
            {
                PrintWarning(LanguageEn ? "We generate the interface, wait for a message about successful generation" : "Генерируем интерфейс, ожидайте сообщения об успешной генерации");
                return;
            }
            if (player == null) return;

            Configuration.ControllerParameters ControllerParameters = config.ControllerParameter;

            if (!LocalBase.ContainsKey(player))
                LocalBase.Add(player, new InformationOpenedUI { });

            LocalBase[player].ElementsPrefix = ControllerParameters.Prefixes.Prefixes.OrderByDescending(arg => arg.Argument.Length).Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions) && !p.IsBlockSelected).ToList();
            LocalBase[player].ElementsNick = ControllerParameters.NickColorList.Where(n => permission.UserHasPermission(player.UserIDString, n.Permissions) && !n.IsBlockSelected).ToList();
            LocalBase[player].ElementsChat = ControllerParameters.MessageColorList.Where(m => permission.UserHasPermission(player.UserIDString, m.Permissions) && !m.IsBlockSelected).ToList();

            if (IQRankSystem && config.ReferenceSetting.IQRankSystems.UseRankSystem)
            {
                List<Configuration.ControllerParameters.AdvancedFuncion> RankList = new List<Configuration.ControllerParameters.AdvancedFuncion>();
                foreach (String Rank in IQRankListKey(player.userID))
                    RankList.Add(new Configuration.ControllerParameters.AdvancedFuncion { Argument = Rank, Permissions = String.Empty });
		   		 		  						  	   		  		 			  		  		  			 		   			
                LocalBase[player].ElementsRanks = RankList;
            }

            DrawUI_IQChat_Context(player);
        }

        
                private void DrawUI_IQChat_DropList(BasePlayer player, String OffsetMin, String OffsetMax, String Title, TakeElementUser ElementType)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_DropList");
            if (Interface == null) return;

            Interface = Interface.Replace("%TITLE%", Title);
            Interface = Interface.Replace("%OFFSET_MIN%", OffsetMin);
            Interface = Interface.Replace("%OFFSET_MAX%", OffsetMax);
            Interface = Interface.Replace("%BUTTON_DROP_LIST_CMD%", $"newui.cmd droplist.controller open {ElementType}");

            CuiHelper.AddUi(player, Interface);
        }
        [ChatCommand("saybro")]
        private void AlertOnlyPlayerChatCommand(BasePlayer Sender, String cmd, String[] args)
        {
            if (!permission.UserHasPermission(Sender.UserIDString, PermissionAlert)) return;
            if (args == null || args.Length == 0)
            {
                ReplySystem(Sender, LanguageEn ? "You didn't specify a player!" : "Вы не указали игрока!");
                return;
            }
            BasePlayer Recipient = BasePlayer.Find(args[0]);
            if (Recipient == null)
            {
                ReplySystem(Sender, LanguageEn ? "The player is not on the server" : "Игрока нет на сервере!");
                return;
            }
            Alert(Sender, Recipient, args.Skip(1).ToArray());
        }
        
        
        object OnPlayerVoice(BasePlayer player, Byte[] data)
        {
            if (UserInformation[player.userID].MuteInfo.IsMute(MuteType.Voice))
                return false;
            return null;
        }
        public enum MuteType
        {
            Chat,
            Voice
        }
                
        
        private static Configuration config = new Configuration();
        void API_ALERT(String Message, Chat.ChatChannel channel = Chat.ChatChannel.Global, String CustomPrefix = null, String CustomAvatar = null, String CustomHex = null)
        {
            foreach (BasePlayer p in BasePlayer.activePlayerList)
                ReplySystem(p, Message, CustomPrefix, CustomAvatar, CustomHex);
        }
        protected override void SaveConfig() => Config.WriteObject(config);

        String API_GET_NICK_COLOR(BasePlayer.EncryptedValue<UInt64> ID) => API_GET_NICK_COLOR(ID.Get());
        private enum TakeElementUser
        {
            Prefix,
            Nick,
            Chat,
            Rank,
            MultiPrefix
        }
        private enum SelectedAction
        {
            Mute,
            Ignore
        }

        private void DrawUI_IQChat_OpenDropListArgument(BasePlayer player, TakeElementUser ElementType, Configuration.ControllerParameters.AdvancedFuncion Info, Int32 X, Int32 Y, Int32 Count)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_OpenDropListArgument");
            if (Interface == null) return;
		   		 		  						  	   		  		 			  		  		  			 		   			
            String DisplayName = player.displayName;
            if (ElementType == TakeElementUser.Nick)
            {
                String ColorNickPlayer = Info.Argument;
                Object resultColorNick = IsGradientColorValue(ColorNickPlayer);
                if (resultColorNick is List<String> gradientColors)
                    DisplayName = ApplyGradientToText(DisplayName, gradientColors);
                else DisplayName = $"<color={ColorNickPlayer}>{DisplayName}</color>";
            }

            String ChatMessage = GetLang("IQCHAT_CONTEXT_NICK_DISPLAY_MESSAGE", player.UserIDString);
            if (ElementType == TakeElementUser.Chat)
            {
                String ColorChat = Info.Argument;
                Object resultColorChat = IsGradientColorValue(ColorChat);
                if (resultColorChat is List<String> gradientChatColors)
                    ChatMessage = ApplyGradientToText(ChatMessage, gradientChatColors);
                else ChatMessage = $"<color={ColorChat}>{ChatMessage}</color>";
            }

            String Argument = ElementType == TakeElementUser.MultiPrefix || ElementType == TakeElementUser.Prefix ? Info.Argument :
                    ElementType == TakeElementUser.Nick ? DisplayName :
                    ElementType == TakeElementUser.Chat ? ChatMessage :
                    ElementType == TakeElementUser.Rank ? IQRankGetNameRankKey(Info.Argument) : String.Empty;

            Interface = Interface.Replace("%OFFSET_MIN%", $"{-140.329 - (-103 * X)} {-2.243 + (Y * -28)}");
            Interface = Interface.Replace("%OFFSET_MAX%", $"{-65.271 - (-103 * X)} {22.568 + (Y * -28)}");
            Interface = Interface.Replace("%COUNT%", Count.ToString());
            Interface = Interface.Replace("%ARGUMENT%", Argument);
            Interface = Interface.Replace("%TAKE_COMMAND_ARGUMENT%", $"newui.cmd droplist.controller element.take {ElementType} {Count} {Info.Permissions} {Info.Argument}");

            CuiHelper.DestroyUi(player, $"ArgumentDropList_{Count}");
            CuiHelper.AddUi(player, Interface);
        }
		   		 		  						  	   		  		 			  		  		  			 		   			
        
        
        [ConsoleCommand("mutefull")]
        void MuteCustomAdminFull(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermissionMute)) return;
            if (arg == null || arg.Args == null || arg.Args.Length != 3 || arg.Args.Length > 3)
            {
                PrintWarning(LanguageEn ? "Invalid syntax, use : mutefull Steam64ID/Nick Reason Time(seconds)" : "Неверный синтаксис,используйте : mutefull Steam64ID/Ник Причина Время(секунды)");
                return;
            }
            string NameOrID = arg.Args[0];
            string Reason = arg.Args[1];
            Int32 TimeMute = 0;
            if (!Int32.TryParse(arg.Args[2], out TimeMute))
            {
                PrintWarning(LanguageEn ? "Enter time in numbers!" : "Введите время цифрами!");
                return;
            }
            
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        
                        Info.MuteInfo.SetMute(MuteType.Chat, TimeMute);
                        Info.MuteInfo.SetMute(MuteType.Voice, TimeMute);
                        PrintWarning(LanguageEn ? "Chat and voice blocking issued to offline player" : "Блокировка чата и голоса выдана offline-игроку");
                        return;
                    }
                    else
                    {
                        PrintWarning(LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    PrintWarning(LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }

            MutePlayer(target, MuteType.Chat, 0, arg.Player(), Reason, TimeMute, true, true);
            MutePlayer(target, MuteType.Voice, 0, arg.Player(), Reason, TimeMute, true, true);
            Puts(LanguageEn ? "Successfully" : "Успешно");
        }
        
                private void DrawUI_IQChat_Update_Check_Box(BasePlayer player, ElementsSettingsType Type, String OffsetMin, String OffsetMax, Boolean StatusCheckBox)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Context_CheckBox");
            User Info = UserInformation[player.userID];
            if (Info == null || Interface == null) return;

            String Name = $"{Type}";
            Interface = Interface.Replace("%NAME_CHECK_BOX%", Name);
            Interface = Interface.Replace("%COLOR%", !StatusCheckBox ? "0.4716981 0.4716981 0.4716981 1" : "0.6040971 0.4198113 1 1");
            Interface = Interface.Replace("%OFFSET_MIN%", OffsetMin);
            Interface = Interface.Replace("%OFFSET_MAX%", OffsetMax);
            Interface = Interface.Replace("%COMMAND_TURNED%", $"newui.cmd checkbox.controller {Type}");
		   		 		  						  	   		  		 			  		  		  			 		   			
            CuiHelper.DestroyUi(player, Name);
            CuiHelper.AddUi(player, Interface);
        }
        private void DiscordLoggChat(BasePlayer player, Chat.ChatChannel Channel, String MessageLogged)
        {
            List<Fields> fields = new List<Fields>
                        {
                            new Fields(LanguageEn ? "Nick" : "Ник", player.displayName, true),
                            new Fields("Steam64ID", player.UserIDString, true),
                            new Fields(LanguageEn ? "Channel" : "Канал", Channel == Chat.ChatChannel.Global ? (LanguageEn ? "Global" : "Глобальный чат") : Channel == Chat.ChatChannel.Local ? (LanguageEn ? "Local" : "Локальный чат") : Channel == Chat.ChatChannel.Cards ? (LanguageEn ? "Poker" : "Покерный чат") : (LanguageEn ? "Team" : "Командный чат"), true),
                            new Fields(LanguageEn ? "Message" : "Сообщение", MessageLogged, false),
                        };

            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, 10710525, fields, new Authors("IQChat Chat-History", null, "https://i.postimg.cc/SshGgy52/xiwsg5m.png", null), null) });
		   		 		  						  	   		  		 			  		  		  			 		   			
            switch (Channel)
            {
                case Chat.ChatChannel.Cards:
                case Chat.ChatChannel.Global:
                case Chat.ChatChannel.Local:
                    {
                        Configuration.OtherSettings.General GlobalChat = config.OtherSetting.LogsChat.GlobalChatSettings;
                        if (!GlobalChat.UseLogged || String.IsNullOrWhiteSpace(GlobalChat.Webhooks)) return;
                        Request($"{GlobalChat.Webhooks}", newMessage.toJSON());
                        break;
                    }
                case Chat.ChatChannel.Team:
                    {
                        Configuration.OtherSettings.General TeamChat = config.OtherSetting.LogsChat.TeamChatSettings;
                        if (!TeamChat.UseLogged || String.IsNullOrWhiteSpace(TeamChat.Webhooks)) return;
                        Request($"{TeamChat.Webhooks}", newMessage.toJSON());
                    }
                    break;
                default:
                    break;
            }
        }

        private void UnmutePlayer(BasePlayer Target, MuteType Type, BasePlayer Moderator = null, Boolean HideUnmute = false, Boolean Command = false, String fakeUserId = "")
        {
            if (IsReadyIQFakeActive() && !String.IsNullOrWhiteSpace(fakeUserId))
            {
                if (Moderator != null)
                {
                    String fakeName = GetFakeName(fakeUserId);
                    ReplyBroadcast(null, null, false, Type == MuteType.Chat ? "FUNC_MESSAGE_UNMUTE_CHAT" : "FUNC_MESSAGE_UNMUTE_VOICE", Moderator.displayName, fakeName);
                }

                SetMuteFakeUser(fakeUserId, false);
                return;
            }
            
            if (!UserInformation.ContainsKey(Target.userID)) return;
            User Info = UserInformation[Target.userID];

            GeneralInformation.RenameInfo TargetRename = GeneralInfo.GetInfoRename(Target.userID);
            GeneralInformation.RenameInfo ModeratorRename = Moderator != null ? GeneralInfo.GetInfoRename(Moderator.userID) : null;
            if (!Info.MuteInfo.IsMute(Type))
            {
                if (Moderator != null)
                    ReplySystem(Moderator, LanguageEn ? "The player is not banned" : "У игрока нет блокировки");
                else Puts(LanguageEn ? "The player is not banned!" : "У игрока нет блокировки!");
                return;
            }

            String TargetName = TargetRename != null ? $"{TargetRename.RenameNick ?? Target.displayName}" : Target.displayName;
            String NameModerator = Moderator == null ? GetLang("IQCHAT_FUNCED_ALERT_TITLE_SERVER", Target.UserIDString) : permission.UserHasPermission(Moderator.UserIDString, PermissionHideMuteName) ? GetLang("IQCHAT_FUNCED_ALERT_TITLE_SERVER", Target.UserIDString) : ModeratorRename != null ? $"{ModeratorRename.RenameNick ?? Moderator.displayName}" : Moderator.displayName;
            String LangMessage = Type == MuteType.Chat ? "FUNC_MESSAGE_UNMUTE_CHAT" : "FUNC_MESSAGE_UNMUTE_VOICE";

            if (!HideUnmute)
            {
                if (Moderator != null)
                    if (!Moderator.IsAdmin && Moderator.userID == Target.userID)
                    {
                        ReplySystem(Moderator, GetLang("FUNC_MESSAGE_UNMUTE_YOURSELF", Moderator.UserIDString));
                        return;
                    }

                ReplyBroadcast(null, null, false, LangMessage, NameModerator, TargetName);
            }
            else
            {
                if (Target != null)
                    ReplySystem(Target, GetLang(LangMessage, Target.UserIDString, NameModerator, TargetName));
                if (Moderator != null)
                {
                    if (!Moderator.IsAdmin && Moderator.userID == Target.userID)
                    {
                        ReplySystem(Moderator, GetLang("FUNC_MESSAGE_UNMUTE_YOURSELF", Moderator.UserIDString));
                        return;
                    }

                    ReplySystem(Moderator, GetLang(LangMessage, Moderator.UserIDString, NameModerator, TargetName));
                }
            }

            Info.MuteInfo.UnMute(Type);
            
            if (Moderator != null && Moderator != Target)
                Interface.Oxide.CallHook("OnPlayerUnMuted", Target, Moderator);
		   		 		  						  	   		  		 			  		  		  			 		   			
            DiscordLoggMuted(Target, Type, Moderator: Moderator);
        }
        private String[] GetInfoSkillTree(BasePlayer player)
        {
            if (!SkillTree || !config.ReferenceSetting.skillTreeSettings.UseSkillTree) return null;
            return (String[])SkillTree.Call("ST_GetPlayerLevel", player);
        }

        [ChatCommand("r")]
        void RChat(BasePlayer Sender, string cmd, string[] arg)
        {
            Configuration.ControllerMessage ControllerMessages = config.ControllerMessages;
            if (!ControllerMessages.TurnedFunc.PMSetting.PMActivate) return;
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Sender, GetLang("COMMAND_R_NOTARG", Sender.UserIDString));
                return;
            }

            Configuration.ControllerMessage.TurnedFuncional.AntiNoob.Settings antiNoob = config.ControllerMessages.TurnedFunc.AntiNoobSetting.AntiNoobPM;
            if (antiNoob.AntiNoobActivate)
                if (IsNoob(Sender.userID, antiNoob.TimeBlocked))
                {
                    ReplySystem(Sender, GetLang("IQCHAT_INFO_ANTI_NOOB_PM", Sender.UserIDString, FormatTime(UserInformationConnection[Sender.userID].LeftTime(antiNoob.TimeBlocked), Sender.UserIDString)));
                    return;
                }

            if (!PMHistory.ContainsKey(Sender))
            {
                ReplySystem(Sender, GetLang("COMMAND_R_NOTMSG", Sender.UserIDString));
                return;
            }

            BasePlayer RetargetUser = PMHistory[Sender];
            if (RetargetUser == null)
            {
                ReplySystem(Sender, GetLang("COMMAND_PM_NOT_USER", Sender.UserIDString));
                return;
            }

            User InfoRetarget = UserInformation[RetargetUser.userID];
            User InfoSender = UserInformation[RetargetUser.userID];

            if(config.ControllerMutes.mutedPM)
                if (InfoSender.MuteInfo.IsMute(MuteType.Chat))
                {
                    ReplySystem(Sender, GetLang("IQCHAT_FUNCED_NO_SEND_CHAT_MUTED", Sender.UserIDString, FormatTime(InfoSender.MuteInfo.GetTime(MuteType.Chat), Sender.UserIDString)));
                    return;
                }

            if (!InfoRetarget.Settings.TurnPM)
            {
                ReplySystem(Sender, GetLang("FUNC_MESSAGE_PM_TURN_FALSE", Sender.UserIDString));
                return;
            }
            if (ControllerMessages.TurnedFunc.IgnoreUsePM)
            {
                if (InfoRetarget.Settings.IsIgnored(Sender.userID))
                {
                    ReplySystem(Sender, GetLang("IGNORE_NO_PM", Sender.UserIDString));
                    return;
                }
                if (InfoSender.Settings.IsIgnored(RetargetUser.userID))
                {
                    ReplySystem(Sender, GetLang("IGNORE_NO_PM_ME", Sender.UserIDString));
                    return;
                }
            }

            String Message = GetMessageInArgs(Sender, arg);
            if (Message == null || Message.Length <= 0)
            {
                ReplySystem(Sender, GetLang("COMMAND_PM_NOT_NULL_MSG", Sender.UserIDString));
                return;
            }
            if (Message.Length > 125) return;
            Message = Message.EscapeRichText();

            PMHistory[RetargetUser] = Sender;

            GeneralInformation.RenameInfo RenameSender = GeneralInfo.GetInfoRename(Sender.userID);
            GeneralInformation.RenameInfo RenamerTarget = GeneralInfo.GetInfoRename(RetargetUser.userID);
            String DisplayNameSender = RenameSender != null ? RenameSender.RenameNick ?? Sender.displayName : Sender.displayName;
            String TargetDisplayName = RenamerTarget != null ? RenamerTarget.RenameNick ?? RetargetUser.displayName : RetargetUser.displayName;

            if (TranslationAPI && config.ReferenceSetting.translationApiSettings.useTranslationApi && config.ReferenceSetting.translationApiSettings.translatePmChat)
                ReplyTranslationPM(Sender, RetargetUser, Message, DisplayNameSender, TargetDisplayName);
            else
            {
                ReplySystem(RetargetUser, GetLang("COMMAND_PM_SEND_MSG", RetargetUser.UserIDString, DisplayNameSender, Message));
                ReplySystem(Sender, GetLang("COMMAND_PM_SUCCESS", Sender.UserIDString, Message, TargetDisplayName));
            }
            
            if (InfoRetarget.Settings.TurnSound)
                Effect.server.Run(ControllerMessages.TurnedFunc.PMSetting.SoundPM, RetargetUser.GetNetworkPosition());

            RustApp?.Call("RA_DirectMessageHandler", Sender.UserIDString, RetargetUser.UserIDString, Message);
            
            Log(LanguageEn ? $"PRIVATE MESSAGES : {DisplayNameSender}({Sender.userID}) sent a message to the player - {TargetDisplayName}({RetargetUser.userID})\nMESSAGE : {Message}" : $"ЛИЧНЫЕ СООБЩЕНИЯ : {DisplayNameSender}({Sender.userID}) отправил сообщение игроку - {TargetDisplayName}({RetargetUser.userID})\nСООБЩЕНИЕ : {Message}");
            DiscordLoggPM(Sender, RetargetUser, Message);

            RCon.Broadcast(RCon.LogType.Chat, new Chat.ChatEntry
            {
                Message = LanguageEn ? $"PRIVATE MESSAGES : {DisplayNameSender}({Sender.userID}) -> {TargetDisplayName}({RetargetUser.userID}) : MESSAGE : {Message}" : $"ЛИЧНЫЕ СООБЩЕНИЯ : {DisplayNameSender}({Sender.userID}) -> {TargetDisplayName}({RetargetUser.userID}) : СООБЩЕНИЕ : {Message}",
                UserId = Sender.UserIDString,
                Username = Sender.displayName,
                Channel = Chat.ChatChannel.Global,
                Time = (DateTime.UtcNow.Hour * 3600) + (DateTime.UtcNow.Minute * 60),
                Color = "#3f4bb8",
            });
            PrintWarning(LanguageEn ? $"PRIVATE MESSAGES : {DisplayNameSender}({Sender.userID}) -> {TargetDisplayName}({RetargetUser.userID}) : MESSAGE : {Message}" : $"ЛИЧНЫЕ СООБЩЕНИЯ : {DisplayNameSender}({Sender.userID}) -> {TargetDisplayName}({RetargetUser.userID}) : СООБЩЕНИЕ : {Message}");
        }
        
        
        
        
        public List<String> GetMesagesList(BasePlayer player, Dictionary<String, List<String>> LanguageMessages)
        {
            String LangPlayer = _.lang.GetLanguage(player.UserIDString);

            if (LanguageMessages.ContainsKey(LangPlayer))
                return LanguageMessages[LangPlayer];
            else if (LanguageMessages.ContainsKey("en"))
                return LanguageMessages["en"];
            else return LanguageMessages.FirstOrDefault().Value;
        }
        
        
                [ConsoleCommand("newui.cmd")]
        private void ConsoleCommandFuncional(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            String Action = arg.Args[0];
            if (Action == null || String.IsNullOrWhiteSpace(Action)) return;

            if (!LocalBase.ContainsKey(player))
            {
                PrintError(LanguageEn ? "UI was unable to process the local base (Local Base) contact the developer" : "UI не смог обработать локальную базу (LocalBase) свяжитесь с разработчиком");
                return;
            }
            Configuration.ControllerParameters ControllerParameters = config.ControllerParameter;
            if (ControllerParameters == null)
            {
                PrintError(LanguageEn ? "An error has been made in the configuration! Controller Parameters is null, contact developer" : "В конфигурации допущена ошибка! ControllerParameters является null, свяжитесь с разработчиком");
                return;
            }

            switch (Action)
            {
                case "action.mute.ignore":
                    {
                        String ActionMenu = arg.Args[1];
                        SelectedAction ActionType = (SelectedAction)Enum.Parse(typeof(SelectedAction), arg.Args[2]);
                        if (ActionMenu == "search.controller" && arg.Args.Length < 4)
                            return;

                        switch (ActionMenu)
                        {
                            case "mute.controller":
                                {
                                    if (!player.IsAdmin)
                                        if (!permission.UserHasPermission(player.UserIDString, PermissionMute)) return;

                                    String ActionMute = arg.Args[3];
                                    switch (ActionMute)
                                    {
                                        case "mute.all.chat":
                                        {
                                            if (!player.IsAdmin) return;

                                            if (GeneralInfo.TurnMuteAllChat)
                                            {
                                                GeneralInfo.TurnMuteAllChat = false;
                                                //  ReplyBroadcast(GetLang("IQCHAT_FUNCED_NO_SEND_CHAT_UNMUTED_ALL_CHAT", player.UserIDString), AdminAlert: true);
                                                ReplyBroadcast(null, null, true,
                                                    "IQCHAT_FUNCED_NO_SEND_CHAT_UNMUTED_ALL_CHAT");
                                            }
                                            else
                                            {
                                                GeneralInfo.TurnMuteAllChat = true;
                                                // ReplyBroadcast(GetLang("IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_CHAT", player.UserIDString), AdminAlert: true);
                                                ReplyBroadcast(null, null, true,
                                                    "IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_CHAT");
                                            }

                                            DrawUI_IQChat_Update_MuteChat_All(player);
                                            break;
                                        }
                                        case "mute.all.voice":
                                        {
                                            if (!player.IsAdmin) return;
                                            if (GeneralInfo.TurnMuteAllVoice)
                                            {
                                                GeneralInfo.TurnMuteAllVoice = false;
                                                //   ReplyBroadcast(GetLang("IQCHAT_FUNCED_NO_SEND_CHAT_UMMUTED_ALL_VOICE", player.UserIDString), AdminAlert: true);
                                                ReplyBroadcast(null, null, true,
                                                    "IQCHAT_FUNCED_NO_SEND_CHAT_UMMUTED_ALL_VOICE");
                                            }
                                            else
                                            {
                                                GeneralInfo.TurnMuteAllVoice = true;
                                                // ReplyBroadcast(GetLang("IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_VOICE", player.UserIDString), AdminAlert: true);
                                                ReplyBroadcast(null, null, true,
                                                    "IQCHAT_FUNCED_NO_SEND_CHAT_MUTED_ALL_VOICE");

                                            }

                                            DrawUI_IQChat_Update_MuteVoice_All(player);
                                            break;
                                        }
                                        default:
                                            break;
                                    }
                                    break;
                                }
                            case "ignore.and.mute.controller":
                                {
                                    String ActionController = arg.Args[3];
                                    BasePlayer TargetPlayer = BasePlayer.Find(arg.Args[4]);
                                    UInt64.TryParse(arg.Args[4], out UInt64 ID);
                                    Boolean isFakeUser = IsFakeUser(arg.Args[4]);
                                    String userIdString = !isFakeUser ? String.Empty : arg.Args[4];

                                    if (TargetPlayer == null && !isFakeUser) 
                                    {
                                        CuiHelper.DestroyUi(player, "MUTE_AND_IGNORE_PANEL_ALERT");
                                        return;
                                    }

                                    switch (ActionController)
                                    {
                                        case "confirm.alert":
                                            {
                                                if (ActionType == SelectedAction.Ignore)
                                                    DrawUI_IQChat_Ignore_Alert(player, TargetPlayer, ID);
                                                else DrawUI_IQChat_Mute_Alert(player, TargetPlayer, ID);
                                                break;
                                            }
                                        case "open.reason.mute":
                                            {
                                                MuteType Type = (MuteType)Enum.Parse(typeof(MuteType), arg.Args[5]);
                                                DrawUI_IQChat_Mute_Alert_Reasons(player, TargetPlayer, Type, IDFake: ID);
                                                break;
                                            }
                                        case "confirm.yes":
                                            {
                                                if (ActionType == SelectedAction.Ignore)
                                                {
                                                    User Info = UserInformation[player.userID];
                                                    Info.Settings.IgnoredAddOrRemove(isFakeUser ? ID : TargetPlayer.userID);

                                                    CuiHelper.DestroyUi(player, "MUTE_AND_IGNORE_PANEL_ALERT");
                                                    DrawUI_IQChat_Mute_And_Ignore_Player_Panel(player, ActionType);
                                                }
                                                else
                                                {
                                                    MuteType Type = (MuteType)Enum.Parse(typeof(MuteType), arg.Args[5]);
                                                    Int32 IndexReason = Int32.Parse(arg.Args[6]);
		   		 		  						  	   		  		 			  		  		  			 		   			
                                                    MutePlayer(TargetPlayer, Type, IndexReason, player, fakeUserId: userIdString);

                                                    CuiHelper.DestroyUi(player, "MUTE_AND_IGNORE_PANEL_ALERT");
                                                    DrawUI_IQChat_Mute_And_Ignore_Player_Panel(player, ActionType);
                                                }
                                                break;
                                            }
                                        case "unmute.yes":
                                            {
                                                MuteType Type = (MuteType)Enum.Parse(typeof(MuteType), arg.Args[5]);

                                                UnmutePlayer(TargetPlayer, Type, player, fakeUserId: userIdString);

                                                CuiHelper.DestroyUi(player, "MUTE_AND_IGNORE_PANEL_ALERT");
                                                DrawUI_IQChat_Mute_And_Ignore_Player_Panel(player, ActionType);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case "open":
                                {
                                    DrawUI_IQChat_Mute_And_Ignore(player, ActionType);
                                    break;
                                }
                            case "page.controller":
                                {
                                    Int32 Page = Int32.Parse(arg.Args[3]);

                                    DrawUI_IQChat_Mute_And_Ignore_Player_Panel(player, ActionType, Page);
                                    break;
                                }
                            case "search.controller":
                                {
                                    String SearchName = arg.Args[3];
                                    DrawUI_IQChat_Mute_And_Ignore_Player_Panel(player, ActionType, SearchName: SearchName);
                                    break;
                                }
                            default:
                                break;
                        }

                        break;
                    }
                case "checkbox.controller":
                    {
                        ElementsSettingsType Type = (ElementsSettingsType)Enum.Parse(typeof(ElementsSettingsType), arg.Args[1]);
                        if (!UserInformation.ContainsKey(player.userID)) return;
                        User Info = UserInformation[player.userID];
                        if (Info == null) return;

                        switch (Type)
                        {
                            case ElementsSettingsType.PM:
                                {
                                    if (Info.Settings.TurnPM)
                                        Info.Settings.TurnPM = false;
                                    else Info.Settings.TurnPM = true;

                                    DrawUI_IQChat_Update_Check_Box(player, Type, "143.38 -67.9", "151.38 -59.9", Info.Settings.TurnPM);
                                    break;
                                }
                            case ElementsSettingsType.Broadcast:
                                {
                                    if (Info.Settings.TurnBroadcast)
                                        Info.Settings.TurnBroadcast = false;
                                    else Info.Settings.TurnBroadcast = true;

                                    DrawUI_IQChat_Update_Check_Box(player, Type, "143.38 -79.6", "151.38 -71.6", Info.Settings.TurnBroadcast);
                                    break;
                                }
                            case ElementsSettingsType.Alert:
                                {
                                    if (Info.Settings.TurnAlert)
                                        Info.Settings.TurnAlert = false;
                                    else Info.Settings.TurnAlert = true;

                                    DrawUI_IQChat_Update_Check_Box(player, Type, "143.38 -91.6", "151.38 -83.6", Info.Settings.TurnAlert);
                                    break;
                                }
                            case ElementsSettingsType.Sound:
                                {
                                    if (Info.Settings.TurnSound)
                                        Info.Settings.TurnSound = false;
                                    else Info.Settings.TurnSound = true;

                                    DrawUI_IQChat_Update_Check_Box(player, Type, "143.38 -103.6", "151.38 -95.6", Info.Settings.TurnSound);
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    }
                case "droplist.controller":
                    {
                        String ActionDropList = arg.Args[1];
                        TakeElementUser Element = (TakeElementUser)Enum.Parse(typeof(TakeElementUser), arg.Args[2]);

                        switch (ActionDropList)
                        {
                            case "open":
                                {
                                    DrawUI_IQChat_OpenDropList(player, Element);
                                    break;
                                }
                            case "page.controller":
                                {
                                    String ActionDropListPage = arg.Args[3];
                                    Int32 Page = (Int32)Int32.Parse(arg.Args[4]);
                                    Page = ActionDropListPage == "+" ? Page + 1 : Page - 1;

                                    DrawUI_IQChat_OpenDropList(player, Element, Page);
                                    break;
                                }
                            case "element.take":
                                {
                                    Int32 Count = Int32.Parse(arg.Args[3]);
                                    String Permissions = arg.Args[4];
                                    
                                    String Argument = String.Join(" ", arg.Args.Skip(5));
                                    if (!permission.UserHasPermission(player.UserIDString, Permissions)) return;
                                    if (!UserInformation.ContainsKey(player.userID)) return;
                                    User User = UserInformation[player.userID];
                                    if (User == null) return;
		   		 		  						  	   		  		 			  		  		  			 		   			
                                    switch (Element)
                                    {
                                        case TakeElementUser.MultiPrefix:
                                            {
                                                if (!User.Info.PrefixList.Contains(Argument))
                                                {
                                                    User.Info.PrefixList.Add(Argument);
                                                    DrawUI_IQChat_OpenDropListArgument(player, Count);
                                                }
                                                else
                                                {
                                                    User.Info.PrefixList.Remove(Argument);
                                                    CuiHelper.DestroyUi(player, $"TAKED_INFO_{Count}");
                                                }
                                                break;
                                            }
                                        case TakeElementUser.Prefix:
                                            User.Info.Prefix = User.Info.Prefix.Equals(Argument) ? String.Empty : Argument;
                                            break;
                                        case TakeElementUser.Nick:
                                            User.Info.ColorNick = Argument;
                                            User.Info.CustomColorNick = String.Empty;
                                            break;
                                        case TakeElementUser.Chat:
                                            User.Info.ColorMessage = Argument;
                                            User.Info.CustomColorMessage = String.Empty;
                                            break;
                                        case TakeElementUser.Rank:
                                            {
                                                User.Info.Rank = Argument;
                                                IQRankSetRank(player.userID, Argument);
                                            }
                                            break;
                                        default:
                                            break;
                                    }
                                    DrawUI_IQChat_Update_DisplayName(player);
                                    break;
                                }
                        }
                        break;
                    }
                case "slider.controller": // newui.cmd slider.controller 0 +
                    {
                        TakeElementUser Element = (TakeElementUser)Enum.Parse(typeof(TakeElementUser), arg.Args[1]);
                        List<Configuration.ControllerParameters.AdvancedFuncion> SliderElements = new List<Configuration.ControllerParameters.AdvancedFuncion>();
                        User Info = UserInformation[player.userID];
                        if (Info == null) return;
		   		 		  						  	   		  		 			  		  		  			 		   			
                        InformationOpenedUI InfoUI = LocalBase[player];
                        if (InfoUI == null) return;


                        String ActionSlide = arg.Args[2];

                        switch (Element)
                        {
                            case TakeElementUser.Prefix:
                                {
                                    SliderElements = LocalBase[player].ElementsPrefix;

                                    if (SliderElements == null || SliderElements.Count == 0) return;
		   		 		  						  	   		  		 			  		  		  			 		   			
                                    if (ActionSlide == "+")
                                    {
                                        InfoUI.SlideIndexPrefix++;

                                        if (InfoUI.SlideIndexPrefix >= SliderElements.Count)
                                            InfoUI.SlideIndexPrefix = 0;
                                    }
                                    else
                                    {
                                        InfoUI.SlideIndexPrefix--;

                                        if (InfoUI.SlideIndexPrefix < 0)
                                            InfoUI.SlideIndexPrefix = SliderElements.Count - 1;
                                    }

                                    Info.Info.Prefix = SliderElements[InfoUI.SlideIndexPrefix].Argument;
                                }
                                break;
                            case TakeElementUser.Nick:
                                {
                                    SliderElements = LocalBase[player].ElementsNick;

                                    if (SliderElements == null || SliderElements.Count == 0) return;

                                    if (ActionSlide == "+")
                                    {
                                        InfoUI.SlideIndexNick++;

                                        if (InfoUI.SlideIndexNick >= SliderElements.Count)
                                            InfoUI.SlideIndexNick = 0;
                                    }
                                    else
                                    {
                                        InfoUI.SlideIndexNick--;

                                        if (InfoUI.SlideIndexNick < 0)
                                            InfoUI.SlideIndexNick = SliderElements.Count - 1;
                                    }
                                    Info.Info.ColorNick = SliderElements[InfoUI.SlideIndexNick].Argument;
                                    Info.Info.CustomColorNick = String.Empty;
                                }
                                break;
                            case TakeElementUser.Chat:
                                {
                                    SliderElements = LocalBase[player].ElementsChat;
                                    if (SliderElements == null || SliderElements.Count == 0) return;

                                    if (ActionSlide == "+")
                                    {
                                        InfoUI.SlideIndexChat++;

                                        if (InfoUI.SlideIndexChat >= SliderElements.Count)
                                            InfoUI.SlideIndexChat = 0;
                                    }
                                    else
                                    {
                                        InfoUI.SlideIndexChat--;

                                        if (InfoUI.SlideIndexChat < 0)
                                            InfoUI.SlideIndexChat = SliderElements.Count - 1;
                                    }
                                    Info.Info.ColorMessage = SliderElements[InfoUI.SlideIndexChat].Argument;
                                    Info.Info.CustomColorMessage = String.Empty;
                                }
                                break;
                            case TakeElementUser.Rank:
                                {
                                    SliderElements = LocalBase[player].ElementsRanks;
                                    if (SliderElements == null || SliderElements.Count == 0) return;

                                    if (ActionSlide == "+")
                                    {
                                        InfoUI.SlideIndexRank++;

                                        if (InfoUI.SlideIndexRank >= SliderElements.Count)
                                            InfoUI.SlideIndexRank = 0;
                                    }
                                    else
                                    {
                                        InfoUI.SlideIndexRank--;

                                        if (InfoUI.SlideIndexRank < 0)
                                            InfoUI.SlideIndexRank = SliderElements.Count - 1;
                                    }
                                    Info.Info.Rank = SliderElements[InfoUI.SlideIndexRank].Argument;
                                    IQRankSetRank(player.userID, SliderElements[InfoUI.SlideIndexRank].Argument);
                                }
                                break;
                            default:
                                break;
                        }
                        DrawUI_IQChat_Slider_Update_Argument(player, Element);
                        DrawUI_IQChat_Update_DisplayName(player);
                        break;
                    }
                default:
                    break;
            }
        }
        void Alert(BasePlayer Sender, BasePlayer Recipient, string[] arg)
        {
            String Message = GetMessageInArgs(Sender, arg);
            if (Message == null) return;

            ReplySystem(Recipient, Message);
        }
        void ReplySystem(BasePlayer player, String Message, String CustomPrefix = null, String CustomAvatar = null, String CustomHex = null)
        {
            Configuration.ControllerMessage ControllerMessages = config.ControllerMessages;

            String Prefix = (CustomPrefix == null || String.IsNullOrWhiteSpace(CustomPrefix)) ? (ControllerMessages.GeneralSetting.BroadcastFormat.BroadcastTitle == null || String.IsNullOrWhiteSpace(ControllerMessages.GeneralSetting.BroadcastFormat.BroadcastTitle)) ? "" : ControllerMessages.GeneralSetting.BroadcastFormat.BroadcastTitle : CustomPrefix;
            String AvatarID = (CustomAvatar == null || String.IsNullOrWhiteSpace(CustomAvatar)) ? (ControllerMessages.GeneralSetting.BroadcastFormat.Steam64IDAvatar == null || String.IsNullOrWhiteSpace(ControllerMessages.GeneralSetting.BroadcastFormat.Steam64IDAvatar)) ? "0" : ControllerMessages.GeneralSetting.BroadcastFormat.Steam64IDAvatar : CustomAvatar;
            String Hex = (CustomHex == null || String.IsNullOrWhiteSpace(CustomHex)) ? (ControllerMessages.GeneralSetting.BroadcastFormat.BroadcastColor == null || String.IsNullOrWhiteSpace(ControllerMessages.GeneralSetting.BroadcastFormat.BroadcastColor)) ? "#ffff" : ControllerMessages.GeneralSetting.BroadcastFormat.BroadcastColor : CustomHex;
           
            player.SendConsoleCommand("chat.add", Chat.ChatChannel.Global, AvatarID, $"{Prefix}<color={Hex}>{Message}</color>");
        }

        
                void AlertUI(BasePlayer Sender, string[] arg)
        {
            if (_interface == null)
            {
                PrintWarning(LanguageEn ? "We generate the interface, wait for a message about successful generation" : "Генерируем интерфейс, ожидайте сообщения об успешной генерации");
                return;
            }
            String Message = GetMessageInArgs(Sender, arg);
            if (Message == null) return;

            foreach (BasePlayer PlayerInList in BasePlayer.activePlayerList)
                DrawUI_IQChat_Alert(PlayerInList, Message);
        }
        void Alert(BasePlayer Sender, string[] arg, Boolean IsAdmin)
        {
            String Message = GetMessageInArgs(Sender, arg);
            if (Message == null) return;

            ReplyBroadcast(Message, AdminAlert: IsAdmin);

            if (config.RustPlusSettings.UseRustPlus)
                foreach (BasePlayer playerList in BasePlayer.activePlayerList)
                    NotificationList.SendNotificationTo(playerList.userID, NotificationChannel.SmartAlarm, config.RustPlusSettings.DisplayNameAlert, Message, Util.GetServerPairingData());
        }

        private void DrawUI_IQChat_Alert(BasePlayer player, String Description, String Title = null)
        {
            if (_interface == null)
            {
                PrintWarning("Генерируем интерфейс, ожидайте сообщения об успешной генерации");
                return;
            }
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Alert");
            if (Interface == null) return;

            Interface = Interface.Replace("%TITLE%", Title ?? GetLang("IQCHAT_ALERT_TITLE", player.UserIDString));
            Interface = Interface.Replace("%DESCRIPTION%", Description);

            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_Chat_Alert);
            CuiHelper.AddUi(player, Interface);

            player.Invoke(() =>
            {
                CuiHelper.DestroyUi(player, InterfaceBuilder.UI_Chat_Alert);
            }, config.ControllerMessages.GeneralSetting.OtherSetting.TimeDeleteAlertUI);
        }
        private void CheckValidateUsers()
        {
            Configuration.ControllerParameters Controller = config.ControllerParameter;
            Configuration.ControllerConnection ControllerConnection = config.ControllerConnect;

            List<Configuration.ControllerParameters.AdvancedFuncion> Prefixes = Controller.Prefixes.Prefixes;
            List<Configuration.ControllerParameters.AdvancedFuncion> NickColor = Controller.NickColorList;
            List<Configuration.ControllerParameters.AdvancedFuncion> ChatColor = Controller.MessageColorList;
		   		 		  						  	   		  		 			  		  		  			 		   			
            foreach (KeyValuePair<UInt64, User> Info in UserInformation)
            {
                if (Controller.Prefixes.TurnMultiPrefixes)
                {
                    foreach (String Prefix in Info.Value.Info.PrefixList.Where(prefixList => !Prefixes.Exists(i => i.Argument == prefixList)))
                        NextTick(() => Info.Value.Info.PrefixList.Remove(Prefix));
                }
                else
                {
                    if (!Prefixes.Exists(i => i.Argument == Info.Value.Info.Prefix))
                        Info.Value.Info.Prefix = ControllerConnection.SetupDefaults.PrefixDefault;
                }
                if (!NickColor.Exists(i => i.Argument == Info.Value.Info.ColorNick))
                    Info.Value.Info.ColorNick = ControllerConnection.SetupDefaults.NickDefault;

                if (!ChatColor.Exists(i => i.Argument == Info.Value.Info.ColorMessage))
                    Info.Value.Info.ColorMessage = ControllerConnection.SetupDefaults.MessageDefault;
            }
        }
        private IOrderedEnumerable<BasePlayer> GetPlayerList(String searchName, SelectedAction action)
        {
            List<BasePlayer> basePlayerList = BasePlayer.activePlayerList.ToList();

            if (searchName != null)
                basePlayerList = basePlayerList.Where(p => UserInformation.ContainsKey(p.userID) && p.displayName.ToLower().Contains(searchName.ToLower())).ToList();

            return action == SelectedAction.Mute ?
                basePlayerList.OrderBy(p => UserInformation[p.userID].MuteInfo.IsMute(MuteType.Chat) || UserInformation[p.userID].MuteInfo.IsMute(MuteType.Voice)) :
                basePlayerList.OrderBy(p => UserInformation[p.userID].Settings.IgnoreUsers.Contains(p.userID));
        }
        private void ReplyTranslationMessage(Chat.ChatChannel channel, BasePlayer player, BasePlayer playerSender, String OutMessage, String FormatMessage, String FormatPlayer, UInt64 RenameID)
        {
            ListHashSet<BasePlayer> playerList = GetPlayerList(player, channel);
            if (playerList == null) return;

            if (!String.IsNullOrWhiteSpace(config.ReferenceSetting.translationApiSettings.codeLanguagePrimary))
            {
                Action<String> callback = translation =>
                {
                    foreach (BasePlayer p in playerList)
                        ReplyPlayerChat(channel, p, playerSender, !permission.UserHasPermission(p.UserIDString, PermissionTranslationIgnore) ? translation : OutMessage, FormatMessage, FormatPlayer, RenameID);
                };

                TranslationAPI.Call("Translate", OutMessage, config.ReferenceSetting.translationApiSettings.codeLanguagePrimary, "auto", callback);
            }
            else
            {
                foreach (BasePlayer p in playerList)
                {
                    String codeResult = lang.GetLanguage(p.UserIDString);
                    saveTranslate.TryAdd(codeResult, new TranslationState());

                    Action<String> callback = translation =>
                    {
                        saveTranslate[codeResult].IsProcessed = true;
                        saveTranslate[codeResult].Translation = translation;
                        saveTranslate[codeResult].DoTranslation = OutMessage;
                        
                        ReplyPlayerChat(channel, p, playerSender,!permission.UserHasPermission(p.UserIDString, PermissionTranslationIgnore) ? translation : OutMessage, FormatMessage, FormatPlayer, RenameID);
                    };

                    if (lang.GetLanguage(player.UserIDString) == codeResult)
                        ReplyPlayerChat(channel, p, playerSender, OutMessage, FormatMessage, FormatPlayer, RenameID);
                    else if (saveTranslate[codeResult].IsProcessed && !string.IsNullOrWhiteSpace(saveTranslate[codeResult].Translation) && OutMessage == saveTranslate[codeResult].DoTranslation)
                        ReplyPlayerChat(channel, p, playerSender, saveTranslate[codeResult].Translation, FormatMessage, FormatPlayer, RenameID);
                    else TranslationAPI.Call("Translate", OutMessage, codeResult, "auto", callback);
                }
            }
        }

        private String Format(Int32 units, String form1, String form2, String form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units}{form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units}{form2}";

            return $"{units}{form3}";
        }

        
        private static InterfaceBuilder _interface;
        void AlertUI(BasePlayer Sender, BasePlayer Recipient, string[] arg)
        {
            if (_interface == null)
            {
                PrintWarning(LanguageEn ? "We generate the interface, wait for a message about successful generation" : "Генерируем интерфейс, ожидайте сообщения об успешной генерации");
                return;
            }
            String Message = GetMessageInArgs(Sender, arg);
            if (Message == null) return;

            DrawUI_IQChat_Alert(Recipient, Message);
        }
        private Boolean HasMorePages<T>(IEnumerable<T> items, Int32 page) => items != null && items.Skip(18 * (page + 1)).Any();

        public class Fields
        {
            public string name { get; set; }
            public string value { get; set; }
            public bool inline { get; set; }
            public Fields(string name, string value, bool inline)
            {
                this.name = name;
                this.value = value;
                this.inline = inline;
            }
        }
        
        private void DiscordLoggPM(BasePlayer Sender, BasePlayer Reciepter, String MessageLogged)
        {
            Configuration.OtherSettings.General PMChat = config.OtherSetting.LogsPMChat;
            if (PMChat == null || !PMChat.UseLogged || String.IsNullOrWhiteSpace(PMChat.Webhooks)) return;
            if (Sender == null || Reciepter == null) return;
            
            GeneralInformation.RenameInfo SenderRename = GeneralInfo.GetInfoRename(Sender.userID);
            GeneralInformation.RenameInfo ReciepterRename = GeneralInfo.GetInfoRename(Reciepter.userID);

            UInt64 UserIDSender = SenderRename != null ? SenderRename.RenameID == 0 ? Sender.userID : SenderRename.RenameID : Sender.userID;
            UInt64 UserIDReciepter = ReciepterRename != null ? ReciepterRename.RenameID == 0 ? Reciepter.userID : ReciepterRename.RenameID : Reciepter.userID;
            String SenderName = SenderRename != null ? ReciepterRename.RenameNick ?? Sender.displayName : Sender.displayName;
            String ReciepterName = ReciepterRename != null ? ReciepterRename.RenameNick ?? Reciepter.displayName : Reciepter.displayName;

            List<Fields> fields = new List<Fields>
                        {
                            new Fields(LanguageEn ? "Sender" : "Отправитель", $"{SenderName}({UserIDSender})", true),
                            new Fields(LanguageEn ? "Recipient" : "Получатель", $"{ReciepterName}({UserIDReciepter})", true),
                            new Fields(LanguageEn ? "Message" : "Сообщение", MessageLogged, false),
                        };

            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, 16608621, fields, new Authors("IQChat PM-History", null, "https://i.postimg.cc/SshGgy52/xiwsg5m.png", null), null) });

            Request($"{PMChat.Webhooks}", newMessage.toJSON());
        }
        
        private void DrawUI_IQChat_Mute_And_Ignore_Pages(BasePlayer player, Boolean IsNextPage, SelectedAction Action, Int32 Page = 0)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Mute_And_Ignore_Pages");
            if (Interface == null) return;
		   		 		  						  	   		  		 			  		  		  			 		   			
            String CommandRight = IsNextPage ? $"newui.cmd action.mute.ignore page.controller {Action} {Page + 1}" : String.Empty;
            String ColorRight = String.IsNullOrEmpty(CommandRight) ? "1 1 1 0.1" : "1 1 1 1";

            String CommandLeft = Page > 0 ? $"newui.cmd action.mute.ignore page.controller {Action} {Page - 1}" : String.Empty;
            String ColorLeft = String.IsNullOrEmpty(CommandLeft) ? "1 1 1 0.1" : "1 1 1 1";

            Interface = Interface.Replace("%COMMAND_LEFT%", CommandLeft);
            Interface = Interface.Replace("%COMMAND_RIGHT%", CommandRight);
            Interface = Interface.Replace("%PAGE%", $"{Page}");
            Interface = Interface.Replace("%COLOR_LEFT%", ColorLeft);
            Interface = Interface.Replace("%COLOR_RIGHT%", ColorRight);

            CuiHelper.DestroyUi(player, "PageCount");
            CuiHelper.DestroyUi(player, "LeftPage");
            CuiHelper.DestroyUi(player, "RightPage");
            CuiHelper.AddUi(player, Interface);
        }
        private const String PermissionMute = "iqchat.muteuse";
        
        private void DiscordCompactLoggChat(BasePlayer player, Chat.ChatChannel Channel, String MessageLogged)
        {
            String ChannelTitle = (LanguageEn ? (Channel == Chat.ChatChannel.Global ? "Global" : Channel == Chat.ChatChannel.Local ? "Local" : "Team") : (Channel == Chat.ChatChannel.Global ? "Глобальный чат" : Channel == Chat.ChatChannel.Local ? "Локальный чат" : "Командный чат"));
            String Format = config.OtherSetting.CompactLogsChat.ShowSteamID
                ? $"[{DateTime.Now.ToShortTimeString()}] [{ChannelTitle}] {player.displayName} ({player.userID}): {MessageLogged}"
                : $"[{DateTime.Now.ToShortTimeString()}] [{ChannelTitle}] {player.displayName}: {MessageLogged}";
            
            FancyMessage newMessage = new FancyMessage(Format, false, null);

            switch (Channel)
            {
                case Chat.ChatChannel.Local:
                case Chat.ChatChannel.Global:
                {
                    Configuration.OtherSettings.General GlobalChat = config.OtherSetting.CompactLogsChat.LogsCompactChat.GlobalChatSettings;
                    if (!GlobalChat.UseLogged || String.IsNullOrWhiteSpace(GlobalChat.Webhooks)) return;
                    Request($"{GlobalChat.Webhooks}", newMessage.toJSON());
                    break;
                }
                case Chat.ChatChannel.Team:
                {
                    Configuration.OtherSettings.General TeamChat = config.OtherSetting.CompactLogsChat.LogsCompactChat.TeamChatSettings;
                    if (!TeamChat.UseLogged || String.IsNullOrWhiteSpace(TeamChat.Webhooks)) return;
                    Request($"{TeamChat.Webhooks}", newMessage.toJSON());
                }
                    break;
                default:
                    break;
            }
        }
		   		 		  						  	   		  		 			  		  		  			 		   			
        [ChatCommand("alertui")]
        private void AlertUIChatCommand(BasePlayer Sender, String cmd, String[] args)
        {
            if (!permission.UserHasPermission(Sender.UserIDString, PermissionAlert)) return;
            AlertUI(Sender, args);
        }
        Boolean API_IS_IGNORED(UInt64 UserHas, UInt64 User)
        {
            if (!UserInformation.ContainsKey(UserHas)) return false;
            if (!UserInformation.ContainsKey(User)) return false;
		   		 		  						  	   		  		 			  		  		  			 		   			
            return UserInformation[UserHas].Settings.IsIgnored(User);
        }

        private String GetLastMessage(BasePlayer player, Int32 Count)
        {
            String Messages = String.Empty;

            if (LastMessagesChat.ContainsKey(player))
            {
                foreach (String Message in LastMessagesChat[player].Skip(LastMessagesChat[player].Count - Count))
                    Messages += $"\n{Message}";
            }

            return Messages;
        }
        
        public static Regex regex = new Regex(@"(:\w+:|<[^>]+>)");
        String API_GET_DEFAULT_NICK_COLOR() => config.ControllerConnect.SetupDefaults.NickDefault;


        [ChatCommand("unmute")]
        void UnMuteCustomChat(BasePlayer Moderator, string cmd, string[] arg)
        {
            if (!permission.UserHasPermission(Moderator.UserIDString, PermissionMute)) return;
            if (arg == null || arg.Length != 1 || arg.Length > 1)
            {
                ReplySystem(Moderator, LanguageEn ? "Invalid syntax, please use : unmute Steam64ID" : "Неверный синтаксис,используйте : unmute Steam64ID");
                return;
            }
            string NameOrID = arg[0];
            
            if (IsFakeUser(NameOrID))
            {
                List<FakePlayer> playerList = GetCombinedPlayerList();
                if (playerList != null)
                {
                    FakePlayer fakeUser = playerList.FirstOrDefault(x => x.userId.Equals(NameOrID) || x.displayName.ToLower().Contains(NameOrID.ToLower()));
                    if (fakeUser != null)
                        UnmutePlayer(null, MuteType.Chat, Moderator, false, true, fakeUser.userId);
                }
                return;
            }
            
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (!Info.MuteInfo.IsMute(MuteType.Chat))
                        {
                            ReplySystem(Moderator, LanguageEn ? "The player does not have a chat lock" : "У игрока нет блокировки чата");
                            return;
                        }
                        
                        if (!Moderator.IsAdmin && Moderator.userID == Steam64ID)
                        {
                            ReplySystem(Moderator, GetLang("FUNC_MESSAGE_UNMUTE_YOURSELF", Moderator.UserIDString));
                            return;
                        }

                        Info.MuteInfo.UnMute(MuteType.Chat);
                        ReplySystem(Moderator, LanguageEn ? "You have unblocked the offline chat to the player" : "Вы разблокировали чат offline игроку");
                        return;
                    }
                    else
                    {
                        ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }
            UnmutePlayer(target, MuteType.Chat, Moderator, false, true);
        }
        internal class AntiNoob
        {
            public DateTime DateConnection = DateTime.UtcNow;
		   		 		  						  	   		  		 			  		  		  			 		   			
            public Boolean IsNoob(Int32 TimeBlocked)
            {
                System.TimeSpan Time = DateTime.UtcNow.Subtract(DateConnection);
                return Time.TotalSeconds < TimeBlocked;
            }

            public Double LeftTime(Int32 TimeBlocked)
            {
                System.TimeSpan Time = DateTime.UtcNow.Subtract(DateConnection);

                return (TimeBlocked - Time.TotalSeconds);
            }
        }
        
        
        private void ControlledBadNick(IPlayer player)
        {
            if (player == null) return;
            Configuration.ControllerMessage ControllerMessage = config.ControllerMessages;

            String DisplayName = player.Name;
		   		 		  						  	   		  		 			  		  		  			 		   			
            Tuple<String, Boolean> GetTupleNick = BadWordsCleaner(DisplayName,
                ControllerMessage.Formatting.ControllerNickname.ReplaceBadNick,
                ControllerMessage.Formatting.ControllerNickname.BadNicks);
            DisplayName = GetTupleNick.Item1;

            DisplayName = RemoveLinkText(DisplayName);
            player.Rename(DisplayName);
        }

        private String GetReferenceTags(BasePlayer player)
        {
            String Result = String.Empty;
            String Rank = String.Empty;
            String RankTime = String.Empty;
            if (IQRankSystem)
            {
                Configuration.ReferenceSettings.IQRankSystem IQRank = config.ReferenceSetting.IQRankSystems;

                if (IQRank.UseRankSystem)
                {
                    if (IQRank.UseTimeStandart)
                        RankTime = String.IsNullOrWhiteSpace(IQRankGetTimeGame(player.userID)) ? String.Empty : String.Format(IQRank.FormatRankTime, IQRankGetTimeGame(player.userID));
                    Rank = String.IsNullOrWhiteSpace(IQRankGetRank(player.userID)) ? String.Empty : String.Format(IQRank.FormatRank, IQRankGetRank(player.userID));
		   		 		  						  	   		  		 			  		  		  			 		   			
                    if (!String.IsNullOrWhiteSpace(RankTime))
                        Result += $"{RankTime} ";
                    if (!String.IsNullOrWhiteSpace(Rank))
                        Result += $"{Rank} ";
                }
            }

            String XPrison = XPrison_GetPrefix(player);
            if (!String.IsNullOrWhiteSpace(XPrison))
                Result += $"{XPrison} ";
            
            String XLevel = config.ReferenceSetting.XLevelsSettings.UseFullXLevels ? XLevel_GetPrefix(player) : XLevel_GetLevel(player);
            if (!String.IsNullOrWhiteSpace(XLevel))
                Result += $"{XLevel} ";

            String ClanTag = GetClanTag(player.userID);
            if (!String.IsNullOrWhiteSpace(ClanTag))
                Result += $"{ClanTag} ";

            String SkillTree = config.ReferenceSetting.skillTreeSettings.UseFullSkillTree ? $"{SkillTree_GetLevel(player)} {SkillTree_GetXP(player)}" : $"{SkillTree_GetLevel(player)}";
            if (!String.IsNullOrWhiteSpace(SkillTree))
                Result += $"{SkillTree} ";

            String PrestigeSkillTree = GetPrestigeLevel(player.userID.Get());
            if (!String.IsNullOrWhiteSpace(PrestigeSkillTree))
                Result += $"{PrestigeSkillTree} ";
            
            String PlayerRanks = PlayerRanks_GetRanks(player);
            if (!String.IsNullOrWhiteSpace(PlayerRanks))
                Result += $"{PlayerRanks} ";
            
            String hookResult = Interface.Call<String>("OnChatReferenceTags", player);
            if (!String.IsNullOrWhiteSpace(hookResult))
                Result += $"{hookResult} ";
            
            return Result;
        }
        private static ImageUI _imageUI;

        
        
        private void ReplyPlayerChat(Chat.ChatChannel channel, BasePlayer player, BasePlayer playerSender, String OutMessage, String FormatMessage, String FormatPlayer, UInt64 RenameID)
        {
            Configuration.ControllerMessage ControllerMessages = config.ControllerMessages;
            
            User Info = UserInformation[playerSender.userID];
            String colorMessage = !String.IsNullOrWhiteSpace(Info.Info.CustomColorMessage)
                ? Info.Info.CustomColorMessage
                : Info.Info.ColorMessage;
            
            Object resultColorMessage = IsGradientColorValue(colorMessage);
            if (resultColorMessage is List<String> gradientColors)
                OutMessage = ApplyGradientToText(OutMessage, gradientColors);
            else OutMessage = !String.IsNullOrWhiteSpace(colorMessage) ? $"<color={colorMessage}>{OutMessage}</color>" : OutMessage;
            
            String messageSend = String.Format(FormatMessage, OutMessage);
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (messageSend.Contains("@"))
            {
                String SplittedName = messageSend.Substring(messageSend.IndexOf('@')).Replace("@", "").Split(' ')[0];

                BasePlayer playerTags = GetPlayerNickOrID(SplittedName);
		   		 		  						  	   		  		 			  		  		  			 		   			
                if (playerTags != null)
                {
                    User InfoP = UserInformation[playerTags.userID];

                    if (InfoP.Settings.TurnAlert && player == playerTags)
                    {
                        ReplySystem(player, $"<size=16>{messageSend.Trim()}</size>", GetLang("IQCHAT_FUNCED_ALERT_TITLE", player.UserIDString, playerTags.displayName), player.UserIDString, ControllerMessages.GeneralSetting.AlertFormat.AlertPlayerColor);
                        if (InfoP.Settings.TurnSound)
                            Effect.server.Run(ControllerMessages.GeneralSetting.AlertFormat.SoundAlertPlayer, playerTags.GetNetworkPosition());
                    }
                    else player.SendConsoleCommand("chat.add", (int)channel, RenameID, $"{FormatPlayer}: {messageSend}");
                }
                else player.SendConsoleCommand("chat.add", (int)channel, RenameID, $"{FormatPlayer}: {messageSend}");
            }
            else player.SendConsoleCommand("chat.add", (int)channel, RenameID, $"{FormatPlayer}: {messageSend}");
            
            player.ConsoleMessage($"{FormatPlayer} {messageSend}");
        }
        
        private class InterfaceBuilder
        {
            
            public static InterfaceBuilder Instance;
            public const String UI_Chat_Context = "UI_IQCHAT_CONTEXT";
            public const String UI_Chat_Context_Visual_Nick = "UI_IQCHAT_CONTEXT_VISUAL_NICK";
            public const String UI_Chat_Alert = "UI_IQCHAT_ALERT";
            public Dictionary<String, String> Interfaces;

            
            
            public InterfaceBuilder()
            {
                Instance = this;
                Interfaces = new Dictionary<String, String>();
                BuildingStaticContext();
                BuildingVisualNick();
                BuildingCheckBox();

                BuildingModerationStatic();
                BuildingMuteAllChat();
                BuildingMuteAllVoice();

                BuildingSlider();
                BuildingSliderUpdateArgument();

                BuildingDropList();
                BuildingOpenDropList();
                BuildingElementDropList();
                BuildingElementDropListTakeLine();
		   		 		  						  	   		  		 			  		  		  			 		   			
                BuildingAlertUI();

                BuildingMuteAndIgnore();
                BuildingMuteAndIgnorePlayerPanel();
                BuildingMuteAndIgnorePlayer();
                BuildingMuteAndIgnorePages();

                BuildingMuteAndIgnorePanelAlert();
                BuildingIgnoreAlert();
                BuildingMuteAlert();
                BuildingMuteAlert_DropList_Title();
                BuildingMuteAlert_DropList_Reason();
            }

            public static void AddInterface(String name, String json)
            {
                if (Instance.Interfaces.ContainsKey(name))
                {
                    _.PrintError($"Error! Tried to add existing cui elements! -> {name}");
                    return;
                }

                Instance.Interfaces.Add(name, json);
            }

            public static string GetInterface(String name)
            {
                string json = string.Empty;
                if (Instance.Interfaces.TryGetValue(name, out json) == false)
                {
                    _.PrintWarning($"Warning! UI elements not found by name! -> {name}");
                }

                return json;
            }

            public static void DestroyAll()
            {
                for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
                {
                    var player = BasePlayer.activePlayerList[i];
                    CuiHelper.DestroyUi(player, UI_Chat_Context);
                    CuiHelper.DestroyUi(player, UI_Chat_Context_Visual_Nick);
                    CuiHelper.DestroyUi(player, UI_Chat_Alert);
                    CuiHelper.DestroyUi(player, "MUTE_AND_IGNORE_PANEL_ALERT");
                }
            }

            
            
                        private void BuildingVisualNick()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = UI_Chat_Context_Visual_Nick,
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%NICK_DISPLAY%", Font = "robotocondensed-regular.ttf", FontSize = 7, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-135.769 -89.558", OffsetMax = "-12.644 -77.176" }
                }
                });

                AddInterface("UI_Chat_Context_Visual_Nick", container.ToJson());
            }
            
                        private void BuildingStaticContext()
            {
                Configuration.ControllerParameters Controller = config.ControllerParameter;
                if (Controller == null)
                {
                    _.PrintWarning("Ошибка генерации интерфейса, null значение в конфигурации, свяжитесь с разработчиком");
                    return;
                }
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "1 0.5", AnchorMax = "1 0.5", OffsetMin = "-379 -217", OffsetMax = "-31 217" },
                    Image = { Color = "0 0 0 0" }
                }, "Overlay", UI_Chat_Context);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "1000 1000", OffsetMax = "-1000 -1000" },
                    Button = { Close = UI_Chat_Context, Color = "0 0 0 0.5" },
                    Text = { Text = "" }
                }, UI_Chat_Context, "CLOSE_UI_Chat_Context_FullScreen");

                container.Add(new CuiElement
                {
                    Name = "ImageContext",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = "%IMG_BACKGROUND%" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "TitleLabel",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 17, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-149.429 166.408", OffsetMax = "-14.788 189.564" }
                }
                });
		   		 		  						  	   		  		 			  		  		  			 		   			
                container.Add(new CuiElement
                {
                    Name = "DescriptionLabel",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%SETTING_ELEMENT%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-149.429 112.021514", OffsetMax = "152.881 131.787" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "InformationLabel",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%INFORMATION%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-149.429 -53.432", OffsetMax = "-32.905 -39.808" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "InformationIcon",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_INFORMATION_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-14.788 -52.12", OffsetMax = "-3.788 -41.12" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "SettingLabel",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%SETTINGS%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "11.075 -53.432", OffsetMax = "126.125 -39.808" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "SettingIcon",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_SETTING_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "141.88 -52.12", OffsetMax = "152.88 -41.12" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "SettingPM",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%SETTINGS_PM%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "11.075 -70.712", OffsetMax = "126.125 -57.088" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "SettingAlertChat",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%SETTINGS_ALERT%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "11.075 -82.412", OffsetMax = "126.125 -68.788" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "SettingNoticyChat",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%SETTINGS_ALERT_PM%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "11.075 -94.412", OffsetMax = "126.125 -80.788" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "SettingSoundAlert",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%SETTINGS_SOUNDS%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "11.075 -106.412", OffsetMax = "126.125 -92.788" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "MuteStatus",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%MUTE_STATUS_PLAYER%", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-143.174 -131.59", OffsetMax = "-120.611 -114.967" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "MuteStatusTitle",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%MUTE_STATUS_TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 7, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-143.174 -141.429", OffsetMax = "-89.127 -132.508" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "CountIgnored",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%IGNORED_STATUS_COUNT%", Font = "robotocondensed-regular.ttf", FontSize = 7, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-66.98 -131.715", OffsetMax = "-11.09 -116.831" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "IgonoredTitle",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%IGNORED_STATUS_TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 7, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-66.98 -142.04", OffsetMax = "-19.967 -132.537" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "IgnoredIcon",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_IGNORE_INFO_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-19.483 -115.225", OffsetMax = "-11.762 -107.814" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = $"newui.cmd action.mute.ignore open {SelectedAction.Ignore}", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "IgnoredIcon", "CLOSE_IGNORED");

                container.Add(new CuiElement
                {
                    Name = "TitleNickPanel",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%NICK_DISPLAY_TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-135.769 -78.878", OffsetMax = "-85.632 -64.613" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "NickTitle",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%SLIDER_NICK_COLOR_TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "22.591 76.362", OffsetMax = "80.629 92.278" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "ChatMessageTitle",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%SLIDER_MESSAGE_COLOR_TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-136.591 9.362", OffsetMax = "-78.045 24.278" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "PrefixTitle",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%SLIDER_PREFIX_TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-136.591 77.362", OffsetMax = "-89.949 93.278" }
                }
                });


                container.Add(new CuiElement
                {
                    Name = "RankTitle",
                    Parent = UI_Chat_Context,
                    Components = {
                        new CuiTextComponent { Text = "%SLIDER_IQRANK_TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "22.825 9.242", OffsetMax = "81.375 25.158" }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "123.62 166", OffsetMax = "153.62 196" },
                    Button = { Close = UI_Chat_Context, Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, UI_Chat_Context, "CLOSE_UI_Chat_Context");

                AddInterface("UI_Chat_Context", container.ToJson());
            }

            
                        private void BuildingCheckBox()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "%NAME_CHECK_BOX%",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "%COLOR%", Png = _imageUI.GetImage("IQCHAT_ELEMENT_SETTING_CHECK_BOX") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%COMMAND_TURNED%", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "%NAME_CHECK_BOX%", "CHECK_BOX_TURNED");

                AddInterface("UI_Chat_Context_CheckBox", container.ToJson());
            }
            
                        private void BuildingSlider()
            {
                CuiElementContainer container = new CuiElementContainer();
                String NameSlider = "%NAME%";

                container.Add(new CuiElement
                {
                    Name = NameSlider,
                    Parent = UI_Chat_Context,
                    Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_SLIDER_ICON") },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%" , OffsetMax = "%OFFSET_MAX%"  }
                        }
                });

                container.Add(new CuiElement
                {
                    Name = "Left",
                    Parent = NameSlider,
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_SLIDER_LEFT_ICON") },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-53.9 -4.5", OffsetMax = "-48.9 4.5" }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%COMMAND_LEFT_SLIDE%", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "Left", "LEFT_SLIDER_BTN");

                container.Add(new CuiElement
                {
                    Name = "Right",
                    Parent = NameSlider,
                    Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_SLIDER_RIGHT_ICON") },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "48.92 -4.5", OffsetMax = "53.92 4.5" }
                    }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%COMMAND_RIGHT_SLIDE%", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "Right", "RIGHT_SLIDER_BTN");

                AddInterface("UI_Chat_Slider", container.ToJson());
            }
            private void BuildingSliderUpdateArgument()
            {
                CuiElementContainer container = new CuiElementContainer();
                String ParentSlider = "%PARENT%";
                String NameArgument = "%NAME%";

                container.Add(new CuiElement
                {
                    Name = NameArgument,
                    Parent = ParentSlider,
                    Components = {
                    new CuiTextComponent { Text = "%ARGUMENT%", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-41.929 -6.801", OffsetMax = "41.929 6.801" }
                }
                });

                AddInterface("UI_Chat_Slider_Update_Argument", container.ToJson());
            }
            
            
                        private void BuildingMuteAndIgnore()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "MuteAndIgnoredPanel",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_MUTE_AND_IGNORE_PANEL")},
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-1007.864 -220.114", OffsetMax = "-167.374 219.063" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "TitlesPanel",
                    Parent = "MuteAndIgnoredPanel",
                    Components = {
                    new CuiTextComponent { Text = "%TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "60.217 164.031", OffsetMax = "356.114 190.962" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "IconPanel",
                    Parent = "MuteAndIgnoredPanel",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_MUTE_AND_IGNORE_ICON")},
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "357.5 170", OffsetMax = "373.5 185"  }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "SearchPanel",
                    Parent = "MuteAndIgnoredPanel",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_MUTE_AND_IGNORE_SEARCH")},
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-385.8 161.244", OffsetMax = "-186.349 192.58" }
                }
                });

                string SearchName = "";

                container.Add(new CuiElement
                {
                    Parent = "SearchPanel",
                    Name = "SearchPanel" + ".Input.Current",
                    Components =
                {
                    new CuiInputFieldComponent { Text = SearchName, FontSize = 14,Command = $"newui.cmd action.mute.ignore search.controller %ACTION_TYPE% {SearchName}", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.5", CharsLimit = 15},
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "PanelPages",
                    Parent = "MuteAndIgnoredPanel",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_MUTE_AND_IGNORE_PAGE_PANEL")},
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-179.196 161.242", OffsetMax = "-121.119 192.578" }
                }
                });

                AddInterface("UI_Chat_Mute_And_Ignore", container.ToJson());
            }

            private void BuildingMuteAndIgnorePlayerPanel()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.85" },
                    Image = { Color = "0 0 0 0" }
                }, "MuteAndIgnoredPanel", "MuteIgnorePanelContent");

                AddInterface("UI_Chat_Mute_And_Ignore_Panel_Content", container.ToJson());
            }
            private void BuildingMuteAndIgnorePlayer()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "PANEL_PLAYER",
                    Parent = "MuteIgnorePanelContent",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_MUTE_AND_IGNORE_PLAYER") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "NickName",
                    Parent = "PANEL_PLAYER",
                    Components = {
                    new CuiTextComponent { Text = "%DISPLAY_NAME%", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-77.391 -17.245", OffsetMax = "91.582 17.244" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "StatusPanel",
                    Parent = "PANEL_PLAYER",
                    Components = {
                    new CuiRawImageComponent { Color = "%COLOR%", Png = _imageUI.GetImage("IQCHAT_MUTE_AND_IGNORE_PLAYER_STATUS") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-92.231 -11.655", OffsetMax = "-87.503 10.44" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%COMMAND_ACTION%", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "PANEL_PLAYER");

                AddInterface("UI_Chat_Mute_And_Ignore_Player", container.ToJson());
            }
            private void BuildingMuteAndIgnorePages()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "PageCount",
                    Parent = "PanelPages",
                    Components = {
                    new CuiTextComponent { Text = "%PAGE%", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-11.03 -15.668", OffsetMax = "11.03 15.668" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "LeftPage",
                    Parent = "PanelPages",
                    Components = {
                    new CuiRawImageComponent { Color = "%COLOR_LEFT%", Png = _imageUI.GetImage("IQCHAT_ELEMENT_SLIDER_LEFT_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-18 -7", OffsetMax = "-13 6" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%COMMAND_LEFT%", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "LeftPage");

                container.Add(new CuiElement
                {
                    Name = "RightPage",
                    Parent = "PanelPages",
                    Components = {
                    new CuiRawImageComponent { Color = "%COLOR_RIGHT%", Png = _imageUI.GetImage("IQCHAT_ELEMENT_SLIDER_RIGHT_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "14 -7", OffsetMax = "19 6" }
                }
                });
		   		 		  						  	   		  		 			  		  		  			 		   			
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%COMMAND_RIGHT%", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "RightPage");

                AddInterface("UI_Chat_Mute_And_Ignore_Pages", container.ToJson());
            }

            
                        private void BuildingMuteAndIgnorePanelAlert()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image = { Color = "0 0 0 0.25", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
                }, "Overlay", "MUTE_AND_IGNORE_PANEL_ALERT");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Close = "MUTE_AND_IGNORE_PANEL_ALERT", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "MUTE_AND_IGNORE_PANEL_ALERT");

                AddInterface("UI_Chat_Mute_And_Ignore_Alert_Panel", container.ToJson());
            }

            
            private void BuildingMuteAlert()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "AlertMute",
                    Parent = "MUTE_AND_IGNORE_PANEL_ALERT",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_MUTE_ALERT_PANEL") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-199.832 -274.669", OffsetMax = "199.832 274.669" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "AlertMuteIcon",
                    Parent = "AlertMute",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_MUTE_ALERT_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-67 204.8", OffsetMax = "67 339.8" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "AlertMuteTitles",
                    Parent = "AlertMute",
                    Components = {
                    new CuiTextComponent { Text = "%TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 25, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-199.828 142.57", OffsetMax = "199.832 179.43" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "AlertMuteTakeChat",
                    Parent = "AlertMute",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1",Png = _imageUI.GetImage("IQCHAT_IGNORE_ALERT_BUTTON_YES") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-99.998 87.944", OffsetMax = "100.002 117.944" }
                }
                });
		   		 		  						  	   		  		 			  		  		  			 		   			
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%COMMAND_TAKE_ACTION_MUTE_CHAT%", Color = "0 0 0 0" },
                    Text = { Text = "%BUTTON_TAKE_CHAT_ACTION%", Align = TextAnchor.MiddleCenter, FontSize = 18, Color = "0.1294118 0.145098 0.1647059 1" }
                }, "AlertMuteTakeChat", "BUTTON_TAKE_CHAT");

                container.Add(new CuiElement
                {
                    Name = "AlertMuteTakeVoice",
                    Parent = "AlertMute",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1",Png = _imageUI.GetImage("IQCHAT_IGNORE_ALERT_BUTTON_YES") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-100 49.70", OffsetMax = "100 79.70" } //
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%COMMAND_TAKE_ACTION_MUTE_VOICE%", Color = "0 0 0 0" },
                    Text = { Text = "%BUTTON_TAKE_VOICE_ACTION%", Align = TextAnchor.MiddleCenter, FontSize = 18, Color = "0.1294118 0.145098 0.1647059 1" }
                }, "AlertMuteTakeVoice", "BUTTON_TAKE_VOICE");

                AddInterface("UI_Chat_Mute_Alert", container.ToJson());
            }
            private void BuildingMuteAlert_DropList_Title()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "AlertMuteTitleReason",
                    Parent = "AlertMute",
                    Components = {
                    new CuiTextComponent { Text = "%TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-199.828 -9.430", OffsetMax = "199.832 27.430" }
                }
                });

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "1 1 1 0" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-147.497 -265.5440", OffsetMax = "147.503 -24.70" }
                }, "AlertMute", "PanelMuteReason");

                AddInterface("UI_Chat_Mute_Alert_DropList_Title", container.ToJson());
            }

            private void BuildingMuteAlert_DropList_Reason()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "Reason",
                    Parent = "PanelMuteReason",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_MUTE_ALERT_PANEL_REASON")},
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%COMMAND_REASON%", Color = "0 0 0 0" },
                    Text = { Text = "%REASON%", Align = TextAnchor.MiddleCenter, FontSize = 13, Color = "1 1 1 1" }
                }, "Reason");

                AddInterface("UI_Chat_Mute_Alert_DropList_Reason", container.ToJson());
            }
            
                        private void BuildingIgnoreAlert()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "AlertIgnore",
                    Parent = "MUTE_AND_IGNORE_PANEL_ALERT",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_IGNORE_ALERT_PANEL") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-236.5 -134", OffsetMax = "236.5 134" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "AlertIgnoreIcon",
                    Parent = "AlertIgnore",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_IGNORE_ALERT_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-66.5 64.8", OffsetMax = "66.5 198.8" }
                }
                });
		   		 		  						  	   		  		 			  		  		  			 		   			
                container.Add(new CuiElement
                {
                    Name = "AlertIgnoreTitle",
                    Parent = "AlertIgnore",
                    Components = {
                    new CuiTextComponent { Text = "%TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 22, Align = TextAnchor.UpperCenter, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-231 -55.00", OffsetMax = "229.421 33.98514" } 
                }
                });

                container.Add(new CuiElement
                {
                    Name = "AlertIgnoreYes",
                    Parent = "AlertIgnore",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_IGNORE_ALERT_BUTTON_YES") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-178 -115", OffsetMax = "-22 -77" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Close = "MUTE_AND_IGNORE_PANEL_ALERT", Command = "%COMMAND%", Color = "0 0 0 0" },
                    Text = { Text = "%BUTTON_YES%", Align = TextAnchor.MiddleCenter, FontSize = 18, Color = "0.1294118 0.145098 0.1647059 1" }
                }, "AlertIgnoreYes", "BUTTON_YES");

                container.Add(new CuiElement
                {
                    Name = "AlertIgnoreNo",
                    Parent = "AlertIgnore",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_IGNORE_ALERT_BUTTON_NO") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "22 -115", OffsetMax = "178 -77" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Close = "MUTE_AND_IGNORE_PANEL_ALERT", Color = "0 0 0 0" },
                    Text = { Text = "%BUTTON_NO%", Align = TextAnchor.MiddleCenter, FontSize = 18 }
                }, "AlertIgnoreNo", "BUTTON_NO");
		   		 		  						  	   		  		 			  		  		  			 		   			
                AddInterface("UI_Chat_Ignore_Alert", container.ToJson());
            }
            
            
            
            
            private void BuildingDropList()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "DropListIcon",
                    Parent = UI_Chat_Context,
                    Components = {
                            new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_PREFIX_MULTI_TAKE_ICON")},
                      new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                        }
                });

                container.Add(new CuiElement
                {
                    Name = "DropListDescription",
                    Parent = "DropListIcon",
                    Components = {
                            new CuiTextComponent { Text = "%TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                            new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-105.5 -13.948", OffsetMax = "-42.615 1.725" }
                        }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%BUTTON_DROP_LIST_CMD%", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "DropListIcon", "DropListIcon_Button");

                AddInterface("UI_Chat_DropList", container.ToJson());
            }

            private void BuildingOpenDropList()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "OpenDropList",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_DROP_LIST_OPEN_ICON")},
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-149.429 -17.38", OffsetMax = "155.093 109.1" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "DropListName",
                    Parent = "OpenDropList",
                    Components = {
                    new CuiTextComponent { Text = "%TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 11, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-140.329 44.5", OffsetMax = "-40.329 58.312" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "DropListDescription",
                    Parent = "OpenDropList",
                    Components = {
                    new CuiTextComponent { Text = "%DESCRIPTION%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-140.329 32.993", OffsetMax = "-40.329 42.77" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "DropListClose",
                    Parent = "OpenDropList",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_PREFIX_MULTI_TAKE_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "113 32.2", OffsetMax = "145 56.2" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Close = "OpenDropList", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "DropListClose", "DropListClose_Button");

                container.Add(new CuiElement
                {
                    Name = "DropListPageRight",
                    Parent = "OpenDropList",
                    Components = {
                    new CuiRawImageComponent { Color = "%COLOR_RIGHT%", Png = _imageUI.GetImage("IQCHAT_ELEMENT_SLIDER_RIGHT_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "100 38", OffsetMax = "105.2 48" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%NEXT_BTN%", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "DropListPageRight", "DropListPageRight_Button");

                container.Add(new CuiElement
                {
                    Name = "DropListPageLeft",
                    Parent = "OpenDropList",
                    Components = {
                    new CuiRawImageComponent { Color ="%COLOR_LEFT%", Png = _imageUI.GetImage("IQCHAT_ELEMENT_SLIDER_LEFT_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "86 38", OffsetMax = "91.2 48" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "%BACK_BTN%", Color = "0 0 0 0" },
                    Text = { Text = "" }
                }, "DropListPageLeft", "DropListPageLeft_Button");

                AddInterface("UI_Chat_OpenDropList", container.ToJson());
            }

            private void BuildingElementDropList()
            {
                CuiElementContainer container = new CuiElementContainer();
                String Name = "ArgumentDropList_%COUNT%";

                container.Add(new CuiElement
                {
                    Name = Name,
                    Parent = "OpenDropList",
                    Components = {
                    new CuiRawImageComponent { FadeIn = 0.3f, Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_DROP_LIST_OPEN_ARGUMENT_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "%OFFSET_MIN%", OffsetMax = "%OFFSET_MAX%" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-37.529 -12.843", OffsetMax = "37.528 12.842" },
                    Button = { FadeIn = 0.3f, Command = "%TAKE_COMMAND_ARGUMENT%", Color = "0 0 0 0" },
                    Text = { FadeIn = 0.3f, Text = "%ARGUMENT%", Font = "robotocondensed-regular.ttf", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, Name, "ArgumentButton");

                AddInterface("UI_Chat_OpenDropListArgument", container.ToJson());
            }

            private void BuildingElementDropListTakeLine()
            {
                CuiElementContainer container = new CuiElementContainer();
                String Parent = "ArgumentDropList_%COUNT%";

                container.Add(new CuiElement
                {
                    Name = "TAKED_INFO_%COUNT%",
                    Parent = Parent,
                    Components = {
                    new CuiRawImageComponent { Color = "0.3098039 0.2745098 0.572549 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_DROP_LIST_OPEN_TAKED") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-25.404 -17.357", OffsetMax = "25.403 -1.584" }
                }
                });

                AddInterface("UI_Chat_OpenDropListArgument_Taked", container.ToJson());
            }
		   		 		  						  	   		  		 			  		  		  			 		   			
            
                        private void BuildingModerationStatic()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "ModerationLabel",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiTextComponent { Text = "%TITLE%", Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "11.075 -126.612", OffsetMax = "126.125 -112.988" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "ModerationIcon",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_MODERATION_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "141.88 -125.3", OffsetMax = "152.88 -114.3" }
                }
                });


                container.Add(new CuiElement
                {
                    Name = "ModeratorMuteMenu",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_PANEL_ICON")},
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "11.071 -144.188", OffsetMax = "152.881 -129.752" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.95" },
                    Button = { Command = "%COMMAND_MUTE_MENU%", Color = "0 0 0 0" },
                    Text = { Text = "%TEXT_MUTE_MENU%", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, "ModeratorMuteMenu", "ModeratorMuteMenu_Btn");


                AddInterface("UI_Chat_Moderation", container.ToJson());
            }
            private void BuildingMuteAllChat()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "ModeratorMuteAllChat",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_PANEL_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "11.07 -161.818", OffsetMax = "152.88 -147.382" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.95" },
                    Button = { Command = "%COMMAND_MUTE_ALLCHAT%", Color = "0 0 0 0" },
                    Text = { Text = "%TEXT_MUTE_ALLCHAT%", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, "ModeratorMuteAllChat", "ModeratorMuteAllChat_Btn");

                AddInterface("UI_Chat_Administation_AllChat", container.ToJson());
            }
            private void BuildingMuteAllVoice()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = "ModeratorMuteAllVoice",
                    Parent = UI_Chat_Context,
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ELEMENT_PANEL_ICON") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "11.075 -179.448", OffsetMax = "152.885 -165.012" }
                }
                });

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.95" },
                    Button = { Command = "%COMMAND_MUTE_ALLVOICE%", Color = "0 0 0 0" },
                    Text = { Text = "%TEXT_MUTE_ALLVOICE%", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, "ModeratorMuteAllVoice", "ModeratorMuteAllVoice_Btn");

                AddInterface("UI_Chat_Administation_AllVoce", container.ToJson());
            }

            
            
                        private void BuildingAlertUI()
            {
                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiElement
                {
                    Name = UI_Chat_Alert,
                    Parent = "Overlay",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = _imageUI.GetImage("IQCHAT_ALERT_PANEL") },
                    new CuiRectTransformComponent { AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -136.5", OffsetMax = "434 -51.5" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "AlertTitle",
                    Parent = UI_Chat_Alert,
                    Components = {
                    new CuiTextComponent { Text = "<b>%TITLE%</b>", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-184.193 9.119", OffsetMax = "189.223 30.925" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "AlertText",
                    Parent = UI_Chat_Alert,
                    Components = {
                    new CuiTextComponent { Text = "%DESCRIPTION%", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-184.193 -27.133", OffsetMax = "189.223 9.119" }
                }
                });

                AddInterface("UI_Chat_Alert", container.ToJson());
            }
                    }

        private object OnServerMessage(String message, String name)
        {
            if (config.ControllerMessages.TurnedFunc.HideAdminGave)
                if (message.Contains("gave") && name == "SERVER")
                    return true;
            return null;
        }
        String API_GET_DEFAULT_PREFIX() => config.ControllerConnect.SetupDefaults.PrefixDefault;

                
        
        private String XLevel_GetLevel(BasePlayer player)
        {
            if (!XLevels || !config.ReferenceSetting.XLevelsSettings.UseXLevels) return String.Empty;
            String level = GetLang("XLEVELS_SYNTAX_PREFIX", player.UserIDString, (Int32)XLevels?.CallHook("API_GetLevel", player));
            
            Object resultColorMessage = IsGradientColorValue(config.ReferenceSetting.XLevelsSettings.colorTag);
            if (resultColorMessage is List<String> gradientColors)
                level = ApplyGradientToText(level, gradientColors);
            else level = $"<color={config.ReferenceSetting.XLevelsSettings.colorTag}>{level}</color>";
		   		 		  						  	   		  		 			  		  		  			 		   			
            return level;
        }

                private void DrawUI_IQChat_Update_DisplayName(BasePlayer player)
        {
            String InterfaceVisualNick = InterfaceBuilder.GetInterface("UI_Chat_Context_Visual_Nick");
            User Info = UserInformation[player.userID];
            Configuration.ControllerParameters Controller = config.ControllerParameter;
            if (Info == null || InterfaceVisualNick == null || Controller == null) return;

            String DisplayNick = String.Empty;

            String Pattern = @"</?size.*?>";
            // if (Controller.Prefixes.TurnMultiPrefixes) 
            // {
            //     if (Info.Info.PrefixList != null && Info.Info.PrefixList.Count != 0)
            //         DisplayNick += Info.Info.PrefixList.Count > 1 ? $"{(Regex.IsMatch(Info.Info.PrefixList[0], Pattern) ? Regex.Replace(Info.Info.PrefixList[0], Pattern, "") : Info.Info.PrefixList[0])}+{Info.Info.PrefixList.Count - 1}" :
            //             (Regex.IsMatch(Info.Info.PrefixList[0], Pattern) ? Regex.Replace(Info.Info.PrefixList[0], Pattern, "") : Info.Info.PrefixList[0]);
            // }
            // else DisplayNick += Regex.IsMatch(Info.Info.Prefix, Pattern) ? Regex.Replace(Info.Info.Prefix, Pattern, "") : Info.Info.Prefix;
            //
            if (Controller.Prefixes.TurnMultiPrefixes)
            {
                if (Info.Info.PrefixList != null && Info.Info.PrefixList.Count != 0)
                {
                    if (Info.Info.PrefixList[0] != null && Regex.IsMatch(Info.Info.PrefixList[0], Pattern))
                        DisplayNick += Regex.Replace(Info.Info.PrefixList[0], Pattern, "");
                    else
                        DisplayNick += Info.Info.PrefixList[0];

                    DisplayNick += Info.Info.PrefixList.Count > 1 ? $"+{Info.Info.PrefixList.Count - 1}" : string.Empty;
                }
            }
            else
            {
                if (Info.Info.Prefix != null && Regex.IsMatch(Info.Info.Prefix, Pattern))
                    DisplayNick += Regex.Replace(Info.Info.Prefix, Pattern, "");
                else DisplayNick += Info.Info.Prefix;
            }

            String DisplayName = player.displayName;
            String ColorNickPlayer = !String.IsNullOrWhiteSpace(Info.Info.CustomColorNick)
                ? Info.Info.CustomColorNick
                : (Info.Info.ColorNick ?? "#ffffff");
            
            Object resultColorNick = IsGradientColorValue(ColorNickPlayer);
            if (resultColorNick is List<String> gradientColors)
                DisplayName = ApplyGradientToText(DisplayName, gradientColors);
            else DisplayName = $"<color={ColorNickPlayer}>{DisplayName}</color>";
            
            String ChatMessage = GetLang("IQCHAT_CONTEXT_NICK_DISPLAY_MESSAGE", player.UserIDString);
            String ColorChat = !String.IsNullOrWhiteSpace(Info.Info.CustomColorMessage) ? Info.Info.CustomColorMessage : Info.Info.ColorMessage;
            Object resultColorChat = IsGradientColorValue(ColorChat);
            if (resultColorChat is List<String> gradientChatColors)
                ChatMessage = ApplyGradientToText(ChatMessage, gradientChatColors);
            else ChatMessage = $"<color={ColorChat}>{ChatMessage}</color>";
            
            DisplayNick += $"{DisplayName}: <color={ColorChat ?? "#ffffff"}>{ChatMessage}</color>";

            InterfaceVisualNick = InterfaceVisualNick.Replace("%NICK_DISPLAY%", DisplayNick);

            CuiHelper.DestroyUi(player, InterfaceBuilder.UI_Chat_Context_Visual_Nick);
            CuiHelper.AddUi(player, InterfaceVisualNick);
        }

        
                void OnUserPermissionGranted(string id, string permName) => SetupParametres(id, permName);

        Boolean API_CHECK_VOICE_CHAT(BasePlayer.EncryptedValue<UInt64> ID) => API_CHECK_VOICE_CHAT(ID.Get());
        public String GetMessages(BasePlayer player, Dictionary<String, List<String>> LanguageMessages)
        {
            String LangPlayer = _.lang.GetLanguage(player.UserIDString);
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (LanguageMessages.ContainsKey(LangPlayer))
                return LanguageMessages[LangPlayer].GetRandom();
            else if (LanguageMessages.ContainsKey("en"))
                return LanguageMessages["en"].GetRandom();
            else return LanguageMessages.FirstOrDefault().Value.GetRandom();
        }
        public static StringBuilder sb = new StringBuilder();
        private Boolean TryGetUserIdAsUlong(String userId, out UInt64 userIdAsUlong) => UInt64.TryParse(userId, out userIdAsUlong);

        private Boolean SetMuteFakeUser(String idOrName, Boolean isMuted)
        {
            if (!IsReadyIQFakeActive()) return false;
            return IQFakeActive.Call<Boolean>("MuteAction", idOrName, isMuted);
        }
        Int32 API_GET_DEFAULT_SIZE_NICK() => config.ControllerMessages.GeneralSetting.OtherSetting.SizeNick;
		   		 		  						  	   		  		 			  		  		  			 		   			
        private void DiscordLoggMuted(BasePlayer Target, MuteType Type, String Reason = null, String TimeBlocked = null, BasePlayer Moderator = null)
        {
            Configuration.OtherSettings.General MuteChat = config.OtherSetting.LogsMuted;
            if (!MuteChat.UseLogged || String.IsNullOrWhiteSpace(MuteChat.Webhooks)) return;

            Configuration.ControllerMute.LoggedFuncion ControllerMuted = config.ControllerMutes.LoggedMute;

            String ActionReason = String.Empty;

            GeneralInformation.RenameInfo RenameSender = GeneralInfo.GetInfoRename(Target.userID);

            UInt64 UserIDModeration = 0;
            String NickModeration = GetLang("IQCHAT_FUNCED_ALERT_TITLE_SERVER", Target.UserIDString);
            if (Moderator != null)
            {
                GeneralInformation.RenameInfo RenameModerator = GeneralInfo.GetInfoRename(Moderator.userID);

                UserIDModeration = RenameModerator != null
                    ? RenameModerator.RenameID == 0 ? Moderator.userID : RenameModerator.RenameID
                    : Moderator.userID;
                NickModeration = RenameModerator != null
                    ? $"{RenameModerator.RenameNick ?? Moderator.displayName}"
                    : Moderator.displayName;
            }

            String NickTarget = RenameSender != null ? $"{RenameSender.RenameNick ?? Target.displayName}" : Target.displayName;
            UInt64 UserIDTarget = RenameSender != null ? RenameSender.RenameID == 0 ? Target.userID : RenameSender.RenameID : Target.userID;

            List<Fields> fields;

            switch (Type)
            {
                case MuteType.Chat:
                    {
                        if (Reason != null)
                            ActionReason = LanguageEn ? "Mute chat" : "Блокировка чата";
                        else ActionReason = LanguageEn ? "Unmute chat" : "Разблокировка чата";
                        break;
                    }
                case MuteType.Voice:
                    {
                        if (Reason != null)
                            ActionReason = LanguageEn ? "Mute voice" : "Блокировка голоса";
                        else ActionReason = LanguageEn ? "Unmute voice" : "Разблокировка голоса";
                        break;
                    }
                default:
                    break;
            }
            Int32 Color = 0;
            if (Reason != null)
            {
                fields = new List<Fields>
                        {
                            new Fields(LanguageEn ? "Nickname of the moderator" : "Ник модератора", NickModeration, true),
                            new Fields(LanguageEn ? "Steam64ID Moderator" : "Steam64ID модератора", $"{UserIDModeration}", true),
                            new Fields(LanguageEn ? "Action" : "Действие", ActionReason, false),
                            new Fields(LanguageEn ? "Reason" : "Причина", Reason, false),
                            new Fields(LanguageEn ? "Time" : "Время", TimeBlocked, false),
                            new Fields(LanguageEn ? "Nick blocked" : "Ник заблокированного", NickTarget, true),
                            new Fields(LanguageEn ? "Steam64ID blocked" : "Steam64ID заблокированного", $"{UserIDTarget}", true),
                        };


		   		 		  						  	   		  		 			  		  		  			 		   			
                if (ControllerMuted.UseHistoryMessage)
                {
                    String Messages = GetLastMessage(Target, ControllerMuted.CountHistoryMessage);
                    if (Messages != null && !String.IsNullOrWhiteSpace(Messages))
                        fields.Insert(fields.Count, new Fields(LanguageEn ? $"The latter {ControllerMuted.CountHistoryMessage} messages" : $"Последние {ControllerMuted.CountHistoryMessage} сообщений", Messages, false));
                }

                Color = 14357781;
            }
            else
            {
                fields = new List<Fields>
                        {
                            new Fields(LanguageEn ? "Nickname of the moderator" : "Ник модератора", NickModeration, true),
                            new Fields(LanguageEn ? "Steam64ID moderator" : "Steam64ID модератора", $"{UserIDModeration}", true),
                            new Fields(LanguageEn ? "Action" : "Действие", ActionReason, false),
                            new Fields(LanguageEn ? "Nick blocked" : "Ник заблокированного", NickTarget, true),
                            new Fields(LanguageEn ? "Steam64ID blocked" : "Steam64ID заблокированного", $"{UserIDTarget}", true),
                        };
                Color = 1432346;
            }

		   		 		  						  	   		  		 			  		  		  			 		   			
            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, Color, fields, new Authors("IQChat Mute-History", null, "https://i.postimg.cc/SshGgy52/xiwsg5m.png", null), null) });

            Request($"{MuteChat.Webhooks}", newMessage.toJSON());
        }
        private IEnumerable<T> GetPageOfPlayers<T>(IEnumerable<T> items, Int32 page) => items != null ? items.Skip(18 * page).Take(18) : Enumerable.Empty<T>();

        
                public Boolean IsNoob(UInt64 userID, Int32 TimeBlocked)
        {
            if (UserInformationConnection.ContainsKey(userID))
                return UserInformationConnection[userID].IsNoob(TimeBlocked);
            return false;
        }
        void API_SEND_PLAYER_CONNECTED(String DisplayName, String country, String userID) 
        {
            Configuration.ControllerAlert.PlayerSession AlertSessionPlayer = config.ControllerAlertSetting.PlayerSessionSetting;

            if (AlertSessionPlayer.ConnectedAlert)
            {
                String Avatar = AlertSessionPlayer.ConnectedAvatarUse ? userID : String.Empty;
                if (AlertSessionPlayer.ConnectedWorld)
                    ReplyBroadcast(null, Avatar, false, "WELCOME_PLAYER_WORLD", DisplayName, country);
                else ReplyBroadcast(null, Avatar, false, "WELCOME_PLAYER", DisplayName);
            }
        }

        
        
        private String XPrison_GetPrefix(BasePlayer player)
        {
            if (!XPrison || !config.ReferenceSetting.xPrisonSettings.UseXPrison) return String.Empty;
            Boolean isPrisonPlayer = (Boolean)XPrison.CallHook("API_IsOnlinePrisoner", player.userID.Get());
            if (!isPrisonPlayer) return String.Empty;
            String prefixPrison = (String)XPrison.CallHook("API_GetOnlinePrisonerPrefix", player);
            if (String.IsNullOrWhiteSpace(prefixPrison)) return String.Empty;
            String resultPrefix = GetLang("XPRISON_SYNTAX_PREFIX", player.UserIDString, prefixPrison);
            
            Object resultColorMessage = IsGradientColorValue(config.ReferenceSetting.xPrisonSettings.colorTag);
            if (resultColorMessage is List<String> gradientColors)
                resultPrefix = ApplyGradientToText(resultPrefix, gradientColors);
            else resultPrefix = $"<color={config.ReferenceSetting.xPrisonSettings.colorTag}>{resultPrefix}</color>";

            return resultPrefix;
        }
		   		 		  						  	   		  		 			  		  		  			 		   			
        
        private String RemoveLinkText(String text)
        {
            String hrefPattern = "([A-Za-z0-9-А-Яа-я]|https?://)[^ ]+\\.(com|lt|net|org|gg|ru|рф|int|info|ru.com|ru.net|com.ru|net.ru|рус|org.ru|moscow|biz|орг|su)";
            Regex rgx = new Regex(hrefPattern, RegexOptions.IgnoreCase);

            return config.ControllerMessages.Formatting.ControllerNickname.AllowedLinkNick.Contains(rgx.Match(text).Value) ? text : rgx.Replace(text, "").Trim();
        }
        List<String> IQRankListKey(ulong userID) => (List<string>)(IQRankSystem?.Call("API_RANK_USER_KEYS", userID));
        
        void ReplyBroadcast(String CustomPrefix = null, String CustomAvatar = null, Boolean AdminAlert = false, String LangKey = "", params object[] args)
        {
            foreach (BasePlayer p in !AdminAlert ? BasePlayer.activePlayerList.Where(p => UserInformation[p.userID].Settings.TurnBroadcast) : BasePlayer.activePlayerList)
                ReplySystem(p,GetLang(LangKey, p.UserIDString, args), CustomPrefix, CustomAvatar);
        }

		   		 		  						  	   		  		 			  		  		  			 		   			
        
                public class FancyMessage
        {
            public string content { get; set; }
            public bool tts { get; set; }
            public Embeds[] embeds { get; set; }
		   		 		  						  	   		  		 			  		  		  			 		   			
            public class Embeds
            {
                public string title { get; set; }
                public int color { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Authors author { get; set; }

                public Embeds(string title, int color, List<Fields> fields, Authors author, Footer footer)
                {
                    this.title = title;
                    this.color = color;
                    this.fields = fields;
                    this.author = author;
                    this.footer = footer;

                }
            }

            public FancyMessage(string content, bool tts, Embeds[] embeds)
            {
                this.content = content;
                this.tts = tts;
                this.embeds = embeds;
            }
            
            public string toJSON() => JsonConvert.SerializeObject(this);
        }
        public Dictionary<UInt64, AntiNoob> UserInformationConnection = new Dictionary<UInt64, AntiNoob>();
        Boolean API_CHECK_MUTE_CHAT(BasePlayer.EncryptedValue<UInt64> ID) => API_CHECK_MUTE_CHAT(ID.Get());

        [ConsoleCommand("hmute")]
        void HideMuteConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermissionMute)) return;

            if (arg == null || arg.Args == null || arg.Args.Length != 3 || arg.Args.Length > 3)
            {
                ConsoleOrPrintMessage(arg.Player(),
                    LanguageEn
                        ? "Invalid syntax, use : hmute Steam64ID Reason Time (seconds)"
                        : "Неверный синтаксис,используйте : hmute Steam64ID Причина Время(секунды)");
                return;
            }
            string NameOrID = arg.Args[0];
            string Reason = arg.Args[1];
            Int32 TimeMute = 0;
            if (!Int32.TryParse(arg.Args[2], out TimeMute))
            {
                ConsoleOrPrintMessage(arg.Player(),
                    LanguageEn ? "Enter the time in numbers!" : "Введите время цифрами!");
                return;
            }
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (Info.MuteInfo.IsMute(MuteType.Chat))
                        {
                            ConsoleOrPrintMessage(arg.Player(),
                                LanguageEn ? "The player already has a chat lock" : "Игрок уже имеет блокировку чата");
                            return;
                        }

                        Info.MuteInfo.SetMute(MuteType.Chat, TimeMute);

                        ConsoleOrPrintMessage(arg.Player(),
                            LanguageEn ? "Chat blocking issued to offline player" : "Блокировка чата выдана offline-игроку");
                        return;
                    }
                    else
                    {
                        ConsoleOrPrintMessage(arg.Player(),
                            LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    ConsoleOrPrintMessage(arg.Player(),
                        LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }

            MutePlayer(target, MuteType.Chat, 0, arg.Player(), Reason, TimeMute, true, true);
        }

        
        
        private Int32 GetPlayersOnlineShort()
        {
            if (IsReadyIQFakeActive())
            {
                List<FakePlayer> combinedPlayers = GetCombinedPlayerList();
                if (combinedPlayers != null)
                    return GetCombinedPlayerList().Count(fp => !permission.UserHasPermission(fp.userId, PermissionHideOnline));
            }
            return BasePlayer.activePlayerList.Count(p => !permission.UserHasPermission(p.UserIDString, PermissionHideOnline));
        }
        
        [ChatCommand("mutevoice")]
        void MuteVoiceCustomChat(BasePlayer Moderator, string cmd, string[] arg)
        {
            if (!permission.UserHasPermission(Moderator.UserIDString, PermissionMute)) return;
            if (arg == null || arg.Length != 3 || arg.Length > 3)
            {
                ReplySystem(Moderator, LanguageEn ? "Invalid syntax, use : mutevoice Steam64ID/Nick Reason Time(seconds)" : "Неверный синтаксис, используйте : mutevoice Steam64ID/Ник Причина Время(секунды)");
                return;
            }
            string NameOrID = arg[0];
            string Reason = arg[1];
            Int32 TimeMute = 0;
            if (!Int32.TryParse(arg[2], out TimeMute))
            {
                ReplySystem(Moderator, LanguageEn ? "Enter time in numbers!" : "Введите время цифрами!");
                return;
            }
            
            if (IsFakeUser(NameOrID))
            {
                List<FakePlayer> playerList = GetCombinedPlayerList();
                if (playerList != null)
                {
                    FakePlayer fakeUser = playerList.FirstOrDefault(x => x.userId.Equals(NameOrID) || x.displayName.ToLower().Contains(NameOrID.ToLower()));
                    if (fakeUser != null)
                        MutePlayer(null, MuteType.Voice, 0, Moderator, Reason, TimeMute, false, true, fakeUser.userId);
                }
                return;
            }
            
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (Info.MuteInfo.IsMute(MuteType.Voice))
                        {
                            ReplySystem(Moderator, LanguageEn ? "The player already has a voice lock" : "Игрок уже имеет блокировку голосового чата");
                            return;
                        }

                        Info.MuteInfo.SetMute(MuteType.Voice, TimeMute);
                        ReplySystem(Moderator, LanguageEn ? "Voice blocking issued to offline player" : "Блокировка глосового чата выдана offline-игроку");
                        return;
                    }
                    else
                    {
                        ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }
		   		 		  						  	   		  		 			  		  		  			 		   			
            MutePlayer(target, MuteType.Voice, 0, Moderator, Reason, TimeMute, false, true);
        }
        
        private Boolean IsFakeUser(String idOrName)
        {
            if (!IsReadyIQFakeActive()) return false;
            return (Boolean)IQFakeActive.Call("IsFakeUser", idOrName);
        }

        
                public GeneralInformation GeneralInfo = new GeneralInformation();

        
                
        public class FakePlayer
        {
            [JsonProperty("userId")] public String userId;

            public Boolean isMuted;
            [JsonProperty("displayName")] public String displayName;
        }
        void API_ALERT_PLAYER(BasePlayer player, String Message, String CustomPrefix = null, String CustomAvatar = null, String CustomHex = null) => ReplySystem(player, Message, CustomPrefix, CustomAvatar, CustomHex);
        private void DrawUI_IQChat_Mute_Alert_Reasons(BasePlayer player, BasePlayer Target, MuteType Type, UInt64 IDFake = 0)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Mute_Alert_DropList_Title");
            if (Interface == null) return;

            Interface = Interface.Replace("%TITLE%", GetLang("IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_REASON", player.UserIDString));

            CuiHelper.DestroyUi(player, "AlertMuteTitleReason");
            CuiHelper.DestroyUi(player, "PanelMuteReason");
            CuiHelper.AddUi(player, Interface);

            List<Configuration.ControllerMute.Muted> Reasons = Type == MuteType.Chat ? config.ControllerMutes.MuteChatReasons : config.ControllerMutes.MuteVoiceReasons;

            Int32 Y = 0;
            foreach (Configuration.ControllerMute.Muted Reason in Reasons.Take(6))
                DrawUI_IQChat_Mute_Alert_Reasons(player, Target, Reason.Reason, Y++, Type, IDFake);
        }
        internal class FlooderInfo
        {
            public Double Time;
            public String LastMessage;
            public Int32 TryFlood;
        }

        public class Footer
        {
            public string text { get; set; }
            public string icon_url { get; set; }
            public string proxy_icon_url { get; set; }
            public Footer(string text, string icon_url, string proxy_icon_url)
            {
                this.text = text;
                this.icon_url = icon_url;
                this.proxy_icon_url = proxy_icon_url;
            }
        }

        public class Authors
        {
            public string name { get; set; }
            public string url { get; set; }
            public string icon_url { get; set; }
            public string proxy_icon_url { get; set; }
            public Authors(string name, string url, string icon_url, string proxy_icon_url)
            {
                this.name = name;
                this.url = url;
                this.icon_url = icon_url;
                this.proxy_icon_url = proxy_icon_url;
            }
        }
        
        private void DrawUI_IQChat_Mute_And_Ignore(BasePlayer player, SelectedAction Action)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Mute_And_Ignore");
            if (Interface == null) return;
		   		 		  						  	   		  		 			  		  		  			 		   			
            Interface = Interface.Replace("%TITLE%", Action == SelectedAction.Mute ? GetLang("IQCHAT_TITLE_IGNORE_AND_MUTE_MUTED", player.UserIDString) : GetLang("IQCHAT_TITLE_IGNORE_AND_MUTE_IGNORED", player.UserIDString));
            Interface = Interface.Replace("%ACTION_TYPE%", $"{Action}");

            CuiHelper.DestroyUi(player, "MuteAndIgnoredPanel");
            CuiHelper.AddUi(player, Interface);

            DrawUI_IQChat_Mute_And_Ignore_Player_Panel(player, Action);
        }

        
        [ChatCommand("cnick")]
        private void ColoredNickSetup(BasePlayer player, String cmd, String[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUseCmdCnick)) return;
            if (player == null)
                return;

            if (args.Length == 0)
            {
                ReplySystem(player, GetLang("COMMAND_COLOR_CUSTOM_NOT_ARG", player.UserIDString));
                return;
            }

            String color = String.Join("", args);
            List<String> correctedColors = Pool.GetList<String>();

            correctedColors = ConvertStringToList(color);
            if (HasInvalidHexColor(correctedColors, out String invalidHex))
            {
                ReplySystem(player, GetLang("COMMAND_COLOR_CUSTOM_NOT_HEX", player.UserIDString, invalidHex));
                return;
            }

            if (correctedColors.Count > 10)
                ReplySystem(player, GetLang("COMMAND_COLOR_CUSTOM_MAX_ARGUMENTS", player.UserIDString));
            

            User User = UserInformation[player.userID];
            String resultColor = JoinStringList(correctedColors);
            
            User.Info.CustomColorNick = resultColor;
            
            Pool.FreeList(ref correctedColors);
            
            ReplySystem(player, GetLang("COMMAND_COLOR_CUSTOM_ACCESS", player.UserIDString));
        }
        
        private List<FakePlayer> GetCombinedPlayerList()
        {
            if (!IsReadyIQFakeActive()) return null;
            JObject jsonData = IQFakeActive.Call<JObject>("GetListPlayers");
            
            if (!jsonData.TryGetValue("players", out JToken playersToken)) return null;
            List<FakePlayer> playerList = playersToken.ToObject<List<FakePlayer>>();
            return playerList;
        }

        
        private Boolean HasInvalidHexColor(List<String> colorList, out String invalidHex)
        {
            foreach (String color in colorList)
            {
                if (!IsValidHexColor(color))
                {
                    invalidHex = color;
                    return true;
                }
            }
            
            invalidHex = String.Empty;
            return false; 
        }
        private void AddHistoryMessage(BasePlayer player, String Message)
        {
            if (!LastMessagesChat.ContainsKey(player))
                LastMessagesChat.Add(player, new List<String> { Message });
            else LastMessagesChat[player].Add(Message);
        }
        
        
        private String GetClanTag(BasePlayer.EncryptedValue<UInt64> playerID) => GetClanTag(playerID.Get());
        void API_SEND_PLAYER_PM(BasePlayer player, string DisplayName, String userID, string Message)
        {
            if (!UserInformation.ContainsKey(player.userID)) return;
            if (!UInt64.TryParse(userID, out UInt64 idSender)) return;
            if (!UserInformation.ContainsKey(player.userID)) return;
            if (UserInformation[player.userID].Settings.IsIgnored(idSender)) return;
            
            ReplySystem(player, GetLang("COMMAND_PM_SEND_MSG", player.UserIDString, DisplayName, Message));

            if (UserInformation[player.userID].Settings.TurnSound)
                Effect.server.Run(config.ControllerMessages.TurnedFunc.PMSetting.SoundPM, player.GetNetworkPosition());
        }
        private void MigrateDataToNoob()
        {
            if (config.ControllerMessages.TurnedFunc.AntiNoobSetting.AntiNoobPM.AntiNoobActivate || config.ControllerMessages.TurnedFunc.AntiNoobSetting.AntiNoobChat.AntiNoobActivate)
            {
                if (UserInformationConnection.Count == 0 || UserInformationConnection == null)
                {
                    PrintWarning(LanguageEn ? "Migration of old players to Anti-Nub.." : "Миграция старых игроков в Анти-Нуб..");
                    foreach (KeyValuePair<UInt64, User> InfoUser in UserInformation.Where(x => !UserInformationConnection.ContainsKey(x.Key)))
                        UserInformationConnection.Add(InfoUser.Key, new AntiNoob { DateConnection = new DateTime(2022, 1, 1) });
                    PrintWarning(LanguageEn ? "Migration of old players completed" : "Миграция старых игроков завершена");
                }
            }
        }
        private const String PermissionTranslationIgnore = "iqchat.translationignore";
        String IQRankGetRank(ulong userID) => (string)(IQRankSystem?.Call("API_GET_RANK_NAME", userID));

        void OnGroupPermissionRevoked(string name, string perm)
        {
            String[] PlayerGroups = permission.GetUsersInGroup(name);
            if (PlayerGroups == null) return;

            foreach (String playerInfo in PlayerGroups)
            {
                BasePlayer player = BasePlayer.FindByID(UInt64.Parse(playerInfo.Substring(0, 17)));
                if (player == null) return;

                RemoveParametres(player.UserIDString, perm);
            }
        }
        [ConsoleCommand("adminalert")]
        private void AdminAlertConsoleCommand(ConsoleSystem.Arg args)
        {
            BasePlayer Sender = args.Player();
            if (Sender != null)
                if (!permission.UserHasPermission(Sender.UserIDString, PermissionAlert)) return;
            Alert(Sender, args.Args, true);
        }
        public void AnwserMessage(BasePlayer player, String Message)
        {
            Configuration.AnswerMessage Anwser = config.AnswerMessages;
            if (!Anwser.UseAnswer) return;
            foreach (KeyValuePair<String, Configuration.LanguageController> Anwsers in Anwser.AnswerMessageList)
                if (Message.Contains(Anwsers.Key.ToLower()))
                {
                    String templateText = GetMessages(player, Anwsers.Value.LanguageMessages);
                    if(String.IsNullOrWhiteSpace(templateText)) continue;
                    ReplySystem(player, templateText);
                }
        }
        void WriteData()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQChat/Information", GeneralInfo);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQChat/Users", UserInformation);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQChat/AntiNoob", UserInformationConnection);
        }
        void Init()
        {
            ReadData();
        }
        
        
                private void DrawUI_IQChat_Ignore_Alert(BasePlayer player, BasePlayer Target, UInt64 fakeUserId = 0)
        {
            String InterfacePanel = InterfaceBuilder.GetInterface("UI_Chat_Mute_And_Ignore_Alert_Panel");
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Ignore_Alert");
            if (Interface == null || InterfacePanel == null) return;

            Boolean isFake = (IsReadyIQFakeActive() && Target == null && fakeUserId != 0);
            
            GeneralInformation.RenameInfo Renamer = isFake ? null : GeneralInfo.GetInfoRename(Target.userID);
            String NickNamed = isFake ? GetFakeName(fakeUserId.ToString()) : Renamer != null ? $"{Renamer.RenameNick ?? Target.displayName}" : Target.displayName;

            Interface = Interface.Replace("%TITLE%", GetLang(UserInformation[player.userID].Settings.IsIgnored(isFake ? fakeUserId : Target.userID) ? "IQCHAT_TITLE_IGNORE_TITLES_UNLOCK" : "IQCHAT_TITLE_IGNORE_TITLES", player.UserIDString, NickNamed));
            Interface = Interface.Replace("%BUTTON_YES%", GetLang("IQCHAT_TITLE_IGNORE_BUTTON_YES", player.UserIDString));
            Interface = Interface.Replace("%BUTTON_NO%", GetLang("IQCHAT_TITLE_IGNORE_BUTTON_NO", player.UserIDString));
            Interface = Interface.Replace("%COMMAND%", $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Ignore} confirm.yes {(isFake ? fakeUserId : Target.userID)}");

            CuiHelper.DestroyUi(player, "MUTE_AND_IGNORE_PANEL_ALERT");
            CuiHelper.AddUi(player, InterfacePanel);
            CuiHelper.AddUi(player, Interface);
        }

        
        
        private void SetupParametres(String ID, String Permissions)
        {
            UInt64 UserID = UInt64.Parse(ID);
            BasePlayer player = BasePlayer.FindByID(UserID);
            Configuration.ControllerConnection.Turned Controller = config.ControllerConnect.Turneds;
            Configuration.ControllerParameters Parameters = config.ControllerParameter;

            if (!UserInformation.ContainsKey(UserID)) return;
            User Info = UserInformation[UserID];

            if (Controller.TurnAutoSetupPrefix)
            {
                Configuration.ControllerParameters.AdvancedFuncion Prefixes = Parameters.Prefixes.Prefixes.FirstOrDefault(prefix => prefix.Permissions == Permissions);
                if (Prefixes != null)
                {
                    if (Parameters.Prefixes.TurnMultiPrefixes && !Info.Info.PrefixList.Contains(Prefixes.Argument))
                        Info.Info.PrefixList.Add(Prefixes.Argument);
                    else Info.Info.Prefix = Prefixes.Argument;

                    if (player != null && config.ControllerMessages.GeneralSetting.alertArgumentsInfoSetup)
                        ReplySystem(player, GetLang("PREFIX_SETUP", player.UserIDString, Prefixes.Argument));

                    Log(LanguageEn ? $"Player ({UserID}) successfully retrieved the prefix {Prefixes.Argument}" : $"Игрок ({UserID}) успешно забрал префикс {Prefixes.Argument}");
                }
            }
            if (Controller.TurnAutoSetupColorNick)
            {
                Configuration.ControllerParameters.AdvancedFuncion ColorNick = Parameters.NickColorList.FirstOrDefault(nick => nick.Permissions == Permissions);
                if (ColorNick != null)
                {
                    Info.Info.ColorNick = ColorNick.Argument;
                    
                    if (player != null && config.ControllerMessages.GeneralSetting.alertArgumentsInfoSetup)
                    {
                        String nickColorTitle = GetLang("COLOR_NICK_SETUP_TITLE", player.UserIDString); 
                        Object resultColorMessage = IsGradientColorValue(Info.Info.ColorNick);
                        if (resultColorMessage is List<String> gradientColors)
                            nickColorTitle = ApplyGradientToText(nickColorTitle, gradientColors);
                        else nickColorTitle = $"<color={Info.Info.ColorNick}>{nickColorTitle}</color>";
                        
                        ReplySystem(player, GetLang("COLOR_NICK_SETUP", player.UserIDString, nickColorTitle));
                    }

                    Log(LanguageEn
                        ? $"Player ({UserID}) successfully took the color of the nickname {ColorNick.Argument}"
                        : $"Игрок ({UserID}) успешно забрал цвет ника {ColorNick.Argument}");
                }
            }
            if (Controller.TurnAutoSetupColorChat)
            {
                Configuration.ControllerParameters.AdvancedFuncion ColorChat = Parameters.MessageColorList.FirstOrDefault(message => message.Permissions == Permissions);
                if (ColorChat != null)
                {
                    Info.Info.ColorMessage = ColorChat.Argument;
                    
                    if (player != null && config.ControllerMessages.GeneralSetting.alertArgumentsInfoSetup)
                    {
                        String chatColorTitle = GetLang("COLOR_CHAT_SETUP_TITLE", player.UserIDString);
                        Object resultColorMessage = IsGradientColorValue(Info.Info.ColorMessage);
                        if (resultColorMessage is List<String> gradientColors)
                            chatColorTitle = ApplyGradientToText(chatColorTitle, gradientColors);
                        else chatColorTitle = $"<color={Info.Info.ColorMessage}>{chatColorTitle}</color>";
                        
                        ReplySystem(player, GetLang("COLOR_CHAT_SETUP", player.UserIDString, chatColorTitle));
                    }

                    Log(LanguageEn ? $"Player ({UserID}) successfully retrieved the color of the chat {ColorChat.Argument}" : $"Игрок ({UserID}) успешно забрал цвет чата {ColorChat.Argument}");
                }
            }
        }
        [ConsoleCommand("saybro")]
        private void AlertOnlyPlayerConsoleCommand(ConsoleSystem.Arg args)
        {
            BasePlayer Sender = args.Player();
            if (Sender != null)
                if (!permission.UserHasPermission(Sender.UserIDString, PermissionAlert)) return;

            if (args.Args == null || args.Args.Length == 0)
            {
                if (Sender != null)
                    ReplySystem(Sender, LanguageEn ? "You didn't specify a player!" : "Вы не указали игрока!");
                else PrintWarning(LanguageEn ? "You didn't specify a player" : "Вы не указали игрока!");
                return;
            }
            BasePlayer Recipient = BasePlayer.Find(args.Args[0]);
            if (Recipient == null)
            {
                if (Sender != null)
                    ReplySystem(Sender, LanguageEn ? "The player is not on the server!" : "Игрока нет на сервере!");
                else PrintWarning(LanguageEn ? "The player is not on the server!" : "Игрока нет на сервере!");
                return;
            }
            Alert(Sender, Recipient, args.Args.Skip(1).ToArray());
        }

        [ChatCommand("ignore")]
        void IgnorePlayerPM(BasePlayer player, String cmd, String[] arg)
        {
            Configuration.ControllerMessage ControllerMessages = config.ControllerMessages;
            if (!ControllerMessages.TurnedFunc.IgnoreUsePM) return;

            User Info = UserInformation[player.userID];

            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(player, GetLang("INGORE_NOTARG", player.UserIDString));
                return;
            }
            String NameUser = arg[0];
            
            if (IsFakeUser(NameUser))
            {
                var playerList = GetCombinedPlayerList();
                if (playerList != null)
                {
                    var fakeUser = playerList.FirstOrDefault(x => x.displayName.Contains(NameUser));
                    if (fakeUser != null)
                    {
                        if (UInt64.TryParse(fakeUser.userId, out UInt64 userIDFake))
                        {
                            String LangFake = !Info.Settings.IsIgnored(userIDFake)
                                ? GetLang("IGNORE_ON_PLAYER", player.UserIDString, fakeUser.displayName)
                                : GetLang("IGNORE_OFF_PLAYER", player.UserIDString, fakeUser.displayName);
                            ReplySystem(player, LangFake);

                            Info.Settings.IgnoredAddOrRemove(userIDFake);
                        }
                    }
                }
                return;
            }
            
            BasePlayer TargetUser = BasePlayer.Find(NameUser);

            if (TargetUser == null || NameUser == null)
            {
                ReplySystem(player, GetLang("COMMAND_PM_NOT_USER", player.UserIDString));
                return;
            }

            String Lang = !Info.Settings.IsIgnored(TargetUser.userID) ? GetLang("IGNORE_ON_PLAYER", player.UserIDString, TargetUser.displayName) : GetLang("IGNORE_OFF_PLAYER", player.UserIDString, TargetUser.displayName);
            ReplySystem(player, Lang);

            Info.Settings.IgnoredAddOrRemove(TargetUser.userID);
        }
        
        static Double CurrentTime => Facepunch.Math.Epoch.Current;

        public Dictionary<UInt64, FlooderInfo> Flooders = new Dictionary<UInt64, FlooderInfo>();

        private void Request(string url, string payload, Action<int> callback = null)
        {
            Dictionary<string, string> header = new Dictionary<string, string>();
            header.Add("Content-Type", "application/json");
            webrequest.Enqueue(url, payload, (code, response) =>
            {
                if (code != 200 && code != 204)
                {
                    if (response != null)
                    {
                        try
                        {
                            JObject json = JObject.Parse(response);
                            if (code == 429)
                            {
                                float seconds = float.Parse(Math.Ceiling((double)(int)json["retry_after"] / 1000).ToString());
                            }
                            else
                            {
                                PrintWarning($" Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                        }
                        catch
                        {
                            PrintWarning($"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                        }
                    }
                    else
                    {
                        PrintWarning($"Discord didn't respond (down?) Code: {code}");
                    }
                }
                try
                {
                    callback?.Invoke(code);
                }
                catch (Exception ex) { }

            }, this, RequestMethod.POST, header, 10f);
        }
        public Dictionary<UInt64, User> UserInformation = new Dictionary<UInt64, User>();

        void ReplyChat(Chat.ChatChannel channel, BasePlayer player, String OutMessage, String FormatMessage, String FormatPlayer)
        {
            GeneralInformation.RenameInfo RenameInfo = GeneralInfo.GetInfoRename(player.userID);
            UInt64 RenameID = RenameInfo != null ? RenameInfo.RenameID != 0 ? RenameInfo.RenameID : player.userID : player.userID;
            
            if (channel == Chat.ChatChannel.Team)
            {
                if (TranslationAPI && config.ReferenceSetting.translationApiSettings.useTranslationApi && config.ReferenceSetting.translationApiSettings.translateTeamChat)
                    ReplyTranslationMessage(channel, player, player, OutMessage, FormatMessage, FormatPlayer, RenameID);
                else
                {
                    ListHashSet<BasePlayer> playerList = GetPlayerList(player, channel);
                    if (playerList == null) return;
                    foreach (BasePlayer p in playerList)
                        ReplyPlayerChat(channel, p, player, OutMessage, FormatMessage, FormatPlayer, RenameID);
                }
            }
            else
            {
                if (TranslationAPI && config.ReferenceSetting.translationApiSettings.useTranslationApi)
                    ReplyTranslationMessage(channel, player, player, OutMessage, FormatMessage, FormatPlayer, RenameID);
                else
                {
                    foreach (BasePlayer p in BasePlayer.activePlayerList)
                        ReplyPlayerChat(channel, p, player, OutMessage, FormatMessage, FormatPlayer, RenameID);
                }
            }
        }

        private String GetFakeName(String idOrName)
        {
            if (!IsReadyIQFakeActive()) return String.Empty;
            return (String)IQFakeActive.Call("GetFakeName", idOrName);
        }
        void API_SEND_PLAYER_DISCONNECTED(String DisplayName, String reason, String userID) 
        {
            Configuration.ControllerAlert.PlayerSession AlertSessionPlayer = config.ControllerAlertSetting.PlayerSessionSetting;
		   		 		  						  	   		  		 			  		  		  			 		   			
            if (AlertSessionPlayer.DisconnectedAlert)
            {
                String Avatar = AlertSessionPlayer.ConnectedAvatarUse ? userID : String.Empty;

                System.Object[] args = AlertSessionPlayer.DisconnectedReason ? new System.Object[] { DisplayName, reason } : new System.Object[] { DisplayName };
                String Lang = AlertSessionPlayer.DisconnectedReason ? "LEAVE_PLAYER_REASON" : "LEAVE_PLAYER";
                ReplyBroadcast(null, Avatar, false, Lang, args);
            }
        }

        [ChatCommand("pm")]
        void PmChat(BasePlayer Sender, String cmd, String[] arg)
        {
            Configuration.ControllerMessage ControllerMessages = config.ControllerMessages;
            if (!ControllerMessages.TurnedFunc.PMSetting.PMActivate) return;
            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Sender, lang.GetMessage("COMMAND_PM_NOTARG", this, Sender.UserIDString));
                return;
            }

            Configuration.ControllerMessage.TurnedFuncional.AntiNoob.Settings antiNoob = config.ControllerMessages.TurnedFunc.AntiNoobSetting.AntiNoobPM;
            if (antiNoob.AntiNoobActivate)
                if (IsNoob(Sender.userID, antiNoob.TimeBlocked))
                {
                    ReplySystem(Sender, GetLang("IQCHAT_INFO_ANTI_NOOB_PM", Sender.UserIDString, FormatTime(UserInformationConnection[Sender.userID].LeftTime(antiNoob.TimeBlocked), Sender.UserIDString)));
                    return;
                }
            
            User InfoSender = UserInformation[Sender.userID];
            
            if(config.ControllerMutes.mutedPM)
                if (InfoSender.MuteInfo.IsMute(MuteType.Chat))
                {
                    ReplySystem(Sender, GetLang("IQCHAT_FUNCED_NO_SEND_CHAT_MUTED", Sender.UserIDString, FormatTime(InfoSender.MuteInfo.GetTime(MuteType.Chat), Sender.UserIDString)));
                    return;
                }
            
            String NameUser = arg[0];

            if (IsReadyIQFakeActive() && IsFakeUser(NameUser))
            {
                ReplySystem(Sender, GetLang("COMMAND_PM_SUCCESS", Sender.UserIDString, string.Join(" ", arg.ToArray()).Replace(NameUser, ""), NameUser));
                return;
            }
            
            BasePlayer TargetUser = GetPlayerNickOrID(NameUser);
            
            if (TargetUser == null || NameUser == null || !UserInformation.ContainsKey(TargetUser.userID))
            {
                ReplySystem(Sender, GetLang("COMMAND_PM_NOT_USER", Sender.UserIDString));
                return;
            }
            
            User InfoTarget = UserInformation[TargetUser.userID];
            
            if (!InfoTarget.Settings.TurnPM)
            {
                ReplySystem(Sender, GetLang("FUNC_MESSAGE_PM_TURN_FALSE", Sender.UserIDString));
                return;
            }

            if (ControllerMessages.TurnedFunc.IgnoreUsePM)
            {
                if (InfoTarget.Settings.IsIgnored(Sender.userID))
                {
                    ReplySystem(Sender, GetLang("IGNORE_NO_PM", Sender.UserIDString));
                    return;
                }
                if (InfoSender.Settings.IsIgnored(TargetUser.userID))
                {
                    ReplySystem(Sender, GetLang("IGNORE_NO_PM_ME", Sender.UserIDString));
                    return;
                }
            }
            String Message = GetMessageInArgs(Sender, arg.Skip(1).ToArray());

            if (Message == null || Message.Length <= 0)
            {
                ReplySystem(Sender, GetLang("COMMAND_PM_NOT_NULL_MSG", Sender.UserIDString));
                return;
            }
            Message = Message.EscapeRichText();

            if (Message.Length > 125) return;
            
            PMHistory[TargetUser] = Sender;
            PMHistory[Sender] = TargetUser;

            GeneralInformation.RenameInfo RenamerSender = GeneralInfo.GetInfoRename(Sender.userID);
            GeneralInformation.RenameInfo RenamerTarget = GeneralInfo.GetInfoRename(TargetUser.userID);
		   		 		  						  	   		  		 			  		  		  			 		   			
            String DisplayNameSender = RenamerSender != null ? RenamerSender.RenameNick ?? Sender.displayName : Sender.displayName;
            String TargetDisplayName = RenamerTarget != null ? RenamerTarget.RenameNick ?? TargetUser.displayName : TargetUser.displayName;

            if (TranslationAPI && config.ReferenceSetting.translationApiSettings.useTranslationApi && config.ReferenceSetting.translationApiSettings.translatePmChat)
                ReplyTranslationPM(Sender, TargetUser, Message, DisplayNameSender, TargetDisplayName);
            else
            {
                ReplySystem(TargetUser, GetLang("COMMAND_PM_SEND_MSG", TargetUser.UserIDString, DisplayNameSender, Message));
                ReplySystem(Sender, GetLang("COMMAND_PM_SUCCESS", Sender.UserIDString, Message, TargetDisplayName));
            }

            if (InfoTarget.Settings.TurnSound)
                Effect.server.Run(ControllerMessages.TurnedFunc.PMSetting.SoundPM, TargetUser.GetNetworkPosition());

            RustApp?.Call("RA_DirectMessageHandler", Sender.UserIDString, TargetUser.UserIDString, Message);
            
            Log(LanguageEn ? $"PRIVATE MESSAGES : {DisplayNameSender}({Sender.userID}) sent a message to the player - {TargetDisplayName}({TargetUser.userID})\nMESSAGE : {Message}" : $"ЛИЧНЫЕ СООБЩЕНИЯ : {DisplayNameSender}({Sender.userID}) отправил сообщение игроку - {TargetDisplayName}({TargetUser.userID})\nСООБЩЕНИЕ : {Message}");
            DiscordLoggPM(Sender, TargetUser, Message);
		   		 		  						  	   		  		 			  		  		  			 		   			
            RCon.Broadcast(RCon.LogType.Chat, new Chat.ChatEntry
            {
                Message = LanguageEn ? $"PRIVATE MESSAGES : {DisplayNameSender}({Sender.userID}) -> {TargetDisplayName}({TargetUser.userID}) : MESSAGE : {Message}" : $"ЛИЧНЫЕ СООБЩЕНИЯ : {DisplayNameSender}({Sender.userID}) -> {TargetDisplayName}({TargetUser.userID}) : СООБЩЕНИЕ : {Message}",
                UserId = Sender.UserIDString,
                Username = Sender.displayName,
                Channel = Chat.ChatChannel.Global,
                Time = (DateTime.UtcNow.Hour * 3600) + (DateTime.UtcNow.Minute * 60),
                Color = "#3f4bb8",
            });
            PrintWarning(LanguageEn ? $"PRIVATE MESSAGES : {DisplayNameSender}({Sender.userID}) -> {TargetDisplayName}({TargetUser.userID}) : MESSAGE : {Message}" : $"ЛИЧНЫЕ СООБЩЕНИЯ : {DisplayNameSender}({Sender.userID}) -> {TargetDisplayName}({TargetUser.userID}) : СООБЩЕНИЕ : {Message}");
        }
        [ConsoleCommand("alertuip")]
        private void AlertUIPConsoleCommand(ConsoleSystem.Arg args)
        {
            BasePlayer Sender = args.Player();
            if (Sender != null)
                if (!permission.UserHasPermission(Sender.UserIDString, PermissionAlert)) return;
            if (args.Args == null || args.Args.Length == 0)
            {
                if (Sender != null)
                    ReplySystem(Sender, LanguageEn ? "You didn't specify a player!" : "Вы не указали игрока!");
                else PrintWarning(LanguageEn ? "You didn't specify a player!" : "Вы не указали игрока!");
                return;
            }
            BasePlayer Recipient = BasePlayer.Find(args.Args[0]);
            if (Recipient == null)
            {
                if (Sender != null)
                    ReplySystem(Sender, LanguageEn ? "The player is not on the server!" : "Игрока нет на сервере!");
                else PrintWarning(LanguageEn ? "The player is not on the server!" : "Игрока нет на сервере!");
                return;
            }
            AlertUI(Sender, Recipient, args.Args.Skip(1).ToArray());
        }
        [ChatCommand("alertuip")]
        private void AlertUIPChatCommand(BasePlayer Sender, String cmd, String[] args)
        {
            if (!permission.UserHasPermission(Sender.UserIDString, PermissionAlert)) return;
            if (args == null || args.Length == 0)
            {
                ReplySystem(Sender, LanguageEn ? "You didn't specify a player!" : "Вы не указали игрока!");
                return;
            }
            BasePlayer Recipient = BasePlayer.Find(args[0]);
            if (Recipient == null)
            {
                ReplySystem(Sender, LanguageEn ? "The player is not on the server!" : "Игрока нет на сервере!");
                return;
            }
            AlertUI(Sender, Recipient, args.Skip(1).ToArray());
        }

        private void DrawUI_IQChat_Mute_Alert_Reasons(BasePlayer player, BasePlayer Target, String Reason, Int32 Y, MuteType Type, UInt64 IDFake = 0)
        {
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Mute_Alert_DropList_Reason");
            if (Interface == null) return;

            Interface = Interface.Replace("%OFFSET_MIN%", $"-147.5 {85.42 - (Y * 40)}");
            Interface = Interface.Replace("%OFFSET_MAX%", $"147.5 {120.42 - (Y * 40)}");
            Interface = Interface.Replace("%REASON%", Reason);
            Interface = Interface.Replace("%COMMAND_REASON%", $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} confirm.yes {((IsReadyIQFakeActive() && Target == null && IDFake != 0) ? IDFake : Target.userID)} {Type} {Y}");
            CuiHelper.AddUi(player, Interface);
        }
        void API_ALERT_PLAYER_UI(BasePlayer player, String Message) => DrawUI_IQChat_Alert(player, Message);
        private String XLevel_GetPrefix(BasePlayer player)
        {
            if (!XLevels || !config.ReferenceSetting.XLevelsSettings.UseXLevels) return String.Empty;
            String xLevelPrefix = (String)XLevels?.CallHook("API_GetPlayerPrefix", player);
            
            Object resultColorMessage = IsGradientColorValue(config.ReferenceSetting.XLevelsSettings.colorTag);
            if (resultColorMessage is List<String> gradientColors)
                xLevelPrefix = ApplyGradientToText(xLevelPrefix, gradientColors);
            else xLevelPrefix = $"<color={config.ReferenceSetting.XLevelsSettings.colorTag}>{xLevelPrefix}</color>";

            return xLevelPrefix;
        }

        
        
        private bool OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (Interface.Oxide.CallHook("CanChatMessage", player, message) != null) return false;
            
            SeparatorChat(channel, player, message);
            return false;
        }

        [ConsoleCommand("rename")]
        private void ConsoleCommandRename(ConsoleSystem.Arg args)
        {
            BasePlayer Renamer = args.Player();
            if (Renamer == null)
            {
                PrintWarning(LanguageEn ? "You can only use this command while on the server" : "Вы можете использовать эту команду только находясь на сервере");
                return;
            }

            if (!permission.UserHasPermission(Renamer.UserIDString, PermissionRename)) return;
            GeneralInformation General = GeneralInfo;
            if (General == null) return;

            if (args.Args.Length == 0 || args == null)
            {
                ReplySystem(Renamer, lang.GetMessage("COMMAND_RENAME_NOTARG", this, Renamer.UserIDString));
                return;
            }

            String Name = args.Args[0];
            UInt64 ID = Renamer.userID;
            if (args.Args.Length == 2 && args.Args[1] != null && !String.IsNullOrWhiteSpace(args.Args[1]))
                if (!UInt64.TryParse(args.Args[1], out ID))
                {
                    ReplySystem(Renamer, lang.GetMessage("COMMAND_RENAME_NOT_ID", this, Renamer.UserIDString));
                    return;
                }

            if (General.RenameList.ContainsKey(Renamer.userID))
            {
                General.RenameList[Renamer.userID].RenameNick = Name;
                General.RenameList[Renamer.userID].RenameID = ID;
            }
            else General.RenameList.Add(Renamer.userID, new GeneralInformation.RenameInfo { RenameNick = Name, RenameID = ID });

            ReplySystem(Renamer, GetLang("COMMAND_RENAME_SUCCES", Renamer.UserIDString, Name, ID));
            RenameUpdate(Renamer, Name);
        }
        Boolean API_CHECK_MUTE_CHAT(UInt64 ID)
        {
            if (!UserInformation.ContainsKey(ID)) return false;
            return UserInformation[ID].MuteInfo.IsMute(MuteType.Chat);
        }
        private const String PermissionMutedAdmin = "iqchat.adminmuted";
        private void OnUserConnected(IPlayer player) => ControlledBadNick(player);
		   		 		  						  	   		  		 			  		  		  			 		   			
        [ChatCommand("mute")]
        void MuteCustomChat(BasePlayer Moderator, string cmd, string[] arg)
        {
            if (!permission.UserHasPermission(Moderator.UserIDString, PermissionMute)) return;
            if (arg == null || arg.Length != 3 || arg.Length > 3)
            {
                ReplySystem(Moderator, LanguageEn ? "Invalid syntax, use : mute Steam64ID/Nick Reason Time(seconds)" : "Неверный синтаксис, используйте : mute Steam64ID/Ник Причина Время(секунды)");
                return;
            }
            string NameOrID = arg[0];
            string Reason = arg[1];
            Int32 TimeMute = 0;
            if (!Int32.TryParse(arg[2], out TimeMute))
            {
                ReplySystem(Moderator, LanguageEn ? "Enter time in numbers!" : "Введите время цифрами!");
                return;
            }
            
            if (IsFakeUser(NameOrID))
            {
                List<FakePlayer> playerList = GetCombinedPlayerList();
                if (playerList != null)
                {
                    FakePlayer fakeUser = playerList.FirstOrDefault(x => x.userId.Equals(NameOrID) || x.displayName.ToLower().Contains(NameOrID.ToLower()));
                    if (fakeUser != null)
                        MutePlayer(null, MuteType.Chat, 0, Moderator, Reason, TimeMute, false, true, fakeUser.userId);
                }
                return;
            }
            
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (Info.MuteInfo.IsMute(MuteType.Chat))
                        {
                            ReplySystem(Moderator, LanguageEn ? "The player already has a chat lock" : "Игрок уже имеет блокировку чата");
                            return;
                        }

                        Info.MuteInfo.SetMute(MuteType.Chat, TimeMute);
                        ReplySystem(Moderator, LanguageEn ? "Chat blocking issued to offline player" : "Блокировка чата выдана offline-игроку");
                        return;
                    }
                    else
                    {
                        ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }
		   		 		  						  	   		  		 			  		  		  			 		   			
            MutePlayer(target, MuteType.Chat, 0, Moderator, Reason, TimeMute, false, true);
        }
        private ListHashSet<BasePlayer> GetPlayerList(BasePlayer player, Chat.ChatChannel channel)
        {
            ListHashSet<BasePlayer> playerList = new();

            if (channel is Chat.ChatChannel.Global or Chat.ChatChannel.Local)
                playerList = BasePlayer.activePlayerList;
            else if (channel == Chat.ChatChannel.Team)
            {
                RelationshipManager.PlayerTeam Team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                if (Team == null) return null;
                foreach (UInt64 FindPlayers in Team.members)
                {
                    BasePlayer TeamPlayer = BasePlayer.FindByID(FindPlayers);
                    if (TeamPlayer == null) continue;
		   		 		  						  	   		  		 			  		  		  			 		   			
                    playerList.Add(TeamPlayer);
                }
            }
            else if (channel == Chat.ChatChannel.Cards)
            {
                if (!player.isMounted)
                    return null;

                CardTable cardTable = player.GetMountedVehicle() as CardTable;
                if (cardTable == null || !cardTable.GameController.PlayerIsInGame(player))
                    return null;

                List<Network.Connection> PlayersCards = new List<Network.Connection>();
                cardTable.GameController.GetConnectionsInGame(PlayersCards);
                if (PlayersCards == null || PlayersCards.Count == 0)
                    return null;

                foreach (Network.Connection PCard in PlayersCards)
                {
                    BasePlayer PlayerInRound = BasePlayer.FindByID(PCard.userid);
                    if (PlayerInRound == null) return null;
                    playerList.Add(PlayerInRound);
                }
            }

            return playerList;
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        Int32 API_GET_DEFAULT_SIZE_MESSAGE() => config.ControllerMessages.GeneralSetting.OtherSetting.SizeMessage;
        private void DrawUI_IQChat_Context_AdminAndModeration(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionMute)) return;

            String InterfaceModeration = InterfaceBuilder.GetInterface("UI_Chat_Moderation");
            if (InterfaceModeration == null) return;

            InterfaceModeration = InterfaceModeration.Replace("%TITLE%", GetLang("IQCHAT_TITLE_MODERATION_PANEL", player.UserIDString));
            InterfaceModeration = InterfaceModeration.Replace("%COMMAND_MUTE_MENU%", $"newui.cmd action.mute.ignore open {SelectedAction.Mute}");
            InterfaceModeration = InterfaceModeration.Replace("%TEXT_MUTE_MENU%", GetLang("IQCHAT_BUTTON_MODERATION_MUTE_MENU", player.UserIDString));

            CuiHelper.AddUi(player, InterfaceModeration);

            DrawUI_IQChat_Update_MuteChat_All(player);
            DrawUI_IQChat_Update_MuteVoice_All(player);
        }

        private void AlertDisconnected(BasePlayer player, String reason)
        {
            Object status = Interface.CallHook("OnChatAlertDisconnected", player, reason);
            if (status != null)
                return;
            
            Configuration.ControllerAlert.AdminSession AlertSessionAdmin = config.ControllerAlertSetting.AdminSessionSetting;
            Configuration.ControllerAlert.PlayerSession AlertSessionPlayer = config.ControllerAlertSetting.PlayerSessionSetting;
            GeneralInformation.RenameInfo RenameInformation = GeneralInfo.GetInfoRename(player.userID);

            if (AlertSessionPlayer.DisconnectedAlert)
            {
                if (!AlertSessionAdmin.DisconnectedAlertAdmin)
                    if (player.IsAdmin) return;

                String DisplayName = player.displayName;

                UInt64 UserID = player.userID;
                if (RenameInformation != null)
                {
                    DisplayName = RenameInformation.RenameNick;
                    UserID = RenameInformation.RenameID;
                }

                String Avatar = AlertSessionPlayer.ConnectedAvatarUse ? UserID.ToString() : String.Empty;

                if (!permission.UserHasPermission(player.UserIDString, PermissionHideDisconnection))
                {
                    if (AlertSessionPlayer.DisconnectedAlertRandom)
                        ReplyBroadcast(null, Avatar, false, AlertSessionPlayer.RandomDisconnectedAlert.LanguageMessages,DisplayName, reason);
                    else
                    {
                        System.Object[] args = AlertSessionPlayer.DisconnectedReason ? new System.Object[] { DisplayName, reason } : new System.Object[] { DisplayName };
                        String Lang = AlertSessionPlayer.DisconnectedReason ? "LEAVE_PLAYER_REASON" : "LEAVE_PLAYER";
                        ReplyBroadcast(null, Avatar, false, Lang, args);
                    }
                }

                Log($"[{player.userID}] {(AlertSessionPlayer.DisconnectedReason ? GetLang("LEAVE_PLAYER_REASON", player.UserIDString, DisplayName, reason) : GetLang("LEAVE_PLAYER", player.UserIDString, DisplayName))}");
            }
        }
        void OnUserPermissionRevoked(string id, string permName) => RemoveParametres(id, permName);

        [ChatCommand("hunmute")]
        void HideUnMute(BasePlayer Moderator, string cmd, string[] arg)
        {
            if (!permission.UserHasPermission(Moderator.UserIDString, PermissionMute)) return;
            if (arg == null || arg.Length != 1 || arg.Length > 1)
            {
                ReplySystem(Moderator, LanguageEn ? "Invalid syntax, please use : hunmute Steam64ID/Nick" : "Неверный синтаксис,используйте : hunmute Steam64ID/Ник");
                return;
            }
            string NameOrID = arg[0];
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (!Info.MuteInfo.IsMute(MuteType.Chat))
                        {
                            ReplySystem(Moderator, LanguageEn ? "The player does not have a chat lock" : "У игрока нет блокировки чата");
                            return;
                        }

                        if (!Moderator.IsAdmin && Moderator.userID == Steam64ID)
                        {
                            ReplySystem(Moderator, GetLang("FUNC_MESSAGE_UNMUTE_YOURSELF", Moderator.UserIDString));
                            return;
                        }
                        
                        Info.MuteInfo.UnMute(MuteType.Chat);
                        ReplySystem(Moderator, LanguageEn ? "You have unblocked the offline chat to the player" : "Вы разблокировали чат offline игроку");
                        return;
                    }
                    else
                    {
                        ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    ReplySystem(Moderator, LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }

            UnmutePlayer(target, MuteType.Chat, Moderator, true, true);
        }

        public class GeneralInformation
        {
            public Boolean TurnMuteAllChat;
            public Boolean TurnMuteAllVoice;

            public Dictionary<UInt64, RenameInfo> RenameList = new Dictionary<UInt64, RenameInfo>();
            internal class RenameInfo
            {
                public String RenameNick;
                public UInt64 RenameID;
            }
            
            public RenameInfo GetInfoRename(UInt64 UserID)
            {
                if (!RenameList.ContainsKey(UserID)) return null;
                return RenameList[UserID];
            }
        }
        private Dictionary<BasePlayer, InformationOpenedUI> LocalBase = new Dictionary<BasePlayer, InformationOpenedUI>();
        
                private void DrawUI_IQChat_Mute_Alert(BasePlayer player, BasePlayer Target, UInt64 IDFake = 0)
        {
            String InterfacePanel = InterfaceBuilder.GetInterface("UI_Chat_Mute_And_Ignore_Alert_Panel");
            String Interface = InterfaceBuilder.GetInterface("UI_Chat_Mute_Alert");
            if (Interface == null || InterfacePanel == null) return;

            Boolean isFake = (IsReadyIQFakeActive() && Target == null && IDFake != 0);
            User InfoTarget = isFake ? null : UserInformation[Target.userID];
            FakePlayer fakePlayer = GetCombinedPlayerList() == null ? null : GetCombinedPlayerList().FirstOrDefault(x => x.userId == IDFake.ToString());
            if (isFake && fakePlayer == null)
                return;
            
            Interface = Interface.Replace("%TITLE%", GetLang("IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT", player.UserIDString));
            Interface = Interface.Replace("%BUTTON_TAKE_CHAT_ACTION%", (isFake && fakePlayer is { isMuted: false }) ? GetLang("IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_CHAT", player.UserIDString) : (isFake && fakePlayer is { isMuted: true }) ? GetLang("IQCHAT_BUTTON_MODERATION_UNMUTE_MENU_TITLE_ALERT_CHAT", player.UserIDString) : InfoTarget == null ? GetLang("IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_CHAT", player.UserIDString) : InfoTarget.MuteInfo.IsMute(MuteType.Chat) ? GetLang("IQCHAT_BUTTON_MODERATION_UNMUTE_MENU_TITLE_ALERT_CHAT", player.UserIDString) : GetLang("IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_CHAT", player.UserIDString));
            Interface = Interface.Replace("%BUTTON_TAKE_VOICE_ACTION%", (isFake && fakePlayer is { isMuted: false }) ? GetLang("IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_VOICE", player.UserIDString) : (isFake && fakePlayer is { isMuted: true }) ? GetLang("IQCHAT_BUTTON_MODERATION_UNMUTE_MENU_TITLE_ALERT_VOICE", player.UserIDString) : InfoTarget == null ? GetLang("IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_VOICE", player.UserIDString) : InfoTarget.MuteInfo.IsMute(MuteType.Voice) ? GetLang("IQCHAT_BUTTON_MODERATION_UNMUTE_MENU_TITLE_ALERT_VOICE", player.UserIDString) : GetLang("IQCHAT_BUTTON_MODERATION_MUTE_MENU_TITLE_ALERT_VOICE", player.UserIDString));
            Interface = Interface.Replace("%COMMAND_TAKE_ACTION_MUTE_CHAT%", (isFake && fakePlayer is { isMuted: false }) ? $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} open.reason.mute {IDFake} {MuteType.Chat}" : (isFake && fakePlayer is { isMuted: true }) ? $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} unmute.yes {fakePlayer.userId} {MuteType.Chat}" : InfoTarget == null ? $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} open.reason.mute {IDFake} {MuteType.Chat}" : InfoTarget.MuteInfo.IsMute(MuteType.Chat) ? $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} unmute.yes {Target.UserIDString} {MuteType.Chat}" : $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} open.reason.mute {Target.UserIDString} {MuteType.Chat}");
            Interface = Interface.Replace("%COMMAND_TAKE_ACTION_MUTE_VOICE%", (isFake && fakePlayer is { isMuted: false }) ? $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} open.reason.mute {IDFake} {MuteType.Voice}" : (isFake && fakePlayer is { isMuted: true }) ? $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} unmute.yes {fakePlayer.userId} {MuteType.Voice}" : InfoTarget == null ? $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} open.reason.mute {IDFake} {MuteType.Voice}" : InfoTarget.MuteInfo.IsMute(MuteType.Voice) ? $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} unmute.yes {Target.UserIDString} {MuteType.Voice}" : $"newui.cmd action.mute.ignore ignore.and.mute.controller {SelectedAction.Mute} open.reason.mute {Target.UserIDString} {MuteType.Voice}");
		   		 		  						  	   		  		 			  		  		  			 		   			
            CuiHelper.DestroyUi(player, "MUTE_AND_IGNORE_PANEL_ALERT");
            CuiHelper.AddUi(player, InterfacePanel);
            CuiHelper.AddUi(player, Interface);
        }
        public class User
        {
            public Information Info = new Information();
            public Setting Settings = new Setting();
            public Mute MuteInfo = new Mute();
            internal class Information
            {
                public String Prefix;
                public String ColorNick;
                public String ColorMessage;
                public String Rank;
                public String CustomColorNick;
                public String CustomColorMessage;

                public List<String> PrefixList = new List<String>();
            }

            internal class Setting
            {
                public Boolean TurnPM = true;
                public Boolean TurnAlert = true;
                public Boolean TurnBroadcast = true;
                public Boolean TurnSound = true;

                public List<UInt64> IgnoreUsers = new List<UInt64>();

                public Boolean IsIgnored(UInt64 TargetID) => IgnoreUsers.Contains(TargetID);
                public void IgnoredAddOrRemove(UInt64 TargetID)
                {
                    if (IsIgnored(TargetID))
                        IgnoreUsers.Remove(TargetID);
                    else IgnoreUsers.Add(TargetID);
                }
            }

            internal class Mute
            {
                public Double TimeMuteChat;
                public Double TimeMuteVoice;

                public Double GetTime(MuteType Type)
                {
                    Double TimeMuted = 0;
                    switch (Type)
                    {
                        case MuteType.Chat:
                            TimeMuted = TimeMuteChat - CurrentTime;
                            break;
                        case MuteType.Voice:
                            TimeMuted = TimeMuteVoice - CurrentTime;
                            break;
                        default:
                            break;
                    }
                    return TimeMuted;
                }
                public void SetMute(MuteType Type, Int32 Time)
                {
                    switch (Type)
                    {
                        case MuteType.Chat:
                            TimeMuteChat = Time + CurrentTime;
                            break;
                        case MuteType.Voice:
                            TimeMuteVoice = Time + CurrentTime;
                            break;
                        default:
                            break;
                    }
                }
                public void UnMute(MuteType Type)
                {
                    switch (Type)
                    {
                        case MuteType.Chat:
                            TimeMuteChat = 0;
                            break;
                        case MuteType.Voice:
                            TimeMuteVoice = 0;
                            break;
                        default:
                            break;
                    }
                }
                public Boolean IsMute(MuteType Type) => GetTime(Type) > 0;
            }
        }

        private void RenameUpdate(BasePlayer Renamer, String NewName)
        {
            Renamer.displayName = NewName;
            Renamer.IPlayer?.Rename(NewName);
        }

        public Boolean IsReadyIQFakeActive()
        {
            if (IQFakeActive != null && config.ReferenceSetting.IQFakeActiveSettings.UseIQFakeActive)
                return IQFakeActive.Call<Boolean>("IsReady");

            return false;
        }
        
        [ConsoleCommand("mute")]
        void MuteCustomAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermissionMute)) return;
            if (arg == null || arg.Args == null || arg.Args.Length != 3 || arg.Args.Length > 3)
            {
                PrintWarning(LanguageEn ? "Invalid syntax, use : mute Steam64ID/Nick Reason Time(seconds)" : "Неверный синтаксис,используйте : mute Steam64ID/Ник Причина Время(секунды)");
                return;
            }
            string NameOrID = arg.Args[0];
            string Reason = arg.Args[1];
            Int32 TimeMute = 0;
            if (!Int32.TryParse(arg.Args[2], out TimeMute))
            {
                PrintWarning(LanguageEn ? "Enter time in numbers!" : "Введите время цифрами!");
                return;
            }
            
            BasePlayer target = GetPlayerNickOrID(NameOrID);
            if (target == null)
            {
                UInt64 Steam64ID = 0;
                if (UInt64.TryParse(NameOrID, out Steam64ID))
                {
                    if (UserInformation.ContainsKey(Steam64ID))
                    {
                        User Info = UserInformation[Steam64ID];
                        if (Info == null) return;
                        if (Info.MuteInfo.IsMute(MuteType.Chat))
                        {
                            PrintWarning(LanguageEn ? "The player already has a chat lock" : "Игрок уже имеет блокировку чата");
                            return;
                        }

                        Info.MuteInfo.SetMute(MuteType.Chat, TimeMute);
                        PrintWarning(LanguageEn ? "Chat blocking issued to offline player" : "Блокировка чата выдана offline-игроку");
                        return;
                    }
                    else
                    {
                        PrintWarning(LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                }
                else
                {
                    PrintWarning(LanguageEn ? "This player is not on the server" : "Такого игрока нет на сервере");
                    return;
                }
            }

            MutePlayer(target, MuteType.Chat, 0, arg.Player(), Reason, TimeMute, false, true);
            Puts(LanguageEn ? "Successfully" : "Успешно");
        }
        private class InformationOpenedUI
        {
            public List<Configuration.ControllerParameters.AdvancedFuncion> ElementsPrefix;
            public List<Configuration.ControllerParameters.AdvancedFuncion> ElementsNick;
            public List<Configuration.ControllerParameters.AdvancedFuncion> ElementsChat;
            public List<Configuration.ControllerParameters.AdvancedFuncion> ElementsRanks;
            public Int32 SlideIndexPrefix = 0;
            public Int32 SlideIndexNick = 0;
            public Int32 SlideIndexChat = 0;
            public Int32 SlideIndexRank = 0;
        }
        
        
        
        String IQRankGetRank(BasePlayer.EncryptedValue<UInt64> userID) => IQRankGetRank(userID.Get());


        
        
                
        
        private class ImageUI
        {
            private const String _path = "IQSystem/IQChat/Images/";
            private const String _printPath = "data/" + _path;
            private readonly Dictionary<String, ImageData> _images = new()
            {
                { "UI_IQCHAT_CONTEXT_NO_RANK", new ImageData() },
                { "UI_IQCHAT_CONTEXT_RANK", new ImageData() },
                { "IQCHAT_INFORMATION_ICON", new ImageData() },
                { "IQCHAT_SETTING_ICON", new ImageData() },
                { "IQCHAT_IGNORE_INFO_ICON", new ImageData() },
                { "IQCHAT_MODERATION_ICON", new ImageData() },
                { "IQCHAT_ELEMENT_PANEL_ICON", new ImageData() },
                { "IQCHAT_ELEMENT_PREFIX_MULTI_TAKE_ICON", new ImageData() },
                { "IQCHAT_ELEMENT_SLIDER_ICON", new ImageData() },
                { "IQCHAT_ELEMENT_SLIDER_LEFT_ICON", new ImageData() },
                { "IQCHAT_ELEMENT_SLIDER_RIGHT_ICON", new ImageData() },
                { "IQCHAT_ELEMENT_DROP_LIST_OPEN_ICON", new ImageData() },
                { "IQCHAT_ELEMENT_DROP_LIST_OPEN_ARGUMENT_ICON", new ImageData() },
                { "IQCHAT_ELEMENT_DROP_LIST_OPEN_TAKED", new ImageData() },
                { "IQCHAT_ELEMENT_SETTING_CHECK_BOX", new ImageData() },
                { "IQCHAT_ALERT_PANEL", new ImageData() },
                { "IQCHAT_MUTE_AND_IGNORE_PANEL", new ImageData() },
                { "IQCHAT_MUTE_AND_IGNORE_ICON", new ImageData() },
                { "IQCHAT_MUTE_AND_IGNORE_SEARCH", new ImageData() },
                { "IQCHAT_MUTE_AND_IGNORE_PAGE_PANEL", new ImageData() },
                { "IQCHAT_MUTE_AND_IGNORE_PLAYER", new ImageData() },
                { "IQCHAT_MUTE_AND_IGNORE_PLAYER_STATUS", new ImageData() },
                { "IQCHAT_IGNORE_ALERT_PANEL", new ImageData() },
                { "IQCHAT_IGNORE_ALERT_ICON", new ImageData() },
                { "IQCHAT_IGNORE_ALERT_BUTTON_YES", new ImageData() },
                { "IQCHAT_IGNORE_ALERT_BUTTON_NO", new ImageData() },
                { "IQCHAT_MUTE_ALERT_PANEL", new ImageData() },
                { "IQCHAT_MUTE_ALERT_ICON", new ImageData() },
                { "IQCHAT_MUTE_ALERT_PANEL_REASON", new ImageData() },
              
            };

            private enum ImageStatus
            {
                NotLoaded,
                Loaded,
                Failed
            }

            private class ImageData
            {
                public ImageStatus Status = ImageStatus.NotLoaded;
                public string Id { get; set; }
            }

            public string GetImage(string name)
            {
                ImageData image;
                if (_images.TryGetValue(name, out image) && image.Status == ImageStatus.Loaded)
                    return image.Id;
                return null;
            }

            public void DownloadImage()
            {
                KeyValuePair<string, ImageData>? image = null;
                foreach (KeyValuePair<string, ImageData> img in _images)
                {
                    if (img.Value.Status == ImageStatus.NotLoaded)
                    {
                        image = img;
                        break;
                    }
                }

                if (image != null)
                {
                    ServerMgr.Instance.StartCoroutine(ProcessDownloadImage(image.Value));
                }
                else
                {
                    List<String> failedImages = new List<string>();
		   		 		  						  	   		  		 			  		  		  			 		   			
                    foreach (KeyValuePair<String, ImageData> img in _images)
                    {
                        if (img.Value.Status == ImageStatus.Failed)
                        {
                            failedImages.Add(img.Key);
                        }
                    }

                    if (failedImages.Count > 0)
                    {
                        String images = String.Join(", ", failedImages);
                        _.PrintError(LanguageEn
                            ? $"Failed to load the following images: {images}. Perhaps you did not upload them to the '{_printPath}' folder.\nDownloaded image - https://drive.google.com/drive/folders/1duFZ6jOjGGwY4Rni-GFD0doviOQiOKMi?usp=sharing"
                            : $"Не удалось загрузить следующие изображения: {images}. Возможно, вы не загрузили их в папку '{_printPath}'.\nСкачать можно тут - https://drive.google.com/drive/folders/1duFZ6jOjGGwY4Rni-GFD0doviOQiOKMi?usp=sharing");
                        Interface.Oxide.UnloadPlugin(_.Name);
                    }
                    else
                    {
                        _.Puts(LanguageEn
                            ? $"{_images.Count} images downloaded successfully!"
                            : $"{_images.Count} изображений успешно загружено!");
                        
                        _interface = new InterfaceBuilder();
                    }
                }
            }
            
            public void UnloadImages()
            {
                foreach (KeyValuePair<string, ImageData> item in _images)
                    if(item.Value.Status == ImageStatus.Loaded)
                        if (item.Value?.Id != null)
                            FileStorage.server.Remove(uint.Parse(item.Value.Id), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);

                _images?.Clear();
            }

            private IEnumerator ProcessDownloadImage(KeyValuePair<string, ImageData> image)
            {
                string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + _path + image.Key + ".png";

                using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
                {
                    yield return www.SendWebRequest();
		   		 		  						  	   		  		 			  		  		  			 		   			
                    if (www.isNetworkError || www.isHttpError)
                    {
                        image.Value.Status = ImageStatus.Failed;
                    }
                    else
                    {
                        Texture2D tex = DownloadHandlerTexture.GetContent(www);
                        image.Value.Id = FileStorage.server.Store(tex.EncodeToPNG(), FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                        image.Value.Status = ImageStatus.Loaded;
                        UnityEngine.Object.DestroyImmediate(tex);
                    }

                    DownloadImage();
                }
            }
        }
        void OnPlayerConnected(BasePlayer player)
        {
            UserConnecteionData(player);
            AlertController(player);
        }

        private String GetPlayerFormat(String displayName, String userId)
        {
            if (!UInt64.TryParse(userId, out UInt64 userID)) return $"<color=54aafe>{displayName}</color>";
            
            GeneralInformation.RenameInfo Renamer = GeneralInfo.GetInfoRename(userID);
            String NickNamed = Renamer != null ? $"{Renamer.RenameNick ?? displayName}" : displayName;
		   		 		  						  	   		  		 			  		  		  			 		   			
            User Info = UserInformation[userID];
            Configuration.ControllerParameters ControllerParameter = config.ControllerParameter;

            String Prefixes = String.Empty;
            String ColorNickPlayer = String.IsNullOrWhiteSpace(Info.Info.ColorNick) ? "#54aafe" : Info.Info.ColorNick;

            String DisplayName = NickNamed;
            
            Object resultColorNick = IsGradientColorValue(ColorNickPlayer);
            if (resultColorNick is List<String> gradientColors)
                DisplayName = ApplyGradientToText(DisplayName, gradientColors);
            else DisplayName = $"<color={ColorNickPlayer}>{DisplayName}</color>";
            
            if (ControllerParameter.Prefixes.TurnMultiPrefixes)
            {
                if (Info.Info.PrefixList != null)
                    Prefixes = String.Join("", Info.Info.PrefixList.Take(ControllerParameter.Prefixes.MaximumMultiPrefixCount));
            }
            else Prefixes = Info.Info.Prefix;

            String ResultName = $"{Prefixes}{DisplayName}";

            return ResultName;
        }
        private void DrawUI_IQChat_Update_MuteChat_All(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionMutedAdmin)) return;

            String InterfaceAdministratorChat = InterfaceBuilder.GetInterface("UI_Chat_Administation_AllChat");
            if (InterfaceAdministratorChat == null) return;
		   		 		  						  	   		  		 			  		  		  			 		   			
            InterfaceAdministratorChat = InterfaceAdministratorChat.Replace("%TEXT_MUTE_ALLCHAT%", GetLang(!GeneralInfo.TurnMuteAllChat ? "IQCHAT_BUTTON_MODERATION_MUTE_ALL_CHAT" : "IQCHAT_BUTTON_MODERATION_UNMUTE_ALL_CHAT", player.UserIDString));
            InterfaceAdministratorChat = InterfaceAdministratorChat.Replace("%COMMAND_MUTE_ALLCHAT%", $"newui.cmd action.mute.ignore mute.controller {SelectedAction.Mute} mute.all.chat");

            CuiHelper.DestroyUi(player, "ModeratorMuteAllChat");
            CuiHelper.AddUi(player, InterfaceAdministratorChat);
        }

        
        
        private void ConsoleOrPrintMessage(BasePlayer player, String Messages)
        {
            if (player != null)
                player.ConsoleMessage(Messages);
            else PrintWarning(Messages);
        }
		   		 		  						  	   		  		 			  		  		  			 		   			
            }
}

