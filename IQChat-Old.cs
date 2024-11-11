using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CompanionServer;
using ConVar;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("IQChat", "Mercury", "0.3.9")]
    [Description("Самый приятный чат для вашего сервера из ветки IQ")]
    class IQChat : RustPlugin
    {
        /// <summary>
        /// Обновление 0.3.6
        /// - Изменил настройки пользователя по умолчанию включенными
        /// - Корректировка UI в дроп листе
        /// - Поправил исчезновение стрелочек в сладйерах при растягивании экрана 
        /// - Добавил возможность выбора тип UI для каждого элемента , слайдер или дроп.лист
        /// - Добавил страницы в дроп.листы 
        /// - Добавил возможность настраиввать расположение UI через конфиг (для опытных пользователей)
        /// - Добавлены комнды на ручную блокировку чата (консольная и чат) : (/)mute Steam64ID Причина Время(секунды)
        /// - Добавлены комнды на ручную разблолкирку чата (консольная и чат) : (/)unmute Steam64ID
        /// - Добавлены команды на скрытую блокировку чата, уведомления о блокировке будет видеть только игрок и модератор (консольная и чат) : (/)hmute Steam64ID Причина Время(секунды)
        /// - Добавлены команды на скрытую разблолкирку чата, уведомления о разблокировке будет видеть только игрок и модератор (консольная и чат) : (/)hunmute Steam64ID
        /// - Добавлена консольная команда migrate , которая переведет настройки ВСЕХ игроков в состояние ВКЛЮЧЕНО
        /// - Поправки в совместной работе в IQModalMenu (вскоре выйдет)
        /// - Добавлена проерка на префиксы, теперь если вы удалили префикс, у игрока он так-же удалится
        /// Обновление 0.3.7
        /// - Поправлены страницы в дроп.листе для IQModalMenu
        /// - Поправлена встраиваемая панель с чатом в IQModalMenu
        /// - Вернул чатовую команду /mute (Случайно была задета и удалена)
        /// - Корректировки API : API_SEND_PLAYER - добавлен вывод форматированного сообщения в консоль
        /// Обновление 0.3.8
        /// - Исправлен чат после обновления
        /// - Добавлены команды для чата и консоли :
        /// - (/)adminalert Сообщение - высылает всем оповещение игнорируя то, что у игрока выключен показ оповещений
        /// - Исправлено отображение аватарки пользователя, если выключен вход с информацией о стране
        /// - Добавлена возможность скрыть оповоещение о входе администратора
        /// - Добавлена возможность скрыть оповоещение о выходе администратора
        /// - Поправил синтаксис команде saybro , теперь можно использовать ник и Steam64ID 
        /// - Поправил синтаксис команде alertuip , теперь можно использовать ник и Steam64ID 
        /// /// </summary>

        #region Reference
        [PluginReference] Plugin IQPersonal, IQFakeActive, XDNotifications, IQRankSystem, IQModalMenu;

        #region IQModalMenu
        bool IQModalMenuConnected() => (bool)IQModalMenu.Call("API_Connected_Plugin", this, config.ReferenceSetting.IQModalMenuSettings.Avatar, config.ReferenceSetting.IQModalMenuSettings.Sprite, "iq.chat.modal");
        bool IQModalMenuDisconnected() => (bool)IQModalMenu.Call("API_Disconnected_Plugin", this);
        void IQModalSend(BasePlayer player) => IQModalMenu.Call("API_Send_Menu", player, this);
        #endregion

        #region IQPersonal
        public void SetMute(BasePlayer player) => IQPersonal?.CallHook("API_SET_MUTE", player.userID);
        public void BadWords(BasePlayer player) => IQPersonal?.CallHook("API_DETECTED_BAD_WORDS", player.userID);
        #endregion

        #region XDNotifications
        private void AddNotify(BasePlayer player, string title, string description, string command = "", string cmdyes = "", string cmdno = "")
        {
            if (!XDNotifications) return;
            var Setting = config.ReferenceSetting.XDNotificationsSettings;
            Interface.Oxide.CallHook("AddNotify", player, title, description, Setting.Color, Setting.AlertDelete, Setting.SoundEffect, command, cmdyes, cmdno);
        }
        #endregion

        #region IQFakeActive
        public bool IsFake(string DisplayName) => (bool)IQFakeActive?.Call("IsFake", DisplayName);
        void SyncReservedFinish()
        {
            if (!config.ReferenceSetting.IQFakeActiveSettings.UseIQFakeActive) return;
            PrintWarning("IQChat - успешно синхронизирована с IQFakeActive");
            PrintWarning("=============SYNC==================");
        }
        #endregion

        #region IQRankSystem
        string IQRankGetRank(ulong userID) => (string)(IQRankSystem?.Call("API_GET_RANK_NAME", userID));
        string IQRankGetTimeGame(ulong userID) => (string)(IQRankSystem?.Call("API_GET_TIME_GAME", userID));
        List<string> IQRankListKey(ulong userID) => (List<string>)(IQRankSystem?.Call("API_RANK_USER_KEYS", userID));
        string IQRankGetNameRankKey(string Key) => (string)(IQRankSystem?.Call("API_GET_RANK_NAME", Key));
        void IQRankSetRank(ulong userID, string RankKey) => IQRankSystem?.Call("API_SET_ACTIVE_RANK", userID, RankKey);

        #endregion

        #endregion

        #region Vars
        private enum MuteType
        {
            Chat,
            Voice
        }
        private enum TakeElemntUser
        {
            Prefix,
            Nick,
            Chat,
            Rank,
            MultiPrefix
        }
        public Dictionary<BasePlayer, BasePlayer> PMHistory = new Dictionary<BasePlayer, BasePlayer>();

        public string PermMuteMenu = "iqchat.muteuse";
        class Response
        {
            [JsonProperty("country")]
            public string Country { get; set; }
        }
        public static StringBuilder sb = new StringBuilder();
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
        #endregion

        #region Configuration

        private static Configuration config = new Configuration();
        private class Configuration
        {
            [JsonProperty("Права для смены ника")]
            public string RenamePermission;
            [JsonProperty("Настройка префиксов")]
            public List<AdvancedFuncion> PrefixList = new List<AdvancedFuncion>();
            [JsonProperty("Настройка цветов для ников")]
            public List<AdvancedFuncion> NickColorList = new List<AdvancedFuncion>();
            [JsonProperty("Настройка цветов для сообщений")]
            public List<AdvancedFuncion> MessageColorList = new List<AdvancedFuncion>();
            [JsonProperty("Настройка сообщений в чате")]
            public MessageSettings MessageSetting;
            [JsonProperty("Настройка мутов")]
            public MuteController MuteControllers = new MuteController();
            [JsonProperty("Настройка UI интерфейса")]
            public Interface InterfaceChat;
            [JsonProperty("Настройка оповещения")]
            public AlertSetting AlertSettings;         
            [JsonProperty("Настройка привилегий")]
            public AutoSetups AutoSetupSetting;
            [JsonProperty("Настройка Rust+")]
            public RustPlus RustPlusSettings;
            [JsonProperty("Дополнительная настройка")]
            public OtherSettings OtherSetting;
            [JsonProperty("Настройка автоответчика")]
            public AnswerMessage AnswerMessages = new AnswerMessage();

            [JsonProperty("Настройка плагинов поддержки")]
            public ReferenceSettings ReferenceSetting = new ReferenceSettings();
            internal class AdvancedFuncion
            {
                [JsonProperty("Права")]
                public string Permissions;
                [JsonProperty("Значение")]
                public string Argument;
            }
            internal class AnswerMessage
            {
                [JsonProperty("Включить автоответчик?(true - да/false - нет)")]
                public bool UseAnswer;
                [JsonProperty("Настройка сообщений [Ключевое слово] = Ответ")]
                public Dictionary<string, string> AnswerMessageList = new Dictionary<string, string>();
            }
            internal class RustPlus
            {
                [JsonProperty("Использовать Rust+")]
                public bool UseRustPlus;
                [JsonProperty("Название для уведомления Rust+")]
                public string DisplayNameAlert;
            }
            
            internal class MuteController
            {
                [JsonProperty("Настройка причин блокировок чата")]
                public List<ReasonMuteChat> ReasonListChat = new List<ReasonMuteChat>();
                [JsonProperty("Настройка автоматического мута")]
                public AutoMute AutoMuteSettings = new AutoMute();
                internal class AutoMute
                {
                    [JsonProperty("Включить автоматический мут по запрещенным словам")]
                    public bool UseAutoMute;
                    [JsonProperty("Время автоматического мута")]
                    public int MuteTime;
                    [JsonProperty("Причина мута")]
                    public string ReasonMute;
                }

                internal class ReasonMuteChat
                {
                    [JsonProperty("Причина мута")]
                    public string Reason;
                    [JsonProperty("Время мута")]
                    public int TimeMute;
                }
            }

            internal class ReferenceSettings
            {
                [JsonProperty("Настройка XDNotifications")]
                public XDNotifications XDNotificationsSettings = new XDNotifications();
                [JsonProperty("Настройка IQModalMenu")]
                public IQModalMenu IQModalMenuSettings = new IQModalMenu();
                [JsonProperty("Настройка IQFakeActive")]
                public IQFakeActive IQFakeActiveSettings = new IQFakeActive();
                [JsonProperty("Настройка IQRankSystem")]
                public IQRankSystem IQRankSystems = new IQRankSystem();

                internal class IQModalMenu
                {
                    [JsonProperty("Спрайт на кнопку в модальном меню")]
                    public string Sprite;
                    [JsonProperty("Ссылка на кнопку в модальном меню(имеет приоритет над спрайтом)")]
                    public string Avatar;
                }

                internal class XDNotifications
                {
                    [JsonProperty("Включить поддержку XDNotifications(UI уведомления будут заменены на уведомление с XDNotifications)")]
                    public bool UseXDNotifications;
                    [JsonProperty("Цвет заднего фона уведомления(HEX)")]
                    public string Color;
                    [JsonProperty("Через сколько удалиться уведомление")]
                    public int AlertDelete;
                    [JsonProperty("Звуковой эффект")]
                    public string SoundEffect;
                }
                internal class IQRankSystem
                {
                    [JsonProperty("Использовать поддержку рангов")]
                    public bool UseRankSystem;
                    [JsonProperty("Отображать игрокам их отыгранное время рядом с рангом")]
                    public bool UseTimeStandart;
                }
                internal class IQFakeActive
                {
                    [JsonProperty("Использовать поддержку IQFakeActive")]
                    public bool UseIQFakeActive;
                }
            }
            internal class AutoSetups
            {
                [JsonProperty("Настройки сброса привилегий")]
                public ReturnDefault ReturnDefaultSetting = new ReturnDefault();
                [JsonProperty("Автоматической установки префиксов/цвета ника/цвета чата")]
                public SetupAuto SetupAutoSetting = new SetupAuto();
                internal class ReturnDefault
                {
                    [JsonProperty("Сбрасывать автоматически префикс при окончании его прав")]
                    public bool UseDropPrefix;
                    [JsonProperty("Сбрасывать автоматически цвет ника при окончании его прав")]
                    public bool UseDropColorNick;
                    [JsonProperty("Сбрасывать автоматически цвет чата при окончании его прав")]
                    public bool UseDropColorChat;

                    [JsonProperty("При окончании префикса, установится данный префикс")]
                    public string PrefixDefault;
                    [JsonProperty("При окончании цвета ника, установится данный цвет")]
                    public string NickDefault;
                    [JsonProperty("При окончании цвета сообщения, установится данный цвета")]
                    public string MessageDefault;
                }
                internal class SetupAuto
                {
                    [JsonProperty("Устанавливать автоматически префикс при получении его прав")]
                    public bool UseSetupAutoPrefix;
                    [JsonProperty("Устанавливать автоматически цвет ника при получении его прав")]
                    public bool UseSetupAutoColorNick;
                    [JsonProperty("Устанавливать автоматически цвет чата при получении его прав")]
                    public bool UseSetupAutoColorChat;

                }
            }
            internal class MessageSettings
            {
                [JsonProperty("Включить форматирование сообщений")]
                public bool FormatingMessage;
                [JsonProperty("Включить личные сообщения")]
                public bool PMActivate;
                [JsonProperty("Включить игнор ЛС игрокам(/ignore nick)")]
                public bool IgnoreUsePM;
                [JsonProperty("Включить Анти-Спам")]
                public bool AntiSpamActivate;
                [JsonProperty("Скрыть из чата выдачу предметов Админу")]
                public bool HideAdminGave;
                [JsonProperty("Использовать список запрещенных слов?")]
                public bool UseBadWords;
                [JsonProperty("Включить возможность использовать несколько префиксов сразу")]
                public bool MultiPrefix;
                [JsonProperty("Переносить мут в командный чат(В случае мута,игрок не сможет писать даже в командный чат)")]
                public bool MuteTeamChat;
                [JsonProperty("Пермишенс для иммунитета к антиспаму")]
                public string PermAdminImmunitetAntispam;
                [JsonProperty("Наименование оповещения в чат")]
                public string BroadcastTitle;
                [JsonProperty("Наименование упоминания игрока в чате")]
                public string AlertPlayerTitle; 
                [JsonProperty("Цвет сообщения упоминания игрока в чате")]
                public string AlertPlayerColor;
                [JsonProperty("Цвет сообщения оповещения в чат")]
                public string BroadcastColor;
                [JsonProperty("На какое сообщение заменять плохие слова")]
                public string ReplaceBadWord;
                [JsonProperty("Звук при при получении личного сообщения")]
                public string SoundPM;     
                [JsonProperty("Звук при при получении и отправки упоминания через @")]
                public string SoundAlertPlayer;            
                [JsonProperty("Время,через которое удалится сообщение с UI от администратора")]
                public int TimeDeleteAlertUI;
                [JsonProperty("Steam64ID для аватарки в чате")]
                public ulong Steam64IDAvatar;
                [JsonProperty("Время через которое игрок может отправлять сообщение (АнтиСпам)")]
                public int FloodTime;
                [JsonProperty("Список плохих слов")]
                public List<string> BadWords = new List<string>();
            }
            internal class Interface
            {
                [JsonProperty("HEX : Для задней панели")]
                public string HexPanel;
                [JsonProperty("HEX : Для титульной панели")]
                public string HexTitle;
                [JsonProperty("HEX : Для кнопок")]
                public string HexButton;
                [JsonProperty("HEX : Для текста")]
                public string HexLabel;
                [JsonProperty("HEX : Кнопки с активной страницей")]
                public string HexPageButtonActive;
                [JsonProperty("HEX : Кнопки с неактивной страницей")]
                public string HexPageButtonInActive;    
                [JsonProperty("HEX : Блюра для меню")]
                public string HexBlurMute;

                [JsonProperty("Настройка расположения основного UI")]
                public InterfacePosition InterfacePositions;
                [JsonProperty("Настройка расположения UI уведомления")]
                public AlertInterfaceSettings AlertInterfaceSetting;
                [JsonProperty("Дополнительная настройка UI интерфейса")]
                public OtherSettingsInterface OtherSettingInterface;

                internal class InterfacePosition
                {
                    [JsonProperty("Настройка основного меню")]
                    public MainPanel MainPanels;
                    [JsonProperty("Настройка панели управления блокировками чата")]
                    public MutePanel MutePanels;        
                    [JsonProperty("Настройка панели управления игнорированием чата")]
                    public IgnoredPanel IgnoredPanels;

                    internal class MutePanel
                    {
                        [JsonProperty("AnchorMin целевого UI в меню управленяи блокировкой чата")]
                        public string BackgroundAnchorMin;
                        [JsonProperty("AnchorMax основного UI в меню управленяи блокировкой чата")]
                        public string BackgroundAnchorMax;
                    }         
                    
                    internal class IgnoredPanel
                    {
                        [JsonProperty("AnchorMin целевого UI в меню управленяи игнорировани")]
                        public string BackgroundAnchorMin;
                        [JsonProperty("AnchorMax основного UI")]
                        public string BackgroundAnchorMax;
                    }

                    internal class MainPanel 
                    {
                        [JsonProperty("Настройка блока с информацией в меню")]
                        public InfromationBlocks InfromationBlock;     
                        [JsonProperty("Настройка блока администрирования в меню")]
                        public ModerationBlocks ModerationBlock;   
                        [JsonProperty("Настройка блока с настройками в меню")]
                        public SettingBlocks SettingBlock;     
                        [JsonProperty("Настройка блока с слайдерами и дроплистами в меню")]
                        public SliderAndDropListBlocks SliderAndDropListBlock;

                        [JsonProperty("AnchorMin целевого меню")]
                        public string BackgroundAnchorMin;
                        [JsonProperty("AnchorMax основного меню")]
                        public string BackgroundAnchorMax;

                        [JsonProperty("AnchorMin панели с контентом в меню (тут расположены кнопки и другая информация)")]
                        public string ContentAnchorMin;
                        [JsonProperty("AnchorMax панели с контентом в меню (тут расположены кнопки и другая информация)")]
                        public string ContentAnchorMax;

                        [JsonProperty("AnchorMin титульной панели в меню")]
                        public string TitleAnchorMin;
                        [JsonProperty("AnchorMax титульной панели в меню")]
                        public string TitleAnchorMax;

                        [JsonProperty("AnchorMin кнопки закрытия")]
                        public string CloseButtonAnchorMin;
                        [JsonProperty("AnchorMax кнопки закрытия")]
                        public string CloseButtonAnchorMax;    
                        
                        [JsonProperty("AnchorMin текста в титульной панели")]
                        public string TitleLabelAnchorMin;
                        [JsonProperty("AnchorMax текста в титульной панели")]
                        public string TitleLabelAnchorMax;

                        internal class InfromationBlocks 
                        {
                            [JsonProperty("AnchorMin текста заголовка в блоке информации")]
                            public string TitleLabelAnchorMin;
                            [JsonProperty("AnchorMax текста заголовка в блоке информации")]
                            public string TitleLabelAnchorMax;   
                            
                            [JsonProperty("AnchorMin текста с информаций о блокировке чата")]
                            public string MutedLabelAnchorMin;
                            [JsonProperty("AnchorMax текста с информацией о блокировке чата")]
                            public string MutedLabelAnchorMax;       
                            
                            [JsonProperty("AnchorMin текста с информаций о нике игрока")]
                            public string NickPlayerAnchorMin;
                            [JsonProperty("AnchorMax текста с информацией о нике игрока")]
                            public string NickPlayerAnchorMax;              
                            
                            [JsonProperty("AnchorMin титульного текста о игнорированных игроках")]
                            public string IgnoredLabelAnchorMin;
                            [JsonProperty("AnchorMax титульного текста о игнорировванных игроках")]
                            public string IgnoredLabelAnchorMax;      
                            
                            [JsonProperty("AnchorMin кнопки с игнор-меню")]
                            public string IgnoredButtonAnchorMin;
                            [JsonProperty("AnchorMax кнопки с игнор-меню")]
                            public string IgnoredButtonAnchorMax;
                        }

                        internal class ModerationBlocks
                        {
                            [JsonProperty("AnchorMin текста заголовка в блоке администрирования")]
                            public string TitleLabelAnchorMin;
                            [JsonProperty("AnchorMax текста заголовка в блоке администрирования")]
                            public string TitleLabelAnchorMax;     
                            
                            [JsonProperty("AnchorMin кнопки с меню блокировки чата")]
                            public string ButtonMuteAnchorMin;
                            [JsonProperty("AnchorMax кнопки с меню блокировки чата")]
                            public string ButtonMuteAnchorMax;        
                            
                            [JsonProperty("AnchorMin кнопки с блокировкой чата всем игрокам")]
                            public string ButtonMuteAllAnchorMin;
                            [JsonProperty("AnchorMax кнопки с блокировкой чата всем игрокам")]
                            public string ButtonMuteAllAnchorMax;          
                            
                            [JsonProperty("AnchorMin кнопки с блокировкой голосового чата всем игрокам")]
                            public string ButtonMuteVoiceAllAnchorMin;
                            [JsonProperty("AnchorMax кнопки с блокировкой голосового чата всем игрокам")]
                            public string ButtonMuteVoiceAllAnchorMax;
                        }

                        internal class SliderAndDropListBlocks
                        {
                            [JsonProperty("AnchorMin заголовка слайдера/дроп листа рангов")]
                            public string TitleSliderAndDropListRankAnchorMin;
                            [JsonProperty("AnchorMax заголовка слайдера/дроп листа рангов")]
                            public string TitleSliderAndDropListRankAnchorMax;

                            [JsonProperty("AnchorMin слайдера/дроп листа рангов")]
                            public string SliderAndDropListRankAnchorMin;
                            [JsonProperty("AnchorMax слайдера/дроп листа рангов")]
                            public string SliderAndDropListRankAnchorMax;

                            [JsonProperty("AnchorMin заголовка слайдера/дроп листа цвета сообщения в чате")]
                            public string TitleSliderAndDropListChatColorAnchorMin;
                            [JsonProperty("AnchorMax заголовка слайдера/дроп листа цвета сообщения в чате")]
                            public string TitleSliderAndDropListChatColorAnchorMax;

                            [JsonProperty("AnchorMin слайдера/дроп листа цвета сообщения в чате")]
                            public string SliderAndDropListChatColorAnchorMin;
                            [JsonProperty("AnchorMax слайдера/дроп листа цвета сообщения в чате")]
                            public string SliderAndDropListChatColorAnchorMax;

                            [JsonProperty("AnchorMin заголовка слайдера/дроп листа цвета ника в чате")]
                            public string TitleSliderAndDropListNickColorAnchorMin;
                            [JsonProperty("AnchorMax заголовка слайдера/дроп листа цвета ника в чате")]
                            public string TitleSliderAndDropListNickColorAnchorMax;

                            [JsonProperty("AnchorMin слайдера/дроп листа цвета ника в чате")]
                            public string SliderAndDropListNickColorAnchorMin;
                            [JsonProperty("AnchorMax слайдера/дроп листа цвета ника в чате")]
                            public string SliderAndDropListNickColorAnchorMax;

                            [JsonProperty("AnchorMin заголовка слайдера/дроп листа префикса в чате")]
                            public string TitleSliderAndDropListPrefixAnchorMin;
                            [JsonProperty("AnchorMax заголовка слайдера/дроп листа префикса в чате")]
                            public string TitleSliderAndDropListPrefixAnchorMax;

                            [JsonProperty("AnchorMin слайдера/дроп листа префикса в чате")]
                            public string SliderAndDropListPrefixAnchorMin;
                            [JsonProperty("AnchorMax слайдера/дроп листа префикса в чате")]
                            public string SliderAndDropListPrefixAnchorMax;
                        }

                        internal class SettingBlocks
                        {
                            [JsonProperty("AnchorMin заголовка в блоке настроек")]
                            public string TitleSettingAnchorMin;
                            [JsonProperty("AnchorMax заголовка в блоке настроек")]
                            public string TitleSettingAnchorMax;

                            [JsonProperty("AnchorMin текста настройки личных сообщений")]
                            public string SettingPMLabelAnchorMin;
                            [JsonProperty("AnchorMax текста настройки личных сообщений")]
                            public string SettingPMLabelAnchorMax;

                            [JsonProperty("AnchorMin текста настройки автоматических сообщений")]
                            public string SettingBroadcastAnchorMin;
                            [JsonProperty("AnchorMax текста настройки автоматических сообщений")]
                            public string SettingBroadcastAnchorMax;

                            [JsonProperty("AnchorMin текста настройки упоминания в чате")]
                            public string SettingAlertAnchorMin;
                            [JsonProperty("AnchorMax текста настройки упоминания в чате")]
                            public string SettingAlertAnchorMax;

                            [JsonProperty("AnchorMin текста настройки звукового оповещения")]
                            public string SettingSoundAnchorMin;
                            [JsonProperty("AnchorMax текста настройки звукового оповещения")]
                            public string SettingSoundAnchorMax;

                            [JsonProperty("AnchorMin бокса настройки личных сообщений")]
                            public string BoxSettingPMLabelAnchorMin;
                            [JsonProperty("AnchorMax бокса настройки личных сообщений")]
                            public string BoxSettingPMLabelAnchorMax;

                            [JsonProperty("AnchorMin бокса настройки автоматических сообщений")]
                            public string BoxSettingBroadcastAnchorMin;
                            [JsonProperty("AnchorMax бокса настройки автоматических сообщений")]
                            public string BoxSettingBroadcastAnchorMax;

                            [JsonProperty("AnchorMin бокса настройки упоминания в чате")]
                            public string BoxSettingAlertAnchorMin;
                            [JsonProperty("AnchorMax бокса настройки упоминания в чате")]
                            public string BoxSettingAlertAnchorMax;

                            [JsonProperty("AnchorMin бокса настройки звукового оповещения")]
                            public string BoxSettingSoundAnchorMin;
                            [JsonProperty("AnchorMax бокса настройки звукового оповещения")]
                            public string BoxSettingSoundAnchorMax;
                        }
                    }
                }

                internal class OtherSettingsInterface
                {
                    [JsonProperty("Использовать выпадающий список на префиксах(иначе будет слайдер)")]
                    public bool DropListPrefixUse;
                    [JsonProperty("Использовать выпадающий список на цветах ника(иначе будет слайдер)")]
                    public bool DropListColorNickUse;
                    [JsonProperty("Использовать выпадающий список на цветах чата(иначе будет слайдер)")]
                    public bool DropListColorChatUse;
                    [JsonProperty("Использовать выпадающий список на рангах(IQRankSystem)(иначе будет слайдер)")]
                    public bool DropListRankUse;
                }

                internal class AlertInterfaceSettings
                {
                    [JsonProperty("AnchorMin")]
                    public string AnchorMin;
                    [JsonProperty("AnchorMax")]
                    public string AnchorMax;
                    [JsonProperty("OffsetMin")]
                    public string OffsetMin;
                    [JsonProperty("OffsetMax")]
                    public string OffsetMax;
                }
            }

            internal class AlertSetting
            {
                [JsonProperty("Включить случайное сообщение зашедшему игроку")]
                public bool WelcomeMessageUse;
                [JsonProperty("Список сообщений игроку при входе")]
                public List<string> WelcomeMessage = new List<string>();
                [JsonProperty("Уведомлять о входе игрока в чат")]
                public bool ConnectedAlert;
                [JsonProperty("Уведомлять о входе админа на сервер в чат")]
                public Boolean ConnectedAlertAdmin;
                [JsonProperty("Уведомлять о выходе админа на сервер в чат")]
                public Boolean DisconnectedAlertAdmin;
                [JsonProperty("Включить случайные уведомления о входе игрока из списка")]
                public bool ConnectionAlertRandom;
                [JsonProperty("Случайные уведомления о входе игрока({0} - ник игрока, {1} - страна(если включено отображение страны)")]
                public List<string> RandomConnectionAlert = new List<string>();
                [JsonProperty("Отображать страну зашедшего игрока")]
                public bool ConnectedWorld;
                [JsonProperty("Уведомлять о выходе игрока в чат из списка")]
                public bool DisconnectedAlert;
                [JsonProperty("Включить случайные уведомления о входе игрока")]
                public bool DisconnectedAlertRandom;
                [JsonProperty("Случайные уведомления о входе игрока({0} - ник игрока, {1} - причина выхода(если включена причина)")]
                public List<string> RandomDisconnectedAlert = new List<string>();
                [JsonProperty("Отображать причину выхода игрока")]
                public bool DisconnectedReason;
                [JsonProperty("При уведомлении о входе/выходе игрока отображать его аватар напротив ника")]
                public bool ConnectedAvatarUse;
                [JsonProperty("Включить автоматические сообщения в чат")]
                public bool AlertMessage;
                [JsonProperty("Тип автоматических сообщений : true - поочередные/false - случайные")]
                public bool AlertMessageType;
                [JsonProperty("Настройка отправки автоматических сообщений в чат")]
                public List<string> MessageList;
                [JsonProperty("Интервал отправки сообщений в чат(Броадкастер)")]
                public int MessageListTimer;
            }
            internal class OtherSettings
            {
                [JsonProperty("Использовать дискорд")]
                public bool UseDiscord;
                [JsonProperty("Вебхук для логирования чата в дискорд")]
                public string WebhooksChatLog;
                [JsonProperty("Вебхук для логирования информации о мутах в дискорде")]
                public string WebhooksMuteInfo;
            }

            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    PrefixList = new List<AdvancedFuncion>
                    {
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "<color=yellow><b>[+]</b></color>",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "<color=yellow><b>[ИГРОК]</b></color>",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.vip",
                            Argument = "<color=yellow><b>[VIP]</b></color>",
                        },
                    },
                    NickColorList = new List<AdvancedFuncion>
                    {
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "#DBEAEC",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "#FFC428",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.vip",
                            Argument = "#45AAB4",
                        },
                    },
                    MessageColorList = new List<AdvancedFuncion>
                    {
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "#DBEAEC",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.default",
                            Argument = "#FFC428",
                        },
                        new AdvancedFuncion
                        {
                            Permissions = "iqchat.vip",
                            Argument = "#45AAB4",
                        },
                    },
                    AutoSetupSetting = new AutoSetups
                    {
                        ReturnDefaultSetting = new AutoSetups.ReturnDefault
                        {
                            UseDropColorChat = true,
                            UseDropColorNick = true,
                            UseDropPrefix = true,

                            PrefixDefault = "",
                            NickDefault = "",
                            MessageDefault = "",
                        },
                        SetupAutoSetting = new AutoSetups.SetupAuto
                        {
                            UseSetupAutoColorChat = true,
                            UseSetupAutoColorNick = true,
                            UseSetupAutoPrefix = true,
                        }
                    },
                    RustPlusSettings = new RustPlus
                    {
                        UseRustPlus = true,
                        DisplayNameAlert = "СУПЕР СЕРВЕР",
                    },
                    MessageSetting = new MessageSettings
                    {
                        UseBadWords = true,
                        HideAdminGave = true,
                        IgnoreUsePM = true,
                        MuteTeamChat = true,
                        PermAdminImmunitetAntispam = "iqchat.adminspam",
                        BroadcastTitle = "<color=#F65050FF><b>[ОПОВЕЩЕНИЕ]</b></color>",
                        AlertPlayerTitle = "<color=#A7F64FFF><b>[УПОМИНАНИЕ]</b></color>",
                        AlertPlayerColor = "#efedee",
                        BroadcastColor = "#efedee",
                        ReplaceBadWord = "Ругаюсь матом",
                        Steam64IDAvatar = 0,
                        TimeDeleteAlertUI = 5,
                        PMActivate = true,
                        SoundAlertPlayer = "assets/bundled/prefabs/fx/notice/item.select.fx.prefab",
                        SoundPM = "assets/bundled/prefabs/fx/notice/stack.world.fx.prefab",
                        AntiSpamActivate = true,
                        FloodTime = 5,
                        FormatingMessage = true,
                        MultiPrefix = true,
                        BadWords = new List<string> { "хуй", "гей", "говно", "бля", "тварь" }
                    },
                    MuteControllers = new MuteController
                    {
                        AutoMuteSettings = new MuteController.AutoMute
                        {
                            UseAutoMute = false,
                            MuteTime = 600,
                            ReasonMute = "Ненормативная лексика"
                        },
                        ReasonListChat = new List<MuteController.ReasonMuteChat>
                        {
                            new MuteController.ReasonMuteChat
                            {
                                Reason = "Оскорбление родителей",
                                TimeMute = 1200,
                            },
                            new MuteController.ReasonMuteChat
                            {
                                Reason = "Оскорбление игроков",
                                TimeMute = 100
                            }
                        },
                    },
                    RenamePermission = "iqchat.renameuse",                  
                    AlertSettings = new AlertSetting
                    {
                        ConnectedAlertAdmin = false,
                        DisconnectedAlertAdmin = false,
                        MessageListTimer = 60,
                        AlertMessageType = false,
                        WelcomeMessageUse = true,
                        ConnectionAlertRandom = false,
                        DisconnectedAlertRandom = false,
                        RandomConnectionAlert = new List<string>
                        {
                            "{0} влетел как дурачок из {1}",
                            "{0} залетел на сервер из {1}, соболезнуем",
                            "{0} прыгнул на сервачок"
                        },
                        RandomDisconnectedAlert = new List<string>
                        {
                            "{0} ушел в мир иной",
                            "{0} вылетел с сервера с причиной {1}",
                            "{0} пошел на другой сервачок"
                        },
                        ConnectedAlert = true,
                        ConnectedWorld = true,
                        DisconnectedAlert = true,
                        DisconnectedReason = true,
                        AlertMessage = true,
                        ConnectedAvatarUse = true,
                        MessageList = new List<string>
                        {
                        "Автоматическое сообщение #1",
                        "Автоматическое сообщение #2",
                        "Автоматическое сообщение #3",
                        "Автоматическое сообщение #4",
                        "Автоматическое сообщение #5",
                        "Автоматическое сообщение #6",
                        },
                        WelcomeMessage = new List<string>
                        {
                            "Добро пожаловать на сервер SUPERSERVER\nРады,что выбрал именно нас!",
                            "С возвращением на сервер!\nЖелаем тебе удачи",
                            "Добро пожаловать на сервер\nУ нас самые лучшие плагины",
                        },

                    },
                    InterfaceChat = new Interface
                    {
                        HexPanel = "#3B3A2EFF",
                        HexTitle = "#1B211BFF",
                        HexButton = "#252a24",
                        HexLabel = "#efedee",   
                        HexPageButtonActive = "#3B3A2EFF",
                        HexPageButtonInActive = "#3B3A2EB9",
                        HexBlurMute = "#3B3A2EC4",

                        InterfacePositions = new Interface.InterfacePosition
                        {
                            MainPanels = new Interface.InterfacePosition.MainPanel
                            {
                                BackgroundAnchorMin = "0.6625 0.1186",
                                BackgroundAnchorMax = "0.9869678 0.8814",

                                ContentAnchorMin = "0 0",
                                ContentAnchorMax = "1 0.8896463",

                                TitleAnchorMin = "0 0.8896463",
                                TitleAnchorMax = "1 1",

                                CloseButtonAnchorMin = "0.9037238 0.3409882",
                                CloseButtonAnchorMax = "0.95509 0.6929768",

                                TitleLabelAnchorMin = "0.04494545 0",
                                TitleLabelAnchorMax = "0.739994 1",

                                InfromationBlock = new Interface.InterfacePosition.MainPanel.InfromationBlocks
                                {
                                    TitleLabelAnchorMin = "0.04152404 0.5224559",
                                    TitleLabelAnchorMax = "0.6196045 0.5934054",

                                    MutedLabelAnchorMin = "0.04152404 0.4078422",
                                    MutedLabelAnchorMax = "0.9486688 0.46788",

                                    NickPlayerAnchorMin = "0.04152404 0.4610541",
                                    NickPlayerAnchorMax = "0.9486688 0.5210921",

                                    IgnoredLabelAnchorMin = "0.04152405 0.3559946",
                                    IgnoredLabelAnchorMax = "0.5168719 0.4160324",

                                    IgnoredButtonAnchorMin = "0.443033 0.366909",
                                    IgnoredButtonAnchorMax = "0.9584759 0.4173968",
                                },
                                ModerationBlock = new Interface.InterfacePosition.MainPanel.ModerationBlocks
                                {
                                    TitleLabelAnchorMin = "0.04152405 0.2482094",
                                    TitleLabelAnchorMax = "0.9390378 0.3191593",

                                    ButtonMuteAnchorMin = "0.04152405 0.1677083",
                                    ButtonMuteAnchorMax = "0.9584759 0.238659",

                                    ButtonMuteAllAnchorMin = "0.04152405 0.0913007",
                                    ButtonMuteAllAnchorMax = "0.9584759 0.1622516",

                                    ButtonMuteVoiceAllAnchorMin = "0.04152405 0.01489319",
                                    ButtonMuteVoiceAllAnchorMax = "0.9584759 0.08584419",
                                },
                                SliderAndDropListBlock = new Interface.InterfacePosition.MainPanel.SliderAndDropListBlocks
                                {
                                    TitleSliderAndDropListRankAnchorMin = "0.04152405 0.761226",
                                    TitleSliderAndDropListRankAnchorMax = "0.4430331 0.8103468",

                                    SliderAndDropListRankAnchorMin = "0.4430331 0.7653192",
                                    SliderAndDropListRankAnchorMax = "0.9584759 0.81",

                                    TitleSliderAndDropListChatColorAnchorMin = "0.04152405 0.8117096",
                                    TitleSliderAndDropListChatColorAnchorMax = "0.4430331 0.8649233",

                                    SliderAndDropListChatColorAnchorMin = "0.4430331 0.8198956",
                                    SliderAndDropListChatColorAnchorMax = "0.9584758 0.8645764",

                                    TitleSliderAndDropListNickColorAnchorMin = "0.04152405 0.8717443",
                                    TitleSliderAndDropListNickColorAnchorMax = "0.4430331 0.9194997",

                                    SliderAndDropListNickColorAnchorMin = "0.4430331 0.8744721",
                                    SliderAndDropListNickColorAnchorMax = "0.9584758 0.9191527",

                                    TitleSliderAndDropListPrefixAnchorMin = "0.04152405 0.9222281",
                                    TitleSliderAndDropListPrefixAnchorMax = "0.4430331 0.9740761",

                                    SliderAndDropListPrefixAnchorMin = "0.4430331 0.9290484",
                                    SliderAndDropListPrefixAnchorMax = "0.9584758 0.9737292",
                                },
                                SettingBlock = new Interface.InterfacePosition.MainPanel.SettingBlocks
                                {
                                    TitleSettingAnchorMin = "0.04152405 0.6779985",
                                    TitleSettingAnchorMax = "0.6196045 0.7489485",

                                    SettingPMLabelAnchorMin = "0.04312925 0.6411574",
                                    SettingPMLabelAnchorMax = "0.4125344 0.6779992",

                                    SettingBroadcastAnchorMin = "0.04312925 0.5974963",
                                    SettingBroadcastAnchorMax = "0.4125344 0.6343381",

                                    SettingAlertAnchorMin = "0.584079 0.6411574",
                                    SettingAlertAnchorMax = "0.9534805 0.6779992",

                                    SettingSoundAnchorMin = "0.584079 0.5974963",
                                    SettingSoundAnchorMax = "0.9534805 0.6343381",

                                    BoxSettingPMLabelAnchorMin = "0.38664 0.6425239",
                                    BoxSettingPMLabelAnchorMax = "0.4251647 0.67527",

                                    BoxSettingBroadcastAnchorMin = "0.38664 0.5988628",
                                    BoxSettingBroadcastAnchorMax = "0.4251647 0.6316",
                                    
                                    BoxSettingSoundAnchorMin = "0.5600009 0.5988628",
                                    BoxSettingSoundAnchorMax = "0.5985249 0.6316088",

                                    BoxSettingAlertAnchorMin = "0.5600009 0.6425239",
                                    BoxSettingAlertAnchorMax = "0.5985249 0.67527",
                                },
                            },
                            MutePanels = new Interface.InterfacePosition.MutePanel
                            {
                                BackgroundAnchorMin = "0.0130322 0.1186",
                                BackgroundAnchorMax = "0.6588541 0.8814",
                            },
                            IgnoredPanels = new Interface.InterfacePosition.IgnoredPanel
                            {
                                BackgroundAnchorMin = "0.0130322 0.1186",
                                BackgroundAnchorMax = "0.6588541 0.8814"
                            }
                        },
                        AlertInterfaceSetting = new Interface.AlertInterfaceSettings
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "10 -70",
                            OffsetMax = "330 -20"
                        },
                        OtherSettingInterface = new Interface.OtherSettingsInterface
                        {
                            DropListColorChatUse = false,
                            DropListColorNickUse = false,
                            DropListPrefixUse = false,
                            DropListRankUse = false,
                        }
                    },
                    OtherSetting = new OtherSettings
                    {
                        UseDiscord = false,
                        WebhooksChatLog = "",
                        WebhooksMuteInfo = "",
                    },
                    AnswerMessages = new AnswerMessage
                    {
                        UseAnswer = true,
                        AnswerMessageList = new Dictionary<string, string>
                        {
                            ["вайп"] = "Вайп будет 27.06",
                            ["wipe"] = "Вайп будет 27.06",
                            ["читер"] = "Нашли читера?Напиши /report и отправь жалобу"
                        }
                    },
                    ReferenceSetting = new ReferenceSettings
                    {
                        XDNotificationsSettings = new ReferenceSettings.XDNotifications
                        {                         
                            UseXDNotifications = false,
                            AlertDelete = 5,
                            Color = "#762424FF",
                            SoundEffect = "",
                        },
                        IQFakeActiveSettings = new ReferenceSettings.IQFakeActive
                        {
                            UseIQFakeActive = true,
                        },
                        IQRankSystems = new ReferenceSettings.IQRankSystem
                        {
                            UseRankSystem = false,
                            UseTimeStandart = true
                        },
                        IQModalMenuSettings = new ReferenceSettings.IQModalMenu
                        {
                            Sprite = "assets/icons/translate.png",
                            Avatar = "",
                        }
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                if(config.InterfaceChat.OtherSettingInterface == null)
                {
                    config.InterfaceChat.OtherSettingInterface = new Configuration.Interface.OtherSettingsInterface
                    {
                        DropListColorChatUse = false,
                        DropListColorNickUse = false,
                        DropListPrefixUse = false,
                        DropListRankUse = false,
                    };
                }
                if(config.InterfaceChat.InterfacePositions == null)
                {
                    config.InterfaceChat.InterfacePositions = new Configuration.Interface.InterfacePosition
                    {
                        MainPanels = new Configuration.Interface.InterfacePosition.MainPanel
                        {
                            BackgroundAnchorMin = "0.6625 0.1186",
                            BackgroundAnchorMax = "0.9869678 0.8814",

                            ContentAnchorMin = "0 0",
                            ContentAnchorMax = "1 0.8896463",

                            TitleAnchorMin = "0 0.8896463",
                            TitleAnchorMax = "1 1",

                            CloseButtonAnchorMin = "0.9037238 0.3409882",
                            CloseButtonAnchorMax = "0.95509 0.6929768",

                            TitleLabelAnchorMin = "0.04494545 0",
                            TitleLabelAnchorMax = "0.739994 1",

                            InfromationBlock = new Configuration.Interface.InterfacePosition.MainPanel.InfromationBlocks
                            {
                                TitleLabelAnchorMin = "0.04152404 0.5224559",
                                TitleLabelAnchorMax = "0.6196045 0.5934054",

                                MutedLabelAnchorMin = "0.04152404 0.4078422",
                                MutedLabelAnchorMax = "0.9486688 0.46788",

                                NickPlayerAnchorMin = "0.04152404 0.4610541",
                                NickPlayerAnchorMax = "0.9486688 0.5210921",

                                IgnoredLabelAnchorMin = "0.04152405 0.3559946",
                                IgnoredLabelAnchorMax = "0.5168719 0.4160324",

                                IgnoredButtonAnchorMin = "0.443033 0.366909",
                                IgnoredButtonAnchorMax = "0.9584759 0.4173968",
                            },
                            ModerationBlock = new Configuration.Interface.InterfacePosition.MainPanel.ModerationBlocks
                            {
                                TitleLabelAnchorMin = "0.04152405 0.2482094",
                                TitleLabelAnchorMax = "0.9390378 0.3191593",

                                ButtonMuteAnchorMin = "0.04152405 0.1677083",
                                ButtonMuteAnchorMax = "0.9584759 0.238659",

                                ButtonMuteAllAnchorMin = "0.04152405 0.0913007",
                                ButtonMuteAllAnchorMax = "0.9584759 0.1622516",

                                ButtonMuteVoiceAllAnchorMin = "0.04152405 0.01489319",
                                ButtonMuteVoiceAllAnchorMax = "0.9584759 0.08584419",
                            },
                            SliderAndDropListBlock = new Configuration.Interface.InterfacePosition.MainPanel.SliderAndDropListBlocks
                            {
                                TitleSliderAndDropListRankAnchorMin = "0.04152405 0.761226",
                                TitleSliderAndDropListRankAnchorMax = "0.4430331 0.8103468",

                                SliderAndDropListRankAnchorMin = "0.4430331 0.7653192",
                                SliderAndDropListRankAnchorMax = "0.9584759 0.81",

                                TitleSliderAndDropListChatColorAnchorMin = "0.04152405 0.8117096",
                                TitleSliderAndDropListChatColorAnchorMax = "0.4430331 0.8649233",

                                SliderAndDropListChatColorAnchorMin = "0.4430331 0.8198956",
                                SliderAndDropListChatColorAnchorMax = "0.9584758 0.8645764",

                                TitleSliderAndDropListNickColorAnchorMin = "0.04152405 0.8717443",
                                TitleSliderAndDropListNickColorAnchorMax = "0.4430331 0.9194997",

                                SliderAndDropListNickColorAnchorMin = "0.4430331 0.8744721",
                                SliderAndDropListNickColorAnchorMax = "0.9584758 0.9191527",

                                TitleSliderAndDropListPrefixAnchorMin = "0.04152405 0.9222281",
                                TitleSliderAndDropListPrefixAnchorMax = "0.4430331 0.9740761",

                                SliderAndDropListPrefixAnchorMin = "0.4430331 0.9290484",
                                SliderAndDropListPrefixAnchorMax = "0.9584758 0.9737292",
                            },
                            SettingBlock = new Configuration.Interface.InterfacePosition.MainPanel.SettingBlocks
                            {
                                TitleSettingAnchorMin = "0.04152405 0.6779985",
                                TitleSettingAnchorMax = "0.6196045 0.7489485",

                                SettingPMLabelAnchorMin = "0.04312925 0.6411574",
                                SettingPMLabelAnchorMax = "0.4125344 0.6779992",

                                SettingBroadcastAnchorMin = "0.04312925 0.5974963",
                                SettingBroadcastAnchorMax = "0.4125344 0.6343381",

                                SettingAlertAnchorMin = "0.584079 0.6411574",
                                SettingAlertAnchorMax = "0.9534805 0.6779992",

                                SettingSoundAnchorMin = "0.584079 0.5974963",
                                SettingSoundAnchorMax = "0.9534805 0.6343381",

                                BoxSettingPMLabelAnchorMin = "0.38664 0.6425239",
                                BoxSettingPMLabelAnchorMax = "0.4251647 0.67527",

                                BoxSettingBroadcastAnchorMin = "0.38664 0.5988628",
                                BoxSettingBroadcastAnchorMax = "0.4251647 0.6316",

                                BoxSettingSoundAnchorMin = "0.5600009 0.5988628",
                                BoxSettingSoundAnchorMax = "0.5985249 0.6316088",

                                BoxSettingAlertAnchorMin = "0.5600009 0.6425239",
                                BoxSettingAlertAnchorMax = "0.5985249 0.67527",
                            },
                        },
                        MutePanels = new Configuration.Interface.InterfacePosition.MutePanel
                        {
                            BackgroundAnchorMin = "0.0130322 0.1186",
                            BackgroundAnchorMax = "0.6588541 0.8814",
                        },
                        IgnoredPanels = new Configuration.Interface.InterfacePosition.IgnoredPanel
                        {
                            BackgroundAnchorMin = "0.0130322 0.1186",
                            BackgroundAnchorMax = "0.6588541 0.8814"
                        }
                    };

                    PrintWarning("Новый конфиг был успешно подтянут с обновлением!");
                }
            }
            catch
            {
                PrintWarning("Ошибка #132" + $"чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }
            NextTick(SaveConfig);
        }

        void RegisteredPermissions()
        {
            for (int MsgColor = 0; MsgColor < config.MessageColorList.Count; MsgColor++)
                if (!permission.PermissionExists(config.MessageColorList[MsgColor].Permissions, this))
                    permission.RegisterPermission(config.MessageColorList[MsgColor].Permissions, this);

            for (int NickColorList = 0; NickColorList < config.NickColorList.Count; NickColorList++)
                if (!permission.PermissionExists(config.NickColorList[NickColorList].Permissions, this))
                    permission.RegisterPermission(config.NickColorList[NickColorList].Permissions, this);

            for (int PrefixList = 0; PrefixList < config.PrefixList.Count; PrefixList++)
                if (!permission.PermissionExists(config.PrefixList[PrefixList].Permissions, this))
                    permission.RegisterPermission(config.PrefixList[PrefixList].Permissions, this);

            permission.RegisterPermission(config.RenamePermission, this);
            permission.RegisterPermission(PermMuteMenu, this);
            permission.RegisterPermission(config.MessageSetting.PermAdminImmunitetAntispam,this);
            PrintWarning("Permissions - completed");
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data
        [JsonProperty("Дата с настройкой чата игрока")]
        public Dictionary<ulong, SettingUser> ChatSettingUser = new Dictionary<ulong, SettingUser>();
        [JsonProperty("Дата с Административной настройкой")] public AdminSettings AdminSetting = new AdminSettings();
        public class SettingUser
        {
            public string ChatPrefix;
            public List<string> MultiPrefix = new List<string>();
            public string NickColor;
            public string MessageColor;
            public double MuteChatTime;
            public double MuteVoiceTime;
            public bool PMTurn;
            public bool AlertTurn;
            public bool BroadcastTurn;
            public bool SoundTurn;
            public bool GlobalChatTurn;
            public List<ulong> IgnoredUsers = new List<ulong>();
        }

        public class AdminSettings
        {
            public bool MuteChatAll;
            public bool MuteVoiceAll;
            public Dictionary<ulong, string> RenameList = new Dictionary<ulong, string>()
;        }
        void ReadData()
        {
            ChatSettingUser = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, SettingUser>>("IQChat/IQUser");
            AdminSetting = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<AdminSettings>("IQChat/AdminSetting");
        }
        void WriteData()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQChat/IQUser", ChatSettingUser);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQChat/AdminSetting", AdminSetting);
        }

        void RegisteredDataUser(BasePlayer player)
        {
            if (!ChatSettingUser.ContainsKey(player.userID))
                ChatSettingUser.Add(player.userID, new SettingUser
                {
                    ChatPrefix = config.AutoSetupSetting.ReturnDefaultSetting.PrefixDefault,
                    NickColor = config.AutoSetupSetting.ReturnDefaultSetting.NickDefault,
                    MessageColor = config.AutoSetupSetting.ReturnDefaultSetting.MessageDefault,
                    MuteChatTime = 0,
                    MuteVoiceTime = 0,
                    AlertTurn = true,
                    PMTurn = true,
                    BroadcastTurn = true,
                    SoundTurn = true,
                    GlobalChatTurn = true,
                    MultiPrefix = new List<string> { },
                    IgnoredUsers = new List<ulong> { },
                    
                });
        }

        #endregion

        #region Hooks     
        private bool OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (Interface.Oxide.CallHook("CanChatMessage", player, message) != null) return false;
            SeparatorChat(channel, player, message);
            return false;
        }
        private object OnServerMessage(string message, string name)
        {
            if (config.MessageSetting.HideAdminGave)
                if (message.Contains("gave") && name == "SERVER")
                    return true;
            return null;
        }
        void OnUserPermissionGranted(string id, string permName) => AutoSetupData(id, permName);
        private void OnUserGroupAdded(string id, string groupName)
        {
            var PermissionsGroup = permission.GetGroupPermissions(groupName);
            if (PermissionsGroup == null) return;
            foreach (var permName in PermissionsGroup)
                AutoSetupData(id, permName); 
        }
        void OnUserPermissionRevoked(string id, string permName) => AutoReturnDefaultData(id, permName);
        void OnUserGroupRemoved(string id, string groupName)
        {
            var PermissionsGroup = permission.GetGroupPermissions(groupName);
            if (PermissionsGroup == null) return;
            foreach (var permName in PermissionsGroup)
                AutoReturnDefaultData(id, permName);
        }
        object OnPlayerVoice(BasePlayer player, Byte[] data)
        {
            var DataPlayer = ChatSettingUser[player.userID];
            bool IsMuted = DataPlayer.MuteVoiceTime > CurrentTime() ? true : false;
            if (IsMuted)
                return false;
            return null;
        }
        void SynhModular() => IQModalMenuConnected();
        private void OnServerInitialized()
        {
            ReadData();
            foreach (var p in BasePlayer.activePlayerList)
                RegisteredDataUser(p);

            RegisteredPermissions();
            BroadcastAuto();

            if (IQModalMenu)
               IQModalMenuConnected();
        }
        void OnPlayerConnected(BasePlayer player)
        {
            RegisteredDataUser(player);
            var Alert = config.AlertSettings;
            if (Alert.ConnectedAlert)
            {
                if (!Alert.ConnectedAlertAdmin)
                    if (player.IsAdmin) return;

                string Avatar = Alert.ConnectedAvatarUse ? player.UserIDString : "";
                string Message = string.Empty;
                if (config.AlertSettings.ConnectedWorld)
                {
                    webrequest.Enqueue("http://ip-api.com/json/" + player.net.connection.ipaddress.Split(':')[0], null, (code, response) =>
                    {
                        if (code != 200 || response == null)
                            return;

                        string country = JsonConvert.DeserializeObject<Response>(response).Country;

                        if (Alert.ConnectionAlertRandom)
                        {
                            sb.Clear();
                            int RandomIndex = UnityEngine.Random.Range(0, Alert.RandomConnectionAlert.Count);
                            Message = sb.AppendFormat(Alert.RandomConnectionAlert[RandomIndex], player.displayName, country).ToString();
                        }
                        else Message = GetLang("WELCOME_PLAYER_WORLD", player.UserIDString, player.displayName, country);
                        ReplyBroadcast(Message, "", Avatar);
                    }, this);
                }
                else
                {
                    if (Alert.ConnectionAlertRandom)
                    {
                        sb.Clear();
                        int RandomIndex = UnityEngine.Random.Range(0, Alert.RandomConnectionAlert.Count);
                        Message = sb.AppendFormat(Alert.RandomConnectionAlert[RandomIndex], player.displayName).ToString();
                    }
                    else Message = GetLang("WELCOME_PLAYER", player.UserIDString, player.displayName);
                    ReplyBroadcast(Message, "", Avatar); 
                }
            }
            if (Alert.WelcomeMessageUse)
            {
                int RandomMessage = UnityEngine.Random.Range(0, Alert.WelcomeMessage.Count);
                string WelcomeMessage = Alert.WelcomeMessage[RandomMessage];
                ReplySystem(Chat.ChatChannel.Global, player, WelcomeMessage);
            }

            CheckValidInformation(player);
        }
        void Unload()
        {
            if (IQModalMenu)
                IQModalMenuDisconnected();
            WriteData();
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var Alert = config.AlertSettings;
            if (Alert.DisconnectedAlert)
            {
                if (!Alert.DisconnectedAlertAdmin)
                    if (player.IsAdmin) return;

                string Avatar = Alert.ConnectedAvatarUse ? player.UserIDString : "";
                string Message = string.Empty;
                if (Alert.DisconnectedAlertRandom)
                {
                    sb.Clear();
                    int RandomIndex = UnityEngine.Random.Range(0, Alert.RandomDisconnectedAlert.Count);
                    Message = sb.AppendFormat(Alert.RandomDisconnectedAlert[RandomIndex], player.displayName, reason).ToString();
                }
                else Message = config.AlertSettings.DisconnectedReason ? GetLang("LEAVE_PLAYER_REASON", player.UserIDString, player.displayName, reason) : GetLang("LEAVE_PLAYER", player.UserIDString, player.displayName);
                ReplyBroadcast(Message, "", Avatar);
            }
        }
        #endregion

        #region DiscordFunc

        #region FancyDiscord
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

            }, this, RequestMethod.POST, header);
        }
        #endregion

        void DiscordSendMessage(string key, string WebHooks, ulong userID = 0, params object[] args)
        {
            if (!config.OtherSetting.UseDiscord) return;
            if (String.IsNullOrWhiteSpace(WebHooks)) return;

            List<Fields> fields = new List<Fields>
                {
                    new Fields("IQChat", key, true),
                };

            FancyMessage newMessage = new FancyMessage(null, false, new FancyMessage.Embeds[1] { new FancyMessage.Embeds(null, 635133, fields, new Authors("IQChat", "https://vk.com/mir_inc", "https://i.imgur.com/ILk3uJc.png", null), new Footer("Author: Mercury[vk.com/mir_inc]", "https://i.imgur.com/ILk3uJc.png", null)) });
            Request($"{WebHooks}", newMessage.toJSON());
        }
        #endregion

        #region Func
        
        public bool IsMutedUser(ulong userID)
        {
            var DataPlayer = ChatSettingUser[userID];
            return DataPlayer.MuteChatTime > CurrentTime();
        }
        public bool IsMutedVoiceUser(ulong userID)
        {
            var DataPlayer = ChatSettingUser[userID];
            return DataPlayer.MuteVoiceTime > CurrentTime();
        }
        private bool IsIgnored(ulong userID, ulong TargetID) => (bool)ChatSettingUser[userID].IgnoredUsers.Contains(TargetID);

        private void SeparatorChat(Chat.ChatChannel channel, BasePlayer player, string Message)
        {
            var DataPlayer = ChatSettingUser[player.userID];

            if (IsMutedUser(player.userID))
            {
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("FUNC_MESSAGE_ISMUTED_TRUE", player.UserIDString, FormatTime(TimeSpan.FromSeconds(DataPlayer.MuteChatTime - CurrentTime()))));
                return;
            }

            var RankSettings = config.ReferenceSetting.IQRankSystems;
            var MessageSettings = config.MessageSetting;
            var MuteController = config.MuteControllers.AutoMuteSettings;
            string OutMessage = Message;
            string PrefxiPlayer = "";
            string MessageSeparator = "";
            string ColorNickPlayer = DataPlayer.NickColor;
            string ColorMessagePlayer = DataPlayer.MessageColor;
            string DisplayName = AdminSetting.RenameList.ContainsKey(player.userID) ? AdminSetting.RenameList[player.userID] : player.displayName;

            if (MessageSettings.FormatingMessage)
                OutMessage = $"{Message.ToLower().Substring(0, 1).ToUpper()}{Message.Remove(0, 1).ToLower()}";

            if (MessageSettings.UseBadWords)
                foreach (var DetectedMessage in OutMessage.Split(' '))
                    if (MessageSettings.BadWords.Contains(DetectedMessage.ToLower()))
                    {
                        OutMessage = OutMessage.Replace(DetectedMessage, MessageSettings.ReplaceBadWord);
                        BadWords(player);
                        if (MuteController.UseAutoMute)
                            MutePlayer(player, MuteType.Chat, 0, null, MuteController.ReasonMute, MuteController.MuteTime);
                    }

            if (MessageSettings.MultiPrefix)
            {
                if (DataPlayer.MultiPrefix != null)

                    for (int i = 0; i < DataPlayer.MultiPrefix.Count; i++)
                        PrefxiPlayer += DataPlayer.MultiPrefix[i];
            }
            else PrefxiPlayer = DataPlayer.ChatPrefix;

            string ModifiedNick = string.IsNullOrWhiteSpace(ColorNickPlayer) ? player.IsAdmin ? $"<color=#a8fc55>{DisplayName}</color>" : $"<color=#54aafe>{DisplayName}</color>" : $"<color={ColorNickPlayer}>{DisplayName}</color>";
            string ModifiedMessage = string.IsNullOrWhiteSpace(ColorMessagePlayer) ? OutMessage : $"<color={ColorMessagePlayer}>{OutMessage}</color>";
            string ModifiedChannel = channel == Chat.ChatChannel.Team ? "<color=#a5e664>[Team]</color>" : channel == Chat.ChatChannel.Cards ? "<color=#AA8234>[Cards]</color>" : "";

            string Rank = string.Empty;
            string RankTime = string.Empty;
            if (IQRankSystem)
                if (RankSettings.UseRankSystem)
                {
                    if (RankSettings.UseTimeStandart)
                        RankTime = $"{IQRankGetTimeGame(player.userID)}";
                    Rank = $"{IQRankGetRank(player.userID)}";
                }
            MessageSeparator = !String.IsNullOrWhiteSpace(Rank) && !String.IsNullOrWhiteSpace(RankTime) ? $"{ModifiedChannel} [{RankTime}] [{Rank}] {PrefxiPlayer} {ModifiedNick}: {ModifiedMessage}" : !String.IsNullOrWhiteSpace(RankTime) ? $"{ModifiedChannel} [{RankTime}] {PrefxiPlayer} {ModifiedNick}: {ModifiedMessage}" : !String.IsNullOrWhiteSpace(Rank) ? $"{ModifiedChannel} [{Rank}] {PrefxiPlayer} {ModifiedNick}: {ModifiedMessage}" : $"{ModifiedChannel} {PrefxiPlayer} {ModifiedNick}: {ModifiedMessage}";

            if (config.RustPlusSettings.UseRustPlus)
                if (channel == Chat.ChatChannel.Team)
                {
                    RelationshipManager.PlayerTeam Team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if (Team == null) return;
                    Util.BroadcastTeamChat(player.Team, player.userID, player.displayName, OutMessage, DataPlayer.MessageColor);
                }

            ReplyChat(channel, player, MessageSeparator);
            AnwserMessage(player, MessageSeparator.ToLower());
            Puts($"{player}: {OutMessage}");
            Log($"СООБЩЕНИЕ В ЧАТ : {player}: {ModifiedChannel} {OutMessage}");
            DiscordSendMessage(GetLang("DISCORD_SEND_LOG_CHAT", player.UserIDString, player.displayName, player.UserIDString, OutMessage, Message), config.OtherSetting.WebhooksChatLog, player.userID);

            RCon.Broadcast(RCon.LogType.Chat, new Chat.ChatEntry
            {
                Message = $"{player.displayName} : {OutMessage}",
                UserId = player.UserIDString,
                Username = player.displayName,
                Channel = channel,
                Time = (DateTime.UtcNow.Hour * 3600) + (DateTime.UtcNow.Minute * 60),
            });
        }

        public void AutoSetupData(string id, string perm)
        {
            var AutoSetup = config.AutoSetupSetting.SetupAutoSetting;
            if (String.IsNullOrWhiteSpace(id)) return;
            if (String.IsNullOrWhiteSpace(perm)) return;
            ulong userID;
            if (!ulong.TryParse(id, out userID)) return;

            if (!ChatSettingUser.ContainsKey(userID)) return;
            var DataPlayer = ChatSettingUser[userID];

            var Prefix = config.PrefixList.FirstOrDefault(x => x.Permissions == perm);
            var ColorChat = config.MessageColorList.FirstOrDefault(x => x.Permissions == perm);
            var ColorNick = config.NickColorList.FirstOrDefault(x => x.Permissions == perm);
            if (AutoSetup.UseSetupAutoPrefix)
                if (Prefix != null)
                {
                    if (!config.MessageSetting.MultiPrefix)
                        DataPlayer.ChatPrefix = Prefix.Argument;
                    else DataPlayer.MultiPrefix.Add(Prefix.Argument);

                    BasePlayer player = BasePlayer.FindByID(userID);
                    if (player != null)
                        ReplySystem(Chat.ChatChannel.Global, player, GetLang("PREFIX_SETUP", player.UserIDString, Prefix.Argument));
                }
            if (AutoSetup.UseSetupAutoColorChat)
                if (ColorChat != null)
                {
                    DataPlayer.MessageColor = ColorChat.Argument;

                    BasePlayer player = BasePlayer.FindByID(userID);
                    if (player != null)
                        ReplySystem(Chat.ChatChannel.Global, player, GetLang("COLOR_CHAT_SETUP", player.UserIDString, ColorChat.Argument));

                }
            if (AutoSetup.UseSetupAutoColorNick)
                if (ColorNick != null)
                {
                    DataPlayer.NickColor = ColorNick.Argument;

                    BasePlayer player = BasePlayer.FindByID(userID);
                    if (player != null)
                        ReplySystem(Chat.ChatChannel.Global, player, GetLang("COLOR_NICK_SETUP", player.UserIDString, ColorNick.Argument));
                }
        }
        public void CheckValidInformation(BasePlayer player)
        {
            var AutoReturn = config.AutoSetupSetting.ReturnDefaultSetting;
            var DataInformation = ChatSettingUser[player.userID];
            var PrefixList = config.PrefixList;
            var NickColorList = config.NickColorList;
            var ChatColorList = config.MessageColorList;

            if (config.MessageSetting.MultiPrefix)
            {
                foreach (var MyPrefix in DataInformation.MultiPrefix)
                    if (PrefixList.FirstOrDefault(x => x.Argument == MyPrefix) == null)
                        NextTick(() => { DataInformation.MultiPrefix.Remove(MyPrefix); });
            }
            else
            {
                if (PrefixList.FirstOrDefault(x => x.Argument == DataInformation.ChatPrefix) == null)
                    DataInformation.ChatPrefix = AutoReturn.PrefixDefault;
            }

            if (NickColorList.FirstOrDefault(x => x.Argument == DataInformation.NickColor) == null)
                DataInformation.NickColor = AutoReturn.NickDefault;

            if (ChatColorList.FirstOrDefault(x => x.Argument == DataInformation.MessageColor) == null)
                DataInformation.MessageColor = AutoReturn.MessageDefault;
        }
        public void AutoReturnDefaultData(string id, string perm)
        {
            var AutoReturn = config.AutoSetupSetting.ReturnDefaultSetting;
            if (String.IsNullOrWhiteSpace(id)) return;
            if (String.IsNullOrWhiteSpace(perm)) return;
            ulong userID;
            if (!ulong.TryParse(id, out userID)) return;
            if (!userID.IsSteamId()) return;
            if (!ChatSettingUser.ContainsKey(userID)) return;

            var DataPlayer = ChatSettingUser[userID];

            var Prefix = config.PrefixList.FirstOrDefault(x => x.Permissions == perm);
            var ColorChat = config.MessageColorList.FirstOrDefault(x => x.Permissions == perm);
            var ColorNick = config.NickColorList.FirstOrDefault(x => x.Permissions == perm);

            if (AutoReturn.UseDropPrefix)
                if (Prefix != null)
                {
                    if (config.MessageSetting.MultiPrefix)
                    {
                        if (DataPlayer.MultiPrefix.Contains(Prefix.Argument))
                        {
                            DataPlayer.MultiPrefix.Remove(Prefix.Argument);

                            BasePlayer player = BasePlayer.FindByID(userID);
                            if (player != null)
                                ReplySystem(Chat.ChatChannel.Global, player, GetLang("PREFIX_RETURNRED", player.UserIDString, Prefix.Argument));
                        }
                    }
                    else if (DataPlayer.ChatPrefix == Prefix.Argument)
                    {
                        DataPlayer.ChatPrefix = AutoReturn.PrefixDefault;

                        BasePlayer player = BasePlayer.FindByID(userID);
                        if (player != null)
                            ReplySystem(Chat.ChatChannel.Global, player, GetLang("PREFIX_RETURNRED", player.UserIDString, Prefix.Argument));
                    }
                }
            if (AutoReturn.UseDropColorChat)
                if (ColorChat != null)
                    if (DataPlayer.MessageColor == ColorChat.Argument)
                    {
                        DataPlayer.MessageColor = AutoReturn.MessageDefault;

                        BasePlayer player = BasePlayer.FindByID(userID);
                        if (player != null)
                        ReplySystem(Chat.ChatChannel.Global, player, GetLang("COLOR_CHAT_RETURNRED", player.UserIDString, ColorChat.Argument));

                    }
            if (AutoReturn.UseDropColorNick)
                if (ColorNick != null)
                    if (DataPlayer.NickColor == ColorNick.Argument)
                    {
                        DataPlayer.NickColor = AutoReturn.NickDefault;

                        BasePlayer player = BasePlayer.FindByID(userID);
                        if (player != null)
                            ReplySystem(Chat.ChatChannel.Global, player, GetLang("COLOR_NICK_RETURNRED", player.UserIDString, ColorNick.Argument));
                    }
        }   
        public void AnwserMessage(BasePlayer player, string Message)
        {
            var Anwser = config.AnswerMessages;
            if (!Anwser.UseAnswer) return;

            foreach (var Anwsers in Anwser.AnswerMessageList)
                if (Message.Contains(Anwsers.Key.ToLower()))
                    ReplySystem(Chat.ChatChannel.Global, player, Anwsers.Value);
        }
     
        public void BroadcastAuto()
        {
            var Alert = config.AlertSettings;
            if (Alert.AlertMessage)
            {
                int IndexBroadkastNow = 0;
                string RandomMsg = string.Empty;

                timer.Every(Alert.MessageListTimer, () =>
                 {
                     if (Alert.AlertMessageType)
                     {
                         if (IndexBroadkastNow >= Alert.MessageList.Count)
                             IndexBroadkastNow = 0;
                         RandomMsg = Alert.MessageList[IndexBroadkastNow++];
                     }
                     else RandomMsg = Alert.MessageList[UnityEngine.Random.Range(0, Alert.MessageList.Count)];
                     ReplyBroadcast(RandomMsg);
                 });
            }
        }
        private void MutePlayer(BasePlayer Target, MuteType Type, int ReasonIndex, BasePlayer Moderator = null, string ReasonCustom = "", int TimeCustom = 0, bool HideMute = false, bool Command = false)
        {
            var ReasonList = config.MuteControllers.ReasonListChat[ReasonIndex];
            string Reason = string.IsNullOrEmpty(ReasonCustom) ? ReasonList.Reason : ReasonCustom;
            int MuteTime = TimeCustom == 0 ? ReasonList.TimeMute : TimeCustom;
            string DisplayNameModerator = Moderator == null ? "Администратор" : Moderator.displayName;
            ulong IDModerator = Moderator == null ? 0 : Moderator.userID;

            if(Target == null || !Target.IsConnected)
            {
                if (Moderator != null && !Command)
                    AlertController(Moderator, "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_NOT_CONNNECTED");
                return;
            }

            switch (Type)
            {
                case MuteType.Chat:
                    {
                        if (Moderator != null && !Command)
                            if (IsMutedUser(Target.userID))
                            {
                                AlertController(Moderator, "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_ITSMUTED");
                                return;
                            }
                        
                        ChatSettingUser[Target.userID].MuteChatTime = MuteTime + CurrentTime();
                        if (!HideMute)
                            ReplyBroadcast(GetLang("FUNC_MESSAGE_MUTE_CHAT", Target.UserIDString, DisplayNameModerator, Target.displayName, FormatTime(TimeSpan.FromSeconds(MuteTime)), Reason));
                        else
                        {
                            if(Target != null)
                            ReplySystem(Chat.ChatChannel.Global, Target, GetLang("FUNC_MESSAGE_MUTE_CHAT", Target.UserIDString, DisplayNameModerator, Target.displayName, FormatTime(TimeSpan.FromSeconds(MuteTime)), Reason));
                            if(Moderator != null)
                            ReplySystem(Chat.ChatChannel.Global, Moderator, GetLang("FUNC_MESSAGE_MUTE_CHAT", Target.UserIDString, DisplayNameModerator, Target.displayName, FormatTime(TimeSpan.FromSeconds(MuteTime)), Reason));
                        }
                        if (Moderator != null)
                            if (Moderator != Target)
                                SetMute(Moderator);
                        DiscordSendMessage(GetLang("DISCORD_SEND_LOG_MUTE", Target.UserIDString, DisplayNameModerator, IDModerator, Target.displayName, Target.userID, Reason), config.OtherSetting.WebhooksMuteInfo);
                        break;
                    }
                case MuteType.Voice:
                    {
                        if (Moderator != null && !Command)
                            if (IsMutedVoiceUser(Target.userID))
                            {
                                AlertController(Moderator, "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_ITSMUTED");
                                return;
                            }
                        ChatSettingUser[Target.userID].MuteVoiceTime = MuteTime + CurrentTime();
                        if (!HideMute)
                            ReplyBroadcast(GetLang("FUNC_MESSAGE_MUTE_VOICE", Target.UserIDString, DisplayNameModerator, Target.displayName, FormatTime(TimeSpan.FromSeconds(MuteTime)), Reason));
                        else
                        {
                            if (Target != null)
                                ReplySystem(Chat.ChatChannel.Global, Target, GetLang("FUNC_MESSAGE_MUTE_VOICE", Target.UserIDString, DisplayNameModerator, Target.displayName, FormatTime(TimeSpan.FromSeconds(MuteTime)), Reason));
                            if (Moderator != null)
                                ReplySystem(Chat.ChatChannel.Global, Moderator, GetLang("FUNC_MESSAGE_MUTE_VOICE", Target.UserIDString, DisplayNameModerator, Target.displayName, FormatTime(TimeSpan.FromSeconds(MuteTime)), Reason));
                        }
                        break;
                    }
            }
            if (Moderator != null && !Command)
                AlertController(Moderator, "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_ACCESS");
        }
        private void UnmutePlayer(BasePlayer Target, MuteType Type, BasePlayer Moderator = null, bool HideUnmute = false, bool Command = false)
        {
            string DisplayNameModerator = Moderator == null ? "Администратор" : Moderator.displayName;
            switch (Type)
            {
                case MuteType.Chat:
                    {
                        ChatSettingUser[Target.userID].MuteChatTime = 0;
                        if (!HideUnmute)
                            ReplyBroadcast(GetLang("FUNC_MESSAGE_UNMUTE_CHAT", Target.UserIDString, DisplayNameModerator, Target.displayName));
                        else
                        {
                            if (Target != null)
                                ReplySystem(Chat.ChatChannel.Global, Target, GetLang("FUNC_MESSAGE_UNMUTE_CHAT", Target.UserIDString, DisplayNameModerator, Target.displayName));
                            if (Moderator != null)
                                ReplySystem(Chat.ChatChannel.Global, Moderator, GetLang("FUNC_MESSAGE_UNMUTE_CHAT", Target.UserIDString, DisplayNameModerator, Target.displayName));
                        }
                        break;
                    }
                case MuteType.Voice:
                    {
                        ChatSettingUser[Target.userID].MuteVoiceTime = 0;
                        if (!HideUnmute)
                            ReplyBroadcast(GetLang("FUNC_MESSAGE_UNMUTE_VOICE", Target.UserIDString, DisplayNameModerator, Target.displayName));
                        else
                        {
                            if (Target != null)
                                ReplySystem(Chat.ChatChannel.Global, Target, GetLang("FUNC_MESSAGE_UNMUTE_VOICE", Target.UserIDString, DisplayNameModerator, Target.displayName));
                            if (Moderator != null)
                                ReplySystem(Chat.ChatChannel.Global, Moderator, GetLang("FUNC_MESSAGE_UNMUTE_VOICE", Target.UserIDString, DisplayNameModerator, Target.displayName));

                        }
                        break;
                    }
            }
            if (Moderator != null && !Command)
                AlertController(Moderator, "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_ACCESS");
        }
        private void MuteAllChatPlayer(float TimeMute = 86400)
        {
            foreach (BasePlayer Target in BasePlayer.activePlayerList)
                ChatSettingUser[Target.userID].MuteChatTime = TimeMute + CurrentTime();
        }
        private void MuteAllVoicePlayer(float TimeMute = 86400)
        {
            foreach (BasePlayer Target in BasePlayer.activePlayerList)
                ChatSettingUser[Target.userID].MuteVoiceTime = TimeMute + CurrentTime();
        }
        private void UnMuteAllChatPlayer()
        {
            foreach (BasePlayer Target in BasePlayer.activePlayerList)
                ChatSettingUser[Target.userID].MuteChatTime = 0;
        }
        private void UnMuteAllVoicePlayer()
        {
            foreach (BasePlayer Target in BasePlayer.activePlayerList)
                ChatSettingUser[Target.userID].MuteVoiceTime = 0;
        }
        public void RenameFunc(BasePlayer player,string NewName)
        {
            if (permission.UserHasPermission(player.UserIDString, config.RenamePermission))
            {
                if (!AdminSetting.RenameList.ContainsKey(player.userID))
                    AdminSetting.RenameList.Add(player.userID, NewName);
                else AdminSetting.RenameList[player.userID] = NewName;
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_RENAME_SUCCES", player.UserIDString, NewName));
            }
            else ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_NOT_PERMISSION",player.UserIDString)); 
        }
        void AlertUI(BasePlayer player, string[] arg)
        {
            if (player != null)
                if (!player.IsAdmin) return;

            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("FUNC_MESSAGE_NO_ARG_BROADCAST", player.UserIDString));
                return;
            }
            string Message = "";
            foreach (var msg in arg)
                Message += " " + msg;

            foreach (BasePlayer p in BasePlayer.activePlayerList)
                UIAlert(p, Message);
        }
        void Alert(BasePlayer player, string[] arg, Boolean IsAdmin)
        {
            if (player != null)
                if (!player.IsAdmin) return;

            if (arg.Length == 0 || arg == null)
            {
                if(player != null)
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("FUNC_MESSAGE_NO_ARG_BROADCAST", player.UserIDString));
                return;
            }
            string Message = "";
            foreach (var msg in arg)
                Message += " " + msg;

            ReplyBroadcast(Message, "", "", IsAdmin);
            if (config.RustPlusSettings.UseRustPlus)
                foreach(var playerList in BasePlayer.activePlayerList)
                    NotificationList.SendNotificationTo(playerList.userID, NotificationChannel.SmartAlarm, config.RustPlusSettings.DisplayNameAlert, Message, Util.GetServerPairingData());
        }

        #endregion

        #region Interface
        private static string IQCHAT_PARENT_MAIN = "IQCHAT_PARENT_MAIN";
        private static string IQCHAT_PARENT_MAIN_CHAT_PANEL = "IQCHAT_PARENT_MAIN_CHAT_PANEL";
        private static string IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT = "IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT";
        private static string IQCHAT_PARENT_MAIN_CHAT_TITLE = "IQCHAT_PARENT_MAIN_CHAT_TITLE";
        private static string IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL = "IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL";
        private static string IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE = "IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE";
        private static string IQCHAT_PARENT_ALERT_UI = "IQCHAT_PARENT_ALERT_UI";
        private static string IQCHAT_PARENT_MAIN_IGNORED_UI = "IQCHAT_PARENT_MAIN_IGNORED_UI";
        private static string IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE = "IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE";

        #region Menu IQChat

        #region UI Menu

        private void IQChat_UI_Menu(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN);
            var Interface = config.InterfaceChat;
            var InterfacePosition = Interface.InterfacePositions;
            var PositionMainPanel = InterfacePosition.MainPanels;
            var PositionInformation = InterfacePosition.MainPanels.InfromationBlock;
            var PositionModeration = InterfacePosition.MainPanels.ModerationBlock;
            var PositionSettings = InterfacePosition.MainPanels.SettingBlock;
            var PositionSliderAndDropList = InterfacePosition.MainPanels.SliderAndDropListBlock;
            float FadeIn = 0.05f;
            var DataPlayer = ChatSettingUser[player.userID];

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, "Overlay", IQCHAT_PARENT_MAIN);

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN,
                Name = IQCHAT_PARENT_MAIN_CHAT_PANEL,
                Components =
                        {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = PositionMainPanel.BackgroundAnchorMin, AnchorMax = PositionMainPanel.BackgroundAnchorMax}, 
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "1.2 -1.2", UseGraphicAlpha = true }

                        }
            });

            container.Add(new CuiPanel 
            {
                RectTransform = { AnchorMin = PositionMainPanel.TitleAnchorMin, AnchorMax = PositionMainPanel.TitleAnchorMax },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexTitle) }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL, IQCHAT_PARENT_MAIN_CHAT_TITLE);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = PositionMainPanel.ContentAnchorMin, AnchorMax = PositionMainPanel.ContentAnchorMax },
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            },  IQCHAT_PARENT_MAIN_CHAT_PANEL, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = PositionMainPanel.CloseButtonAnchorMin, AnchorMax = PositionMainPanel.CloseButtonAnchorMax },
                Button = { FadeIn = FadeIn, Close = IQCHAT_PARENT_MAIN, Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/power.png" },
                Text = { FadeIn = FadeIn, Text = "" }
            }, IQCHAT_PARENT_MAIN_CHAT_TITLE, "CLOSE_BUTTON");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionMainPanel.TitleLabelAnchorMin, AnchorMax = PositionMainPanel.TitleLabelAnchorMax },
                Text = { FadeIn = FadeIn, Text = lang.GetMessage("UI_TITLE_PANEL_LABEL", this, player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_TITLE, "TITLE_LABEL");

            #region Information
            
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionInformation.TitleLabelAnchorMin, AnchorMax = PositionInformation.TitleLabelAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_TITLE",player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION");

            string FormatTimeMute = IsMutedUser(player.userID) ? FormatTime(TimeSpan.FromSeconds(DataPlayer.MuteChatTime - CurrentTime())) : GetLang("UI_CHAT_PANEL_INFORMATION_MUTED_TITLE_NOT",player.UserIDString);
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionInformation.MutedLabelAnchorMin, AnchorMax = PositionInformation.MutedLabelAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_MUTED_TITLE", player.UserIDString, FormatTimeMute), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION_MUTE_TIME");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionInformation.IgnoredLabelAnchorMin, AnchorMax = PositionInformation.IgnoredLabelAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_IGNORED_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION_IGNORED_TITLE");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = PositionInformation.IgnoredButtonAnchorMin, AnchorMax = PositionInformation.IgnoredButtonAnchorMax },
                Button = { FadeIn = FadeIn, Command = "iq.chat ignore.controller open", Color = HexToRustFormat(Interface.HexButton) },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_IGNORED_BUTTON_TITLE", player.UserIDString, DataPlayer.IgnoredUsers.Count), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter }
            },  IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION_IGNORED_TITLE_COUNT");

            #endregion

            #region Moderation Panel
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionModeration.TitleLabelAnchorMin, AnchorMax = PositionModeration.TitleLabelAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_MODERATOR_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_SETTINGS");

            if (permission.UserHasPermission(player.UserIDString, PermMuteMenu))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = PositionModeration.ButtonMuteAnchorMin, AnchorMax = PositionModeration.ButtonMuteAnchorMax },
                    Button = { FadeIn = FadeIn, Command = "iq.chat mute.controller open.menu", Color = HexToRustFormat(Interface.HexButton) },
                    Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_MODERATOR_MUTE_BUTTON_TITLE", player.UserIDString, DataPlayer.IgnoredUsers.Count), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter }
                }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_PANEL_MODERATION_BUTTON_MUTE");
            }

            #endregion

            #region User Interface

            #region Settings

            container.Add(new CuiLabel 
            {
                RectTransform = { AnchorMin = PositionSettings.TitleSettingAnchorMin, AnchorMax = PositionSettings.TitleSettingAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_SETTINGS");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionSettings.SettingPMLabelAnchorMin, AnchorMax = PositionSettings.SettingPMLabelAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_PM_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_SETTINGS_PM_TITLE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionSettings.SettingBroadcastAnchorMin, AnchorMax = PositionSettings.SettingBroadcastAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_BROADCAST_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "UI_CHAT_PANEL_SETTINGS_BROADCAST_TITLE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionSettings.SettingAlertAnchorMin, AnchorMax = PositionSettings.SettingAlertAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_ALERT_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleRight }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_SETTINGS_ALERT_TITLE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionSettings.SettingSoundAnchorMin, AnchorMax = PositionSettings.SettingSoundAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_SOUND_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleRight }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "UI_CHAT_PANEL_SETTINGS_SOUND_TITLE");

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT,
                Name = "TURNED_PM_BOX",
                Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = PositionSettings.BoxSettingPMLabelAnchorMin, AnchorMax = PositionSettings.BoxSettingPMLabelAnchorMax },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
            });

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT,
                Name = "TURNED_BROADCAST_BOX",
                Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = PositionSettings.BoxSettingBroadcastAnchorMin, AnchorMax = PositionSettings.BoxSettingBroadcastAnchorMax },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.5", UseGraphicAlpha = true }
                }
            });

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT,
                Name = "TURNED_SOUND_BOX",
                Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = PositionSettings.BoxSettingSoundAnchorMin, AnchorMax = PositionSettings.BoxSettingSoundAnchorMax },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.5", UseGraphicAlpha = true }
                }
            });

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT,
                Name = "TURNED_ALERT_BOX",
                Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = PositionSettings.BoxSettingAlertAnchorMin, AnchorMax = PositionSettings.BoxSettingAlertAnchorMax },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
            });
            
            #region Rank
            if (IQRankSystem)
                if (config.ReferenceSetting.IQRankSystems.UseRankSystem)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = PositionSliderAndDropList.TitleSliderAndDropListRankAnchorMin, AnchorMax = PositionSliderAndDropList.TitleSliderAndDropListRankAnchorMax },
                        Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_RANK_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
                    }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_RANK");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = PositionSliderAndDropList.SliderAndDropListRankAnchorMin, AnchorMax = PositionSliderAndDropList.SliderAndDropListRankAnchorMax },
                        Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) }
                    }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "RANK_TAKED_PANEL");
                }

            #endregion

            #region Chat

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionSliderAndDropList.TitleSliderAndDropListChatColorAnchorMin, AnchorMax = PositionSliderAndDropList.TitleSliderAndDropListChatColorAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_CHAT_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_CHAT");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = PositionSliderAndDropList.SliderAndDropListChatColorAnchorMin, AnchorMax = PositionSliderAndDropList.SliderAndDropListChatColorAnchorMax },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "CHAT_TAKED_PANEL");

            #endregion

            #region Nick

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionSliderAndDropList.TitleSliderAndDropListNickColorAnchorMin, AnchorMax = PositionSliderAndDropList.TitleSliderAndDropListNickColorAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_NICK_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_NICK");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = PositionSliderAndDropList.SliderAndDropListNickColorAnchorMin, AnchorMax = PositionSliderAndDropList.SliderAndDropListNickColorAnchorMax },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "NICKED_TAKED_PANEL");

            #endregion

            #region Prefix

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = PositionSliderAndDropList.TitleSliderAndDropListPrefixAnchorMin, AnchorMax = PositionSliderAndDropList.TitleSliderAndDropListPrefixAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_PREFIX_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_PREFIX");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = PositionSliderAndDropList.SliderAndDropListPrefixAnchorMin, AnchorMax = PositionSliderAndDropList.SliderAndDropListPrefixAnchorMax },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "PREFIX_TAKED_PANEL");

            #endregion

            #endregion

            #endregion

            CuiHelper.AddUi(player, container);
            UI_Prefix_Taked(player);
            UI_Nick_Taked(player);
            UI_Chat_Taked(player);
            if (IQRankSystem)
                if (config.ReferenceSetting.IQRankSystems.UseRankSystem)
                    UI_Rank_Taked(player);
            TurnedPM(player);
            TurnedAlert(player);
            TurnedBroadcast(player);
            TurnedSound(player);
            UpdateNick(player);
            if(player.IsAdmin)
            {
                TurnedAdminButtonMuteAllVoice(player);
                TurnedAdminButtonMuteAllChat(player);
            }
        }

        #endregion

        #region Controller Elements User

        #region Prefix

        private void UI_Prefix_Taked(BasePlayer player, int Index = 0, int Page = 0)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TITLE_PREFIX");
            CuiHelper.DestroyUi(player, "PREFIX_TAKED_PANEL_NEXT_BACK_BUTTON");
            CuiHelper.DestroyUi(player, "PREFIX_TAKED_PANEL_NEXT_BUTTON");
            CuiHelper.DestroyUi(player, "PREFIX_TAKED_PANEL_DROP_LIST");
            CuiHelper.DestroyUi(player, "PREFIX_TAKED_PANEL_DROP_LIST_BUTTON");

            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;
            var DataPlayer = ChatSettingUser[player.userID];
            var PrefixList = Interface.OtherSettingInterface.DropListPrefixUse ? config.PrefixList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).Skip(8 * Page).ToList() : config.PrefixList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).ToList();
            string Prefix = PrefixList == null || PrefixList.Count == 0 ? GetLang("UI_CHAT_PANEL_TAKE_PREFIX", player.UserIDString) : config.MessageSetting.MultiPrefix ? GetLang("UI_CHAT_PANEL_TAKE_PREFIX_MULTI", player.UserIDString) : PrefixList[Index].Argument;

            if (!config.MessageSetting.MultiPrefix && !config.InterfaceChat.OtherSettingInterface.DropListPrefixUse)
            {
                #region Page 
                string ColorBackPage = Index != 0 ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
                string CommandBackPage = Index != 0 ? $"iq.chat take.element.user {TakeElemntUser.Prefix} {Index - 1} 0" : "";

                string ColortNextPage = (PrefixList.Count() - 1) > Index ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
                string CommandNextPage = (PrefixList.Count() - 1) > Index ? $"iq.chat take.element.user {TakeElemntUser.Prefix} {Index + 1} 0" : "";

                container.Add(new CuiElement
                {
                    Parent = "PREFIX_TAKED_PANEL",
                    Name = "PREFIX_TAKED_PANEL_PAGE_BACK",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColorBackPage) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.009342605 0.09161124", AnchorMax = $"0.09342606 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.92" },
                    Button = { FadeIn = FadeIn, Command = CommandBackPage, Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = $"↩", Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter }
                }, "PREFIX_TAKED_PANEL_PAGE_BACK", "PREFIX_TAKED_PANEL_PAGE_BACK_BUTTON");

                container.Add(new CuiElement
                {
                    Parent = "PREFIX_TAKED_PANEL",
                    Name = "PREFIX_TAKED_PANEL_NEXT_PAGE",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColortNextPage) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.9062352 0.09161124", AnchorMax = $"0.9918757 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.92" },
                    Button = { FadeIn = FadeIn, Command = CommandNextPage, Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = $"↪", Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter }
                }, "PREFIX_TAKED_PANEL_NEXT_PAGE", "PREFIX_TAKED_PANEL_NEXT_BUTTON");

                #endregion
            }
            else
            {
                if (!config.MessageSetting.MultiPrefix)
                    CuiHelper.DestroyUi(player, "DROP_LIST_PANEL_ELEMENT");

                #region Drop List

                container.Add(new CuiElement
                {
                    Parent = "PREFIX_TAKED_PANEL",
                    Name = "PREFIX_TAKED_PANEL_DROP_LIST",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPageButtonActive) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.9062352 0.09161124", AnchorMax = $"0.9918757 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                TakeElemntUser PrefixController = Interface.OtherSettingInterface.DropListPrefixUse && !config.MessageSetting.MultiPrefix ? TakeElemntUser.Prefix : TakeElemntUser.MultiPrefix;
                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0.2181819 0.1923077", AnchorMax = $"0.8000001 0.8076923" },
                    Button = { FadeIn = FadeIn, Command = $"iq.chat take.element.droplist false {PrefixController}", Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/electric.png" },
                    Text = { FadeIn = FadeIn, Text = "" }
                }, "PREFIX_TAKED_PANEL_DROP_LIST", "PREFIX_TAKED_PANEL_DROP_LIST_BUTTON");

                #endregion
            }

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1307966 0", AnchorMax = "0.8501773 1" },
                Text = { FadeIn = FadeIn, Text = Prefix, Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), FontSize = 15, Align = TextAnchor.MiddleCenter }
            }, "PREFIX_TAKED_PANEL", "TITLE_PREFIX");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Nick

        private void UI_Nick_Taked(BasePlayer player, int Index = 0, int Page = 0)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TITLE_NICKED");
            CuiHelper.DestroyUi(player, "NICKED_TAKED_PANEL_NEXT_BACK_BUTTON");
            CuiHelper.DestroyUi(player, "NICKED_TAKED_PANEL_NEXT_BUTTON");

            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;
            var DataPlayer = ChatSettingUser[player.userID];
            var ColorList = Interface.OtherSettingInterface.DropListColorNickUse ? config.NickColorList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).Skip(8 * Page).ToList() : config.NickColorList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).ToList();
            string Color = ColorList == null || ColorList.Count == 0 ? GetLang("UI_CHAT_PANEL_TAKE_COLOR", player.UserIDString) : $"<color={ColorList[Index].Argument}>{player.displayName}</color>";

            if (!Interface.OtherSettingInterface.DropListColorNickUse)
            {
                #region Page 
                string ColorBackPage = Index != 0 ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
                string CommandBackPage = Index != 0 ? $"iq.chat take.element.user {TakeElemntUser.Nick} {Index - 1} 0" : "";

                string ColortNextPage = (ColorList.Count() - 1) > Index ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
                string CommandNextPage = (ColorList.Count() - 1) > Index ? $"iq.chat take.element.user {TakeElemntUser.Nick} {Index + 1} 0" : "";

                container.Add(new CuiElement
                {
                    Parent = "NICKED_TAKED_PANEL",
                    Name = "NICKED_TAKED_PANEL_PAGE_BACK",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColorBackPage) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.009342605 0.09161124", AnchorMax = $"0.09342606 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.92" },
                    Button = { FadeIn = FadeIn, Command = CommandBackPage, Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = $"↩", Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter }
                }, "NICKED_TAKED_PANEL_PAGE_BACK", "NICKED_TAKED_PANEL_PAGE_BACK_BUTTON");

                container.Add(new CuiElement
                {
                    Parent = "NICKED_TAKED_PANEL",
                    Name = "NICKED_TAKED_PANEL_NEXT_PAGE",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColortNextPage) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.9062352 0.09161124", AnchorMax = $"0.9918757 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.92" },
                    Button = { FadeIn = FadeIn, Command = CommandNextPage, Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = $"↪", Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter }
                }, "NICKED_TAKED_PANEL_NEXT_PAGE", "NICKED_TAKED_PANEL_NEXT_BUTTON");

                #endregion
            }
            else
            {
                CuiHelper.DestroyUi(player, "DROP_LIST_PANEL_ELEMENT");

                #region Drop List

                container.Add(new CuiElement
                {
                    Parent = "NICKED_TAKED_PANEL",
                    Name = "NICKED_TAKED_PANEL_DROP_LIST",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPageButtonActive) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.9062352 0.09161124", AnchorMax = $"0.9918757 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0.2181819 0.1923077", AnchorMax = $"0.8000001 0.8076923" },
                    Button = { FadeIn = FadeIn, Command = $"iq.chat take.element.droplist false {TakeElemntUser.Nick}", Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/electric.png" },
                    Text = { FadeIn = FadeIn, Text = "" }
                }, "NICKED_TAKED_PANEL_DROP_LIST", "NICKED_TAKED_PANEL_DROP_LIST_BUTTON");

                #endregion
            }

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1307966 0", AnchorMax = "0.8501773 1" },
                Text = { FadeIn = FadeIn, Text = Color, Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), FontSize = 15, Align = TextAnchor.MiddleCenter }
            }, "NICKED_TAKED_PANEL", "TITLE_NICKED");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Chat

        private void UI_Chat_Taked(BasePlayer player, int Index = 0, int Page = 0)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TITLE_CHAT");
            CuiHelper.DestroyUi(player, "CHAT_TAKED_PANEL_NEXT_BACK_BUTTON");
            CuiHelper.DestroyUi(player, "CHAT_TAKED_PANEL_NEXT_BUTTON");

            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;
            var DataPlayer = ChatSettingUser[player.userID];
            var ColorList = Interface.OtherSettingInterface.DropListColorChatUse ? config.MessageColorList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).Skip(8 * Page).ToList() : config.MessageColorList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).ToList();
            string Color = ColorList == null || ColorList.Count == 0 ? GetLang("UI_CHAT_PANEL_TAKE_COLOR", player.UserIDString) : $"<color={ColorList[Index].Argument}>я лучший</color>";

            if (!Interface.OtherSettingInterface.DropListColorChatUse)
            {
                #region Page 
                string ColorBackPage = Index != 0 ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
                string CommandBackPage = Index != 0 ? $"iq.chat take.element.user {TakeElemntUser.Chat} {Index - 1} 0" : "";

                string ColortNextPage = (ColorList.Count() - 1) > Index ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
                string CommandNextPage = (ColorList.Count() - 1) > Index ? $"iq.chat take.element.user {TakeElemntUser.Chat} {Index + 1} 0" : "";

                container.Add(new CuiElement
                {
                    Parent = "CHAT_TAKED_PANEL",
                    Name = "CHAT_TAKED_PANEL_PAGE_BACK",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColorBackPage) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.009342605 0.09161124", AnchorMax = $"0.09342606 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.92" },
                    Button = { FadeIn = FadeIn, Command = CommandBackPage, Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = $"↩", Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter }
                }, "CHAT_TAKED_PANEL_PAGE_BACK", "CHAT_TAKED_PANEL_PAGE_BACK_BUTTON");

                container.Add(new CuiElement
                {
                    Parent = "CHAT_TAKED_PANEL",
                    Name = "CHAT_TAKED_PANEL_NEXT_PAGE",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColortNextPage) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.9062352 0.09161124", AnchorMax = $"0.9918757 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.92" },
                    Button = { FadeIn = FadeIn, Command = CommandNextPage, Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = $"↪", Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter }
                }, "CHAT_TAKED_PANEL_NEXT_PAGE", "CHAT_TAKED_PANEL_NEXT_BUTTON");

                #endregion
            }
            else
            {
                CuiHelper.DestroyUi(player, "DROP_LIST_PANEL_ELEMENT");

                #region Drop List

                container.Add(new CuiElement
                {
                    Parent = "CHAT_TAKED_PANEL",
                    Name = "CHAT_TAKED_PANEL_DROP_LIST",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPageButtonActive) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.9062352 0.09161124", AnchorMax = $"0.9918757 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0.2181819 0.1923077", AnchorMax = $"0.8000001 0.8076923" },
                    Button = { FadeIn = FadeIn, Command = $"iq.chat take.element.droplist false {TakeElemntUser.Chat}", Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/electric.png" },
                    Text = { FadeIn = FadeIn, Text = "" }
                }, "CHAT_TAKED_PANEL_DROP_LIST", "CHAT_TAKED_PANEL_DROP_LIST_BUTTON");

                #endregion
            }

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1307966 0", AnchorMax = "0.8501773 1" },
                Text = { FadeIn = FadeIn, Text = Color, Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), FontSize = 15, Align = TextAnchor.MiddleCenter }
            }, "CHAT_TAKED_PANEL", "TITLE_CHAT");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Rank

        private void UI_Rank_Taked(BasePlayer player,int Index = 0, int Page = 0)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TITLE_RANK");
            CuiHelper.DestroyUi(player, "RANK_TAKED_PANEL_NEXT_BACK_BUTTON");
            CuiHelper.DestroyUi(player, "RANK_TAKED_PANEL_NEXT_BUTTON");

            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;
            var RankList = Interface.OtherSettingInterface.DropListRankUse ? IQRankListKey(player.userID).Skip(8 * Page).ToList() : IQRankListKey(player.userID);
            string Rank = RankList == null || RankList.Count == 0 ? GetLang("UI_CHAT_PANEL_TAKE_RANK", player.UserIDString) : IQRankGetNameRankKey(RankList[Index]);

            if (!Interface.OtherSettingInterface.DropListRankUse)
            {
                #region Page 
                string ColorBackPage = Index != 0 ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
                string CommandBackPage = Index != 0 ? $"iq.chat take.element.user {TakeElemntUser.Rank} {Index - 1} 0" : "";

                string ColortNextPage = (RankList.Count() - 1) > Index ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
                string CommandNextPage = (RankList.Count() - 1) > Index ? $"iq.chat take.element.user {TakeElemntUser.Rank} {Index + 1} 0" : "";

                container.Add(new CuiElement
                {
                    Parent = "RANK_TAKED_PANEL",
                    Name = "RANK_TAKED_PANEL_PAGE_BACK",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColorBackPage) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.009342605 0.09161124", AnchorMax = $"0.09342606 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.92" },
                    Button = { FadeIn = FadeIn, Command = CommandBackPage, Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = $"↩", Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter }
                }, "RANK_TAKED_PANEL_PAGE_BACK", "RANK_TAKED_PANEL_PAGE_BACK_BUTTON");

                container.Add(new CuiElement
                {
                    Parent = "RANK_TAKED_PANEL",
                    Name = "RANK_TAKED_PANEL_NEXT_PAGE",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(ColortNextPage) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.9062352 0.09161124", AnchorMax = $"0.9918757 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.92" },
                    Button = { FadeIn = FadeIn, Command = CommandNextPage, Color = "0 0 0 0" },
                    Text = { FadeIn = FadeIn, Text = $"↪", Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter }
                }, "RANK_TAKED_PANEL_NEXT_PAGE", "RANK_TAKED_PANEL_NEXT_BUTTON");

                #endregion
            }
            else
            {
                CuiHelper.DestroyUi(player, "DROP_LIST_PANEL_ELEMENT");

                #region Drop List

                string AcnhorMin = IQModalMenu ? $"0.9062352 0.06061124" : $"0.9062352 0.09061124";
                container.Add(new CuiElement
                {
                    Parent = "RANK_TAKED_PANEL",
                    Name = "RANK_TAKED_PANEL_DROP_LIST",
                    Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPageButtonActive) },
                        new CuiRectTransformComponent{ AnchorMin = AcnhorMin, AnchorMax = $"0.9918757 0.8855753" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
                });

                container.Add(new CuiButton
                {
                    FadeOut = 0.3f,
                    RectTransform = { AnchorMin = $"0.2181819 0.1923077", AnchorMax = $"0.8000001 0.8076923" },
                    Button = { FadeIn = FadeIn, Command = $"iq.chat take.element.droplist false {TakeElemntUser.Rank}", Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/electric.png" },
                    Text = { FadeIn = FadeIn, Text = "" }
                }, "RANK_TAKED_PANEL_DROP_LIST", "RANK_TAKED_PANEL_DROP_LIST_BUTTON");

                #endregion
            }

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1307966 0", AnchorMax = "0.8501773 1" },
                Text = { FadeIn = FadeIn, Text = Rank, Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), FontSize = 15, Align = TextAnchor.MiddleCenter }
            }, "RANK_TAKED_PANEL", "TITLE_RANK");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Drop List
        private void Take_DropList(BasePlayer player, bool IsOpened, TakeElemntUser Type, int Page = 0)
        {
            var Interface = config.InterfaceChat;
            string Parent = Type == TakeElemntUser.MultiPrefix || Type == TakeElemntUser.Prefix ? "PREFIX_TAKED_PANEL" : Type == TakeElemntUser.Nick ? "NICKED_TAKED_PANEL" : Type == TakeElemntUser.Chat ? "CHAT_TAKED_PANEL" : "RANK_TAKED_PANEL";
            string NameButton = Type == TakeElemntUser.MultiPrefix || Type == TakeElemntUser.Prefix ? "PREFIX_TAKED_PANEL_DROP_LIST" : Type == TakeElemntUser.Nick ? "NICKED_TAKED_PANEL_DROP_LIST" : Type == TakeElemntUser.Chat ? "CHAT_TAKED_PANEL_DROP_LIST" : "RANK_TAKED_PANEL_DROP_LIST";
            CuiHelper.DestroyUi(player, NameButton + "_BUTTON");

            CuiElementContainer buttonDrop = new CuiElementContainer();
            string Command = IsOpened ? $"iq.chat take.element.droplist false {Type}" : $"iq.chat take.element.droplist true {Type}";

            buttonDrop.Add(new CuiButton
            {
                FadeOut = 0.3f,
                RectTransform = { AnchorMin = $"0.2181819 0.1923077", AnchorMax = $"0.8000001 0.8076923" },
                Button = { Command = Command, Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/electric.png" },
                Text = { Text = "" }
            }, NameButton, NameButton + "_BUTTON");

            CuiHelper.AddUi(player, buttonDrop);

            CuiHelper.DestroyUi(player, "DROP_LIST_PANEL_ELEMENT");
            if (IsOpened) return;

            CuiElementContainer container = new CuiElementContainer();
            string OffsetMax = IQModalMenu ? "224.5 0" : "215.5 0";
            string OffsetMaxPage = IQModalMenu ? "222.3 0" : "214 -3";

            container.Add(new CuiElement
            {
                Parent = Parent,
                Name = "DROP_LIST_PANEL_ELEMENT",
                Components =
                {
                        new CuiImageComponent { Color = HexToRustFormat(Interface.HexPageButtonActive) },
                        new CuiRectTransformComponent{ AnchorMin = $"0 0", AnchorMax = $"0 0", OffsetMin = "1.5 -90", OffsetMax = OffsetMax },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.5 1.2", UseGraphicAlpha = false }
                }
            });

            container.Add(new CuiElement
            {
                Parent = "DROP_LIST_PANEL_ELEMENT",
                Name = "PAGE_PANEL_CONTROLLER",
                Components =
                {
                        new CuiImageComponent { Color = HexToRustFormat(Interface.HexPageButtonActive) },
                        new CuiRectTransformComponent{ AnchorMin = $"0 0", AnchorMax = $"0 0", OffsetMin = "0 -27", OffsetMax = OffsetMaxPage },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.5 1.2", UseGraphicAlpha = false }
                }
            });

            CuiHelper.AddUi(player, container);
            LoadedElementList(player, Type, Page);
        }

        void LoadedElementList(BasePlayer player, TakeElemntUser Type, int Page = 0)
        {
            for(int i = 0; i < 8; i++)
                CuiHelper.DestroyUi(player, $"ELEMENT_{i}");
            
            var Interface = config.InterfaceChat;
            var DataPlayer = ChatSettingUser[player.userID];
            CuiElementContainer container = new CuiElementContainer();

            var ElementList = Type == TakeElemntUser.Prefix || Type == TakeElemntUser.MultiPrefix ? config.PrefixList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).Skip(8 * Page).ToList()
                            : Type == TakeElemntUser.Chat ? config.MessageColorList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).Skip(8 * Page).ToList()
                            : Type == TakeElemntUser.Nick ? config.NickColorList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).Skip(8 * Page).ToList() : new List<Configuration.AdvancedFuncion>();

            if (Type == TakeElemntUser.Rank)
            {
                var RankList = IQRankListKey(player.userID).Skip(8 * Page);
                int x = 0, y = 0, i = 0;
                foreach (var Element in RankList)
                {
                    string Rank = IQRankGetNameRankKey(Element);
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{0 + (x * 0.504)} {0.752 - (y * 0.245)}", AnchorMax = $"{0.495 + (x * 0.504)} {0.975 - (y * 0.245)}" },
                        Button = { Command = $"iq.chat take.element.user {Type} {i} {Page}", Color = HexToRustFormat(Interface.HexButton) },
                        Text = { Text = Rank, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), FontSize = 10 }
                    }, "DROP_LIST_PANEL_ELEMENT", $"ELEMENT_{i}");

                    if (i >= 7) break;
                    x++; i++;
                    if (x == 2)
                    {
                        y++;
                        x = 0;
                    }
                }
            }
            else
            {
                int x = 0, y = 0, i = 0;
                foreach (var Element in ElementList)
                {
                    string DisplayElement = Type == TakeElemntUser.Chat ? $"<b><color={Element.Argument}> я лучший</color></b>" : Type == TakeElemntUser.Nick ? $"<color={Element.Argument}>{player.displayName}</color>" : Element.Argument;
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{0 + (x * 0.504)} {0.752 - (y * 0.245)}", AnchorMax = $"{0.495 + (x * 0.504)} {0.975 - (y * 0.245)}" },
                        Button = { Command = $"iq.chat take.element.user {Type} {i} {Page}", Color = HexToRustFormat(Interface.HexButton) },
                        Text = { Text = DisplayElement, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), FontSize = 10 }
                    }, "DROP_LIST_PANEL_ELEMENT", $"ELEMENT_{i}");

                    if (DataPlayer.MultiPrefix.Contains(Element.Argument) && config.MessageSetting.MultiPrefix)
                    {
                        container.Add(new CuiPanel
                        {
                            RectTransform = { AnchorMin = "0.03763337 0.1", AnchorMax = "0.1881546 0.8883512" },
                            Image = { Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/vote_up.png" }
                        }, $"ELEMENT_{i}");
                    }

                    if (i >= 7) break;
                    x++; i++;
                    if (x == 2)
                    {
                        y++;
                        x = 0;
                    }
                }
            }

            CuiHelper.AddUi(player, container);
            LoadPage(player, Type, Type == TakeElemntUser.Rank ? IQRankListKey(player.userID).Count : ElementList.Count, Page);
        }

        void LoadPage(BasePlayer player, TakeElemntUser Type, int ElementCount, int Page = 0)
        {
            CuiHelper.DestroyUi(player, "PAGE_PANEL_CONTROLLER_BUTTON_BACK");
            CuiHelper.DestroyUi(player, "BUTTON_BACK_ELEMENT");
            CuiHelper.DestroyUi(player, "COUNT_PAGE");
            CuiHelper.DestroyUi(player, "PAGE_PANEL_CONTROLLER_BUTTON_NEXT");
            CuiHelper.DestroyUi(player, "BUTTON_NEXT_ELEMENT");

            var Interface = config.InterfaceChat;
            CuiElementContainer container = new CuiElementContainer();

            string CommandBackPage = Page != 0 ? $"iq.chat drop.list.page.controller back {Type} {Page}" : "";
            string ColorBackPage = Page != 0 ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
            string ColortNextPage = ElementCount > 8 ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
            string CommandNextPage = ElementCount > 8 ? $"iq.chat drop.list.page.controller next {Type} {Page}" : "";

            container.Add(new CuiElement
            {
                Parent = "PAGE_PANEL_CONTROLLER",
                Name = "PAGE_PANEL_CONTROLLER_BUTTON_BACK",
                Components =
                {
                        new CuiImageComponent { Color = HexToRustFormat(ColorBackPage) },
                        new CuiRectTransformComponent{ AnchorMin = $"0 0", AnchorMax = $"0.1114139 0.95" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.92" },
                Button = { Command = CommandBackPage, Color = "0 0 0 0" },
                Text = { Text = $"↩", Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter }
            }, "PAGE_PANEL_CONTROLLER_BUTTON_BACK", "BUTTON_BACK_ELEMENT");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1114139 0", AnchorMax = "0.8913107 1" },
                Text = { Text = GetLang("UI_CHAT_PAGE_CONTROLLER_DROP_LIST_COUNT", player.UserIDString, Page.ToString()), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter }
            }, "PAGE_PANEL_CONTROLLER", "COUNT_PAGE"); 

            container.Add(new CuiElement
            {
                Parent = "PAGE_PANEL_CONTROLLER",
                Name = "PAGE_PANEL_CONTROLLER_BUTTON_NEXT",
                Components =
                {
                        new CuiImageComponent { Color = HexToRustFormat(ColortNextPage) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.8885861 0", AnchorMax = $"1 0.95" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.92" },
                Button = { Command = CommandNextPage, Color = "0 0 0 0" },
                Text = { Text = $"↪", Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", FontSize = 15, Align = TextAnchor.MiddleCenter }
            }, "PAGE_PANEL_CONTROLLER_BUTTON_NEXT", "BUTTON_NEXT_ELEMENT");

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #endregion

        #region UI Mute Menu

        void IQChat_UI_Mute_Menu(BasePlayer player)
        { 
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL);
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN_IGNORED_UI);
            var Interface = config.InterfaceChat;
            var InterfacePosition = config.InterfaceChat.InterfacePositions.MutePanels;
            float FadeIn = 0.3f;
            string Parent = IQModalMenu ? IQMODAL_CONTENT : IQCHAT_PARENT_MAIN;
            string AnchorMin = IQModalMenu ? "0.001 0" : InterfacePosition.BackgroundAnchorMin;
            string AcnhorMax = IQModalMenu ? "0.997 1" : InterfacePosition.BackgroundAnchorMax;

            container.Add(new CuiElement
            {
                Parent = Parent,
                Name = IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL,
                Components =
                        {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = AnchorMin, AnchorMax = AcnhorMax },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "1.2 -1.2", UseGraphicAlpha = true }

                        }
            });

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL,
                Name = IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE,
                Components =
                        {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexTitle) },
                        new CuiRectTransformComponent{ AnchorMin = "0.001 0.8896463", AnchorMax = "0.999 1"  },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexPanel), Distance = "1.2 -1.2", UseGraphicAlpha = true }

                        }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.9492017 0.31899254", AnchorMax = "0.9750086 0.6709784" }, 
                Button = { FadeIn = FadeIn, Close = IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL, Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/exit.png" },
                Text = { FadeIn = FadeIn, Text = "" }
            }, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE, "CLOSE_BUTTON_MUTE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.374989 0", AnchorMax = "0.93870254 1" }, 
                Text = { FadeIn = FadeIn, Text = lang.GetMessage("UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TITLE", this, player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleRight }
            }, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE, "TITLE_MUTE_LABEL");

            #region Input Search

            string SearchName = "";

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.01531807 0.31899", AnchorMax = "0.04112639 0.6709784" },
                Button = { FadeIn = FadeIn, Command = $"iq.chat mute.controller search.player {SearchName}", Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/examine.png" },
                Text = { FadeIn = FadeIn, Text = "" }
            }, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE, "SEARCH_BUTTON");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.04677023 0.31899", AnchorMax = "0.2685355 0.6709784" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE, "INPUT_PANEL");

            container.Add(new CuiElement
            {
                Parent = "INPUT_PANEL",
                Name = "INPUT_ELEMENT",
                Components =
                {
                    new CuiInputFieldComponent { Text = SearchName, FontSize = 14,Command = $"iq.chat mute.controller search.player {SearchName}", Align = TextAnchor.MiddleLeft, Color = HexToRustFormat(Interface.HexLabel), CharsLimit = 25},
                    new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "1 1" }
                }
            }); 

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2725679 0", AnchorMax = "0.3588576 1" },
                Button = { FadeIn = FadeIn, Command = $"iq.chat mute.controller search.player {SearchName}", Color = "0 0 0 0"},
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TITLE_SEARCH", player.UserIDString), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
            }, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE, "SEARCH_BUTTON_TITLE");

            #endregion

            CuiHelper.AddUi(player, container);

            Loaded_Players_Mute_Menu(player);
        }

        void Loaded_Players_Mute_Menu(BasePlayer player, int Page = 0, string Search = "")
        {
            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TITLE_PAGE_ACTIVE");
            CuiHelper.DestroyUi(player, "BUTTON_PAGE_NEXT");
            CuiHelper.DestroyUi(player, "BUTTON_PAGE_BACK");
            CuiHelper.DestroyUi(player, "IQ_CHAT_MUTE_PANEL_CONTENT");

            string ColorBackPage = Page != 0 ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
            string CommandBackPage = Page != 0 ? $"iq.chat mute.controller page.controller back {Page} {Search}" : ""; 

            string ColortNextPage = BasePlayer.activePlayerList.Skip(70 * (Page + 1)).Count() > 0 ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
            string CommandNextPage = BasePlayer.activePlayerList.Skip(70 * (Page + 1)).Count() > 0 ? $"iq.chat mute.controller page.controller next {Page} {Search}" : "";

            #region Page Mute Players

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.3975857 0.31899254", AnchorMax = "0.4233935 0.6709784" }, 
                Text = { FadeIn = FadeIn, Text = Page.ToString(), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter, FontSize = 16 }
            }, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE, "TITLE_PAGE_ACTIVE");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.4274257 0.3189905", AnchorMax = "0.453233 0.6709783" },
                Button = { FadeIn = FadeIn, Command = CommandNextPage, Color = HexToRustFormat(ColortNextPage) },
                Text = { FadeIn = FadeIn, Text = "<b>></b>", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat(Interface.HexLabel)}
            }, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE, "BUTTON_PAGE_NEXT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3677455 0.31899", AnchorMax = "0.3935539 0.6709784" },
                Button = { FadeIn = FadeIn, Command = CommandBackPage, Color = HexToRustFormat(ColorBackPage) },
                Text = { FadeIn = FadeIn, Text = "<b><</b>", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat(Interface.HexLabel) }
            }, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL_TITLE, "BUTTON_PAGE_BACK");

            #endregion

            #region Players

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.8896463" },
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL, "IQ_CHAT_MUTE_PANEL_CONTENT");

            int x = 0, y = 0, i = 0;
            foreach(BasePlayer PlayerList in BasePlayer.activePlayerList.Where(p => p.displayName.ToLower().Contains(Search.ToLower())).Skip(70 * Page).OrderByDescending(p => IsMutedUser(p.userID)))
            {
                string Command = $"iq.chat mute.controller take.user take {PlayerList.userID}";
                container.Add(new CuiElement
                {
                    Parent = "IQ_CHAT_MUTE_PANEL_CONTENT",
                    Name = $"PLAYER_PANEL_{i}",
                    Components =
                        {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) },
                        new CuiRectTransformComponent{ AnchorMin = $"{0.0120794 + (x * 0.1987)} {0.9345078 - (y * 0.07)}", AnchorMax = $"{0.1903083 + (x * 0.1987)} {0.983627 - (y * 0.07)}" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "1.2 -1.2", UseGraphicAlpha = true }

                        }
                });
                string Indication = IsMutedUser(PlayerList.userID) ? "#cd4632" : Interface.HexTitle;
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0.0191", AnchorMax = "0.02041666 0.955" },
                    Image = { FadeIn = FadeIn, Color = HexToRustFormat(Indication) }
                }, $"PLAYER_PANEL_{i}", $"PLAYER_LINE_{i}");

                string FormatNick = PlayerList.displayName.Length > 23 ? $"{PlayerList.displayName.Substring(0, 23)}..." : PlayerList.displayName;
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.05687201 0", AnchorMax = "0.9638011 1" },
                    Button = { FadeIn = FadeIn, Color = "0 0 0 0", Command = Command },
                    Text = { FadeIn = FadeIn, Text = FormatNick, FontSize = 12, Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleLeft }
                }, $"PLAYER_PANEL_{i}", $"BUTTON_MUTE_PLAYER_{i}");

                i++;
                if (i == 70) break;
                x++;
                if (x == 5)
                {
                    x = 0;
                    y++;
                }
            }

            #endregion

            CuiHelper.AddUi(player, container);
        }

        #region Mute Take User

        void Take_User_Mute_Menu(BasePlayer player, ulong TargetUserID)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "IQ_MUTE_BLUR_CONTENT");
            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;
            string ChatType = IsMutedUser(TargetUserID) ? "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_UNMUTE" : "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT";
            string VoiceType = IsMutedVoiceUser(TargetUserID) ? "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_VOICE_UNMUTE" : "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_VOICE";
            string CommandChatType = IsMutedUser(TargetUserID) ? $"iq.chat mute.controller take.user user.action {TargetUserID} unmute {MuteType.Chat}" : $"iq.chat mute.controller take.user take.type {MuteType.Chat} {TargetUserID}"; 
            string CommandVoiceType = IsMutedVoiceUser(TargetUserID) ? $"iq.chat mute.controller take.user user.action {TargetUserID} unmute {MuteType.Voice}" : $"iq.chat mute.controller take.user take.type {MuteType.Voice} {TargetUserID}";

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0", OffsetMin = "0 0" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexBlurMute), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "IQ_CHAT_MUTE_PANEL_CONTENT", "IQ_MUTE_BLUR_CONTENT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn, Close = "IQ_MUTE_BLUR_CONTENT", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn, Text = "" }
            }, "IQ_MUTE_BLUR_CONTENT", "CLOSE_BUTTON_MUTE_BLUR_CONTENT");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.2 0.8526422", AnchorMax = "0.8 0.9249566" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_MUTE_TITLE", player.UserIDString), Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter}
            }, "IQ_MUTE_BLUR_CONTENT", "CLOSE_BUTTON_MUTE_BLUR_CONTENT_TYPE_TITLE");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3 0.7748709", AnchorMax = "0.7 0.8471852" },
                Button = { FadeIn = FadeIn, Command = CommandChatType, Color = HexToRustFormat(Interface.HexButton) },
                Text = { FadeIn = FadeIn, Text = GetLang(ChatType, player.UserIDString), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
            }, "IQ_MUTE_BLUR_CONTENT", "BUTTON_TAKE_TYPE_CHAT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3 0.6970995", AnchorMax = "0.7 0.7694138" },
                Button = { FadeIn = FadeIn, Command = CommandVoiceType, Color = HexToRustFormat(Interface.HexButton) },
                Text = { FadeIn = FadeIn, Text = GetLang(VoiceType, player.UserIDString), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
            }, "IQ_MUTE_BLUR_CONTENT", "BUTTON_TAKE_TYPE_VOICE");

            CuiHelper.AddUi(player, container);
        }

        void Take_Reason_Mute_Menu(BasePlayer player, MuteType Type, ulong TargetUserID)
        {
            CuiElementContainer container = new CuiElementContainer();
            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.2 0.6179637", AnchorMax = "0.8 0.6902781" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_REASON_MUTE_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter }
            }, "IQ_MUTE_BLUR_CONTENT", "CLOSE_BUTTON_MUTE_BLUR_CONTENT_REASON_TITLE");

            for(int i = 0; i < config.MuteControllers.ReasonListChat.Count; i++)
            {
                var ReasonInfo = config.MuteControllers.ReasonListChat[i];
                string Command = $"iq.chat mute.controller take.user user.action {TargetUserID} mute {Type} {i}"; 
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.3 {0.5442856 - (i * 0.077)}", AnchorMax = $"0.7 {0.6165999 - (i * 0.077)}" },
                    Button = { FadeIn = FadeIn, Command = Command, Color = HexToRustFormat(Interface.HexButton) },
                    Text = { FadeIn = FadeIn, Text = $"{ReasonInfo.Reason} - {FormatTime(TimeSpan.FromSeconds(ReasonInfo.TimeMute))}", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, "IQ_MUTE_BLUR_CONTENT", "BUTTON_TAKE_TYPE_VOICE");
            }

            CuiHelper.AddUi(player, container);
        }

        void AlertController(BasePlayer player, string LangKey, bool MuteOrIgnored = true)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "IQ_IGNORED_BLUR_CONTENT");
            CuiHelper.DestroyUi(player, "IQ_MUTE_BLUR_CONTENT");
            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;

            string Message = GetLang(LangKey, player.UserIDString);
            string Parent = MuteOrIgnored ? "IQ_CHAT_MUTE_PANEL_CONTENT" : "IQ_CHAT_IGNORED_PANEL_CONTENT";
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0", OffsetMin = "0 0" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexBlurMute), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            },  Parent, "IQ_MUTE_BLUR_CONTENT");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { FadeIn = FadeIn, Text = Message, Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
            }, "IQ_MUTE_BLUR_CONTENT", "MUTE_ACCESS_LABEL_INFO");

            timer.Once(3f, () => {
                if (MuteOrIgnored)
                    Loaded_Players_Mute_Menu(player);
                else UI_Ignored_Loaded_Players(player);
            });

            CuiHelper.AddUi(player, container);
        }


        #endregion

        #endregion

        #region UI Ignored Menu

        private void UI_Ignored_Menu(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL);
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE);
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN_IGNORED_UI);
            var Interface = config.InterfaceChat;
            var InterfacePosition = Interface.InterfacePositions.IgnoredPanels;
            float FadeIn = 0.3f;

            string Parent = IQModalMenu ? IQMODAL_CONTENT : IQCHAT_PARENT_MAIN;
            string AnchorMin = IQModalMenu ? "0.001 0" : InterfacePosition.BackgroundAnchorMin;
            string AcnhorMax = IQModalMenu ? "0.997 1" : InterfacePosition.BackgroundAnchorMax;

            container.Add(new CuiElement
            {
                Parent = Parent,
                Name = IQCHAT_PARENT_MAIN_IGNORED_UI,
                Components =
                        {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = AnchorMin, AnchorMax = AcnhorMax },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "1.2 -1.2", UseGraphicAlpha = true }

                        }
            });

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_IGNORED_UI,
                Name = IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE,
                Components =
                        {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexTitle) },
                        new CuiRectTransformComponent{ AnchorMin = "0.001 0.8896463", AnchorMax = "0.999 1"  },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexPanel), Distance = "1.2 -1.2", UseGraphicAlpha = true }

                        }
            });


            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.9492017 0.31899", AnchorMax = "0.9750086 0.6709784" },
                Button = { FadeIn = FadeIn, Close = IQCHAT_PARENT_MAIN_IGNORED_UI, Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/exit.png" },
                Text = { FadeIn = FadeIn, Text = "" }
            }, IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE, "CLOSE_BUTTON_IGNORED");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.374989 0", AnchorMax = "0.9387087 1" },
                Text = { FadeIn = FadeIn, Text = lang.GetMessage("UI_CHAT_PANEL_IGNORED_MENU_TITLE", this, player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleRight }
            }, IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE, "TITLE_IGNORED_LABEL");

            #region Input Search

            string SearchName = "";

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.01531807 0.31899", AnchorMax = "0.04112639 0.6709784" },
                Button = { FadeIn = FadeIn, Command = $"iq.chat ignore.controller search.player {SearchName}", Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/examine.png" },
                Text = { FadeIn = FadeIn, Text = "" }
            }, IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE, "SEARCH_BUTTON");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.04677023 0.31899", AnchorMax = "0.2685355 0.6709784" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE, "INPUT_PANEL");

            container.Add(new CuiElement
            {
                Parent = "INPUT_PANEL",
                Name = "INPUT_ELEMENT",
                Components =
                {
                    new CuiInputFieldComponent { Text = SearchName, FontSize = 14,Command = $"iq.chat ignore.controller search.player {SearchName}", Align = TextAnchor.MiddleLeft, Color = HexToRustFormat(Interface.HexLabel), CharsLimit = 25},
                    new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "1 1" }
                }
            });

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.2725679 0", AnchorMax = "0.3588576 1" },
                Button = { FadeIn = FadeIn, Command = $"iq.chat ignore.controller search.player {SearchName}", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TITLE_SEARCH", player.UserIDString), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
            }, IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE, "SEARCH_BUTTON_TITLE");

            #endregion

            CuiHelper.AddUi(player, container);

            UI_Ignored_Loaded_Players(player);
        }

        private void UI_Ignored_Loaded_Players(BasePlayer player, int Page = 0, string Search = "")
        {
            var ChatUser = ChatSettingUser[player.userID];
            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TITLE_PAGE_ACTIVE");
            CuiHelper.DestroyUi(player, "BUTTON_PAGE_NEXT");
            CuiHelper.DestroyUi(player, "BUTTON_PAGE_BACK");
            CuiHelper.DestroyUi(player, "IQ_CHAT_IGNORED_PANEL_CONTENT");

            string ColorBackPage = Page != 0 ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
            string CommandBackPage = Page != 0 ? $"iq.chat ignore.controller page.controller back {Page} {Search}" : "";

            string ColortNextPage = BasePlayer.activePlayerList.Skip(70 * (Page + 1)).Count() > 0 ? Interface.HexPageButtonActive : Interface.HexPageButtonInActive;
            string CommandNextPage = BasePlayer.activePlayerList.Skip(70 * (Page + 1)).Count() > 0 ? $"iq.chat ignore.controller page.controller next {Page} {Search}" : "";

            #region Page Ignored Players

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.3975857 0.31899", AnchorMax = "0.4233935 0.6709784" },
                Text = { FadeIn = FadeIn, Text = Page.ToString(), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter, FontSize = 16 }
            }, IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE, "TITLE_PAGE_ACTIVE");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.4274257 0.3189905", AnchorMax = "0.453233 0.6709783" },
                Button = { FadeIn = FadeIn, Command = CommandNextPage, Color = HexToRustFormat(ColortNextPage) },
                Text = { FadeIn = FadeIn, Text = "<b>></b>", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat(Interface.HexLabel) }
            }, IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE, "BUTTON_PAGE_NEXT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.3677455 0.31899", AnchorMax = "0.3935539 0.6709784" },
                Button = { FadeIn = FadeIn, Command = CommandBackPage, Color = HexToRustFormat(ColorBackPage) },
                Text = { FadeIn = FadeIn, Text = "<b><</b>", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = HexToRustFormat(Interface.HexLabel) }
            }, IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE, "BUTTON_PAGE_BACK");

            #endregion

            #region Players

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.8896463" },
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, IQCHAT_PARENT_MAIN_IGNORED_UI, "IQ_CHAT_IGNORED_PANEL_CONTENT");

            int x = 0, y = 0, i = 0;
            foreach (BasePlayer PlayerList in BasePlayer.activePlayerList.Where(p => p.displayName.ToLower().Contains(Search.ToLower())).Skip(70 * Page).OrderByDescending(p => IsIgnored(player.userID, p.userID)))
            {
                string Command = $"iq.chat ignore.controller take.user take {PlayerList.userID}";
                container.Add(new CuiElement
                {
                    Parent = "IQ_CHAT_IGNORED_PANEL_CONTENT",
                    Name = $"PLAYER_PANEL_{i}",
                    Components =
                        {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) },
                        new CuiRectTransformComponent{ AnchorMin = $"{0.0120794 + (x * 0.1987)} {0.9345078 - (y * 0.07)}", AnchorMax = $"{0.1903083 + (x * 0.1987)} {0.983627 - (y * 0.07)}" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "1.2 -1.2", UseGraphicAlpha = true }

                        }
                });

                string Indication = IsIgnored(player.userID,PlayerList.userID) ? "#cd4632" : Interface.HexTitle;
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0.0191", AnchorMax = "0.02041666 0.955" },
                    Image = { FadeIn = FadeIn, Color = HexToRustFormat(Indication) }
                }, $"PLAYER_PANEL_{i}", $"PLAYER_LINE_{i}");            

                string FormatNick = PlayerList.displayName.Length > 23 ? $"{PlayerList.displayName.Substring(0, 23)}..." : PlayerList.displayName;
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.05687201 0", AnchorMax = "0.9638011 1" },
                    Button = { FadeIn = FadeIn, Color = "0 0 0 0", Command = Command },
                    Text = { FadeIn = FadeIn, Text = FormatNick, FontSize = 12, Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleLeft }
                }, $"PLAYER_PANEL_{i}", $"BUTTON_IGNORED_PLAYER_{i}");

                i++;
                if (i == 70) break;
                x++;
                if (x == 5)
                {
                    x = 0;
                    y++;
                }
            }

            #endregion

            CuiHelper.AddUi(player, container);
        }

        #region UI Ignored Alert

        void Take_User_Ignored_Menu(BasePlayer player, ulong TargetUserID)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "IQ_IGNORED_BLUR_CONTENT");
            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;

            string AlertMessage = !IsIgnored(player.userID, TargetUserID) ? GetLang("UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_ADD_IGNORED", player.UserIDString, BasePlayer.FindByID(TargetUserID).displayName.ToUpper()) : GetLang("UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_REMOVE_IGNORED", player.UserIDString, BasePlayer.FindByID(TargetUserID).displayName.ToUpper());

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0", OffsetMin = "0 0" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexBlurMute), Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" }
            }, "IQ_CHAT_IGNORED_PANEL_CONTENT", "IQ_IGNORED_BLUR_CONTENT");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Button = { FadeIn = FadeIn, Close = "IQ_IGNORED_BLUR_CONTENT", Color = "0 0 0 0" },
                Text = { FadeIn = FadeIn, Text = "" }
            }, "IQ_IGNORED_BLUR_CONTENT", "CLOSE_BUTTON_IGNORED_BLUR_CONTENT");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.7216589", AnchorMax = "1 0.916771" },
                Text = { FadeIn = FadeIn, Text = AlertMessage, Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter }
            }, "IQ_IGNORED_BLUR_CONTENT", "CLOSE_BUTTON_MUTE_BLUR_CONTENT_TYPE_TITLE");
            
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.08385485 0.6206912", AnchorMax = "0.4749908 0.6930054" },
                Button = { FadeIn = FadeIn, Command = $"iq.chat ignore.controller take.user user.confirm {TargetUserID} yes", Color = HexToRustFormat(Interface.HexButton) },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_YES_BUTTON", player.UserIDString), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
            }, "IQ_IGNORED_BLUR_CONTENT", "BUTTON_ALERT_IGNORED_YES");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.5250092 0.6206912", AnchorMax = "0.9161451 0.6930054" },
                Button = { FadeIn = FadeIn, Command = $"iq.chat ignore.controller take.user user.confirm {TargetUserID} no", Color = HexToRustFormat(Interface.HexButton) },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_NO_BUTTON", player.UserIDString), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
            }, "IQ_IGNORED_BLUR_CONTENT", "BUTTON_ALERT_IGNORED_NO");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #endregion

        #region Settings Turned

        void TurnedPM(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TURNED_PM");

            float FadeIn = 0.3f;
            var Interface = config.InterfaceChat;
            var Data = ChatSettingUser[player.userID];
            string Sprite = Data.PMTurn ? "assets/icons/vote_up.png" : "assets/icons/vote_down.png";
            string Command = Data.PMTurn ? "iq.chat setting turned.pm false" : "iq.chat setting turned.pm true";

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                Button = { FadeIn = FadeIn, Command = Command, Sprite = Sprite, Color = HexToRustFormat(Interface.HexLabel) },
                Text = { FadeIn = FadeIn, Text = "" }
            },  "TURNED_PM_BOX", "TURNED_PM");

            CuiHelper.AddUi(player, container);
        }

        void TurnedBroadcast(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TURNED_BROADCAST");

            float FadeIn = 0.3f;
            var Interface = config.InterfaceChat;
            var Data = ChatSettingUser[player.userID];
            string Sprite = Data.BroadcastTurn ? "assets/icons/vote_up.png" : "assets/icons/vote_down.png";
            string Command = Data.BroadcastTurn ? "iq.chat setting turned.broadcast false" : "iq.chat setting turned.broadcast true";

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                Button = { FadeIn = FadeIn, Command = Command, Sprite = Sprite, Color = HexToRustFormat(Interface.HexLabel) },
                Text = { FadeIn = FadeIn, Text = "" }
            }, "TURNED_BROADCAST_BOX", "TURNED_BROADCAST");

            CuiHelper.AddUi(player, container);
        }     

        void TurnedSound(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TURNED_SOUND");

            float FadeIn = 0.3f;
            var Interface = config.InterfaceChat;
            var Data = ChatSettingUser[player.userID];
            string Sprite = Data.SoundTurn ? "assets/icons/vote_up.png" : "assets/icons/vote_down.png";
            string Command = Data.SoundTurn ? "iq.chat setting turned.sound false" : "iq.chat setting turned.sound true";

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                Button = { FadeIn = FadeIn, Command = Command, Sprite = Sprite, Color = HexToRustFormat(Interface.HexLabel) },
                Text = { FadeIn = FadeIn, Text = "" }
            }, "TURNED_SOUND_BOX", "TURNED_SOUND");

            CuiHelper.AddUi(player, container);
        }

        void TurnedGlobalChat(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TURNED_GLOBAL_CHAT");

            float FadeIn = 0.3f;
            var Interface = config.InterfaceChat;
            var Data = ChatSettingUser[player.userID];
            string Sprite = Data.GlobalChatTurn ? "assets/icons/vote_up.png" : "assets/icons/vote_down.png";
            string Command = Data.GlobalChatTurn ? "iq.chat setting turned.globalchat false" : "iq.chat setting turned.globalchat true";

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                Button = { FadeIn = FadeIn, Command = Command, Sprite = Sprite, Color = HexToRustFormat(Interface.HexLabel) },
                Text = { FadeIn = FadeIn, Text = "" }
            }, "TURNED_GLOBAL_CHAT_BOX", "TURNED_GLOBAL_CHAT");

            CuiHelper.AddUi(player, container);
        }

        void TurnedAlert(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TURNED_ALERT");

            float FadeIn = 0.3f;
            var Interface = config.InterfaceChat;
            var Data = ChatSettingUser[player.userID];
            string Sprite = Data.AlertTurn ? "assets/icons/vote_up.png" : "assets/icons/vote_down.png";
            string Command = Data.AlertTurn ? "iq.chat setting turned.alert false" : "iq.chat setting turned.alert true";

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1" },
                Button = { FadeIn = FadeIn, Command = Command, Sprite = Sprite, Color = HexToRustFormat(Interface.HexLabel) },
                Text = { FadeIn = FadeIn, Text = "" }
            },  "TURNED_ALERT_BOX", "TURNED_ALERT");

            CuiHelper.AddUi(player, container);
        }

        void TurnedAdminButtonMuteAllChat(BasePlayer Admin)
        {
            float FadeIn = 0.3f;
            var Interface = config.InterfaceChat;

            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(Admin, "TITLE_LABEL_PANEL_MODERATION_BUTTON_MUTE_ALL_CHAT");

            string MessageMuteAllChat = !AdminSetting.MuteChatAll ? "UI_CHAT_PANEL_MODERATOR_MUTE_CHAT_ALL_BUTTON_TITLE" : "UI_CHAT_PANEL_MODERATOR_UNMUTE_CHAT_ALL_BUTTON_TITLE";
            string CommandMuteAllChat = !AdminSetting.MuteChatAll ? $"iq.chat mute.controller mute.all.players {MuteType.Chat}" : $"iq.chat mute.controller unmute.all.players {MuteType.Chat}";
            string AnchorMin = IQModalMenu ? "0.04152405 0.2455987" : Interface.InterfacePositions.MainPanels.ModerationBlock.ButtonMuteAllAnchorMin;
            string AnchorMax = IQModalMenu ? "0.4782379 0.3165501" : Interface.InterfacePositions.MainPanels.ModerationBlock.ButtonMuteAllAnchorMax;

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = AnchorMin, AnchorMax = AnchorMax },
                Button = { FadeIn = FadeIn, Command = CommandMuteAllChat, Color = HexToRustFormat(Interface.HexButton) },
                Text = { FadeIn = FadeIn, Text = GetLang(MessageMuteAllChat, Admin.UserIDString), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_PANEL_MODERATION_BUTTON_MUTE_ALL_CHAT");

            CuiHelper.AddUi(Admin, container);
        }
        void TurnedAdminButtonMuteAllVoice(BasePlayer Admin)
        {
            float FadeIn = 0.3f;
            var Interface = config.InterfaceChat;

            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(Admin, "TITLE_LABEL_PANEL_MODERATION_BUTTON_MUTE_ALL_VOICE");

            string MessageMuteAllVoice = !AdminSetting.MuteVoiceAll ? "UI_CHAT_PANEL_MODERATOR_MUTE_VOICE_ALL_BUTTON_TITLE" : "UI_CHAT_PANEL_MODERATOR_UNMUTE_VOICE_ALL_BUTTON_TITLE";
            string CommandMuteAllVoice = !AdminSetting.MuteVoiceAll ? $"iq.chat mute.controller mute.all.players {MuteType.Voice}" : $"iq.chat mute.controller unmute.all.players {MuteType.Voice}";
            string AnchorMin = IQModalMenu ? "0.4802379 0.2455987" : Interface.InterfacePositions.MainPanels.ModerationBlock.ButtonMuteVoiceAllAnchorMin;
            string AnchorMax = IQModalMenu ? "0.9584759 0.3165501" : Interface.InterfacePositions.MainPanels.ModerationBlock.ButtonMuteVoiceAllAnchorMax;

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = AnchorMin, AnchorMax = AnchorMax },
                Button = { FadeIn = FadeIn, Command = CommandMuteAllVoice, Color = HexToRustFormat(Interface.HexButton) },
                Text = { FadeIn = FadeIn, Text = GetLang(MessageMuteAllVoice, Admin.UserIDString), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_PANEL_MODERATION_BUTTON_MUTE_ALL_VOICE");

            CuiHelper.AddUi(Admin, container);
        }
        #endregion

        #region Inforamtion

        void UpdateNick(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, "TITLE_LABEL_INFORMATION_MY_NICK");

            float FadeIn = 0.3f;
            var Interface = config.InterfaceChat;
            var DataPlayer = ChatSettingUser[player.userID];

            string Prefix = string.Empty;
            string Rank = IQRankSystem ? config.ReferenceSetting.IQRankSystems.UseRankSystem ? IQRankGetRank(player.userID) : string.Empty : string.Empty;
            if (config.MessageSetting.MultiPrefix)
            {
                if (DataPlayer.MultiPrefix != null)
                    for (int i = 0; i < DataPlayer.MultiPrefix.Count; i++)
                        Prefix += DataPlayer.MultiPrefix[i];
            }
            else Prefix = DataPlayer.ChatPrefix;
            string ResultNick = !String.IsNullOrEmpty(Rank) ? $"<b>[{Rank}] {Prefix}<color={DataPlayer.NickColor}>{player.displayName}</color> : <color={DataPlayer.MessageColor}> я лучший</color></b>" : $"<b>{Prefix}<color={DataPlayer.NickColor}>{player.displayName}</color> : <color={DataPlayer.MessageColor}> я лучший</color></b>";
            int Size = ResultNick.Length >= 300 ? 7 : ResultNick.Length >= 200 ? 10 : ResultNick.Length >= 100 ? 15 : 20;

            container.Add(new CuiLabel 
            {
                RectTransform = { AnchorMin = Interface.InterfacePositions.MainPanels.InfromationBlock.NickPlayerAnchorMin, AnchorMax = Interface.InterfacePositions.MainPanels.InfromationBlock.NickPlayerAnchorMax },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_NICK_TITLE", player.UserIDString, $"<size={Size}>{ResultNick}</size>"), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION_MY_NICK");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region UI Alert
        void UIAlert(BasePlayer player, string Message)
        {
            if (XDNotifications && config.ReferenceSetting.XDNotificationsSettings.UseXDNotifications)
            {
                AddNotify(player, lang.GetMessage("UI_ALERT_TITLE", this, player.UserIDString), Message);
                return;
            }
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_ALERT_UI);
            var Interface = config.InterfaceChat;
            var Transform = config.InterfaceChat.AlertInterfaceSetting;
            float FadeIn = 0.3f;

            container.Add(new CuiElement
            {
                Parent = "Overlay",
                Name = IQCHAT_PARENT_ALERT_UI,
                Components =
                        {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = Transform.AnchorMin, AnchorMax = Transform.AnchorMax, OffsetMin = Transform.OffsetMin, OffsetMax = Transform.OffsetMax },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "1.3 -1.2", UseGraphicAlpha = true }

                        }
            });

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.01041666 1" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) }
            }, IQCHAT_PARENT_ALERT_UI, "LINE_ALERT");

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_ALERT_UI,
                Name = "SPRITE_ALERT",
                Components =
                    {
                        new CuiImageComponent { FadeIn = FadeIn,  Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/player_assist.png" },
                        new CuiRectTransformComponent { AnchorMin = "0.03333334 0.2800003", AnchorMax = "0.1 0.7066669" }
                    }
            });

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1208334 0.5866671", AnchorMax = "0.8687501 0.933333" },
                Text = { FadeIn = FadeIn, Text = lang.GetMessage("UI_ALERT_TITLE", this, player.UserIDString), Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperLeft }
            },  IQCHAT_PARENT_ALERT_UI, "IQ_ALERT_TITLE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.1208334 0.1066661", AnchorMax = "0.9666667 0.5733335" },
                Text = { FadeIn = FadeIn, Text = Message, FontSize = 10, Color = HexToRustFormat(Interface.HexLabel), Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_ALERT_UI, "IQ_ALERT_TITLE_MESSAGE");

            CuiHelper.AddUi(player, container);

            timer.Once(config.MessageSetting.TimeDeleteAlertUI, () =>
            {
                CuiHelper.DestroyUi(player, IQCHAT_PARENT_ALERT_UI);
            });
        }
        #endregion

        #endregion

        #endregion

        #region Command

        #region UsingCommand
        
        #region Hide Mute

        [ConsoleCommand("hmute")]
        void HideMuteConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermMuteMenu)) return;

            if (arg == null || arg.Args == null || arg.Args.Length != 3 || arg.Args.Length > 3)
            {
                PrintWarning("Неверный синтаксис,используйте : hmute Steam64ID Причина Время(секунды)");
                return;
            }
            string NameOrID = arg.Args[0];
            string Reason = arg.Args[1];
            int TimeMute = int.Parse(arg.Args[2]);
            BasePlayer target = BasePlayer.Find(NameOrID);
            if (target == null)
            {
                PrintWarning("Такого игрока нет на сервере");
                return;
            }

            MutePlayer(target, MuteType.Chat, 0, arg.Player(), Reason, TimeMute, true, true);
        }

        [ChatCommand("hmute")]
        void HideMute(BasePlayer Moderator, string cmd, string[] arg)
        {
            if (!permission.UserHasPermission(Moderator.UserIDString, PermMuteMenu)) return;
            if (arg == null || arg.Length != 3 || arg.Length > 3)
            {
                ReplySystem(Chat.ChatChannel.Global, Moderator, "Неверный синтаксис,используйте : hmute Steam64ID/Ник Причина Время(секунды)");
                return;
            }
            string NameOrID = arg[0];
            string Reason = arg[1];
            int TimeMute = int.Parse(arg[2]);
            BasePlayer target = BasePlayer.Find(NameOrID);
            if (target == null)
            {
                ReplySystem(Chat.ChatChannel.Global, Moderator, "Такого игрока нет на сервере");
                return;
            }

            MutePlayer(target, MuteType.Chat, 0, Moderator, Reason, TimeMute, true, true);
        }

        [ConsoleCommand("hunmute")]
        void HideUnMuteConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermMuteMenu)) return;
            if (arg == null || arg.Args == null || arg.Args.Length != 1 || arg.Args.Length > 1)
            {
                PrintWarning("Неверный синтаксис,используйте : hunmute Steam64ID");
                return;
            }
            string NameOrID = arg.Args[0];
            BasePlayer target = BasePlayer.Find(NameOrID);
            if (target == null)
            {
                PrintWarning("Такого игрока нет на сервере");
                return;
            }

            UnmutePlayer(target, MuteType.Chat, arg.Player(), true, true);
        }

        [ChatCommand("hunmute")]
        void HideUnMute(BasePlayer Moderator, string cmd, string[] arg)
        {
            if (!permission.UserHasPermission(Moderator.UserIDString, PermMuteMenu)) return;
            if (arg == null || arg.Length != 1 || arg.Length > 1)
            {
                ReplySystem(Chat.ChatChannel.Global, Moderator, "Неверный синтаксис,используйте : hunmute Steam64ID/Ник");
                return;
            }
            string NameOrID = arg[0];
            BasePlayer target = BasePlayer.Find(NameOrID);
            if (target == null)
            {
                ReplySystem(Chat.ChatChannel.Global, Moderator, "Такого игрока нет на сервере");
                return;
            }

            UnmutePlayer(target, MuteType.Chat, Moderator, true, true);
        }

        #endregion

        #region Mute

        [ConsoleCommand("mute")]
        void MuteCustomAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermMuteMenu)) return;
            if (arg == null || arg.Args == null || arg.Args.Length != 3 || arg.Args.Length > 3)
            {
                PrintWarning("Неверный синтаксис,используйте : mute Steam64ID/Ник Причина Время(секунды)");
                return;
            }
            string NameOrID = arg.Args[0];
            string Reason = arg.Args[1];
            int TimeMute = int.Parse(arg.Args[2]);
            BasePlayer target = BasePlayer.Find(NameOrID);
            if (target == null)
            {
                PrintWarning("Такого игрока нет на сервере");
                return;
            }

            MutePlayer(target, MuteType.Chat, 0, arg.Player(), Reason, TimeMute, true, true);
            Puts("Успешно");
        }

        [ChatCommand("mute")]
        void MuteCustomChat(BasePlayer Moderator, string cmd, string[] arg)
        {
            if (!permission.UserHasPermission(Moderator.UserIDString, PermMuteMenu)) return;
            if (arg == null || arg.Length != 3 || arg.Length > 3)
            {
                ReplySystem(Chat.ChatChannel.Global, Moderator, "Неверный синтаксис,используйте : mute Steam64ID/Ник Причина Время(секунды)");
                return;
            }
            string NameOrID = arg[0];
            string Reason = arg[1];
            int TimeMute = int.Parse(arg[2]);
            BasePlayer target = BasePlayer.Find(NameOrID);
            if (target == null)
            {
                ReplySystem(Chat.ChatChannel.Global, Moderator, "Такого игрока нет на сервере");
                return;
            }

            MutePlayer(target, MuteType.Chat, 0, Moderator, Reason, TimeMute, true, true);
        }

        [ConsoleCommand("unmute")]
        void UnMuteCustomAdmin(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                if (!permission.UserHasPermission(arg.Player().UserIDString, PermMuteMenu)) return;
            if (arg == null || arg.Args == null || arg.Args.Length != 1 || arg.Args.Length > 1)
            {
                PrintWarning("Неверный синтаксис,используйте : unmute Steam64ID");
                return;
            }
            string NameOrID = arg.Args[0];
            BasePlayer target = BasePlayer.Find(NameOrID);
            if (target == null)
            {
                PrintWarning("Такого игрока нет на сервере");
                return;
            }
            UnmutePlayer(target, MuteType.Chat, arg.Player(), true, true);
            Puts("Успешно");
        }

        [ChatCommand("unmute")]
        void UnMuteCustomChat(BasePlayer Moderator, string cmd, string[] arg)
        {
            if (!permission.UserHasPermission(Moderator.UserIDString, PermMuteMenu)) return;
            if (arg == null || arg.Length != 1 || arg.Length > 1)
            {
                ReplySystem(Chat.ChatChannel.Global, Moderator, "Неверный синтаксис,используйте : unmute Steam64ID");
                return;
            }
            string NameOrID = arg[0];
            BasePlayer target = BasePlayer.Find(NameOrID);
            if (target == null)
            {
                ReplySystem(Chat.ChatChannel.Global, Moderator, "Такого игрока нет на сервере");
                return;
            }
            UnmutePlayer(target, MuteType.Chat, Moderator, true, true);
        }

        #endregion

        [ChatCommand("chat")]
        void ChatCommandMenu(BasePlayer player)
        {
            if (IQModalMenu)
                IQModalSend(player);
            else IQChat_UI_Menu(player);
        }

        [ChatCommand("alert")]
        void ChatAlertPlayers(BasePlayer player, string cmd, string[] arg) => Alert(player, arg,false);

        [ChatCommand("alertui")]
        void ChatAlertPlayersUI(BasePlayer player, string cmd, string[] arg) => AlertUI(player, arg);

        [ChatCommand("adminalert")]
        void ChatAdminAlert(BasePlayer player, string cmd, string[] arg) => Alert(player, arg, true);

        [ChatCommand("rename")]
        void RenameMetods(BasePlayer player, string cmd, string[] arg)
        {
            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_RENAME_NOTARG", this, player.UserIDString));
                return;
            }
            string NewName = "";
            foreach (var name in arg)
                NewName += " " + name;
            RenameFunc(player, NewName);
        }

        #region PM

        [ChatCommand("pm")]
        void PmChat(BasePlayer player, string cmd, string[] arg)
        {
            if (!config.MessageSetting.PMActivate) return;
            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_PM_NOTARG", this, player.UserIDString));
                return;
            }
            string NameUser = arg[0];
            if (config.ReferenceSetting.IQFakeActiveSettings.UseIQFakeActive)
                if (IQFakeActive)
                    if (IsFake(NameUser))
                    {
                        ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_PM_SUCCESS", player.UserIDString, string.Join(" ", arg.ToArray().ToArray()).Replace(NameUser, "")));
                        return;
                    }
            BasePlayer TargetUser = BasePlayer.Find(NameUser);
            if (TargetUser == null || NameUser == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_PM_NOT_USER", player.UserIDString));
                return;
            }
            if(!ChatSettingUser[TargetUser.userID].PMTurn)
            {
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("FUNC_MESSAGE_PM_TURN_FALSE", player.UserIDString));
                return;
            }
            if (config.MessageSetting.IgnoreUsePM)
            {
                if (IsIgnored(TargetUser.userID, player.userID))
                {
                    ReplySystem(Chat.ChatChannel.Global, player, GetLang("IGNORE_NO_PM", player.UserIDString));
                    return;
                }
                if (IsIgnored(player.userID,TargetUser.userID))
                {
                    ReplySystem(Chat.ChatChannel.Global, player, GetLang("IGNORE_NO_PM_ME", player.UserIDString));
                    return;
                }
            }
            var argList = arg.ToArray();
            string Message = string.Join(" ", argList.ToArray()).Replace(NameUser, "");
            if (Message.Length > 125) return;
            if (Message.Length <= 0 || Message == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_PM_NOT_NULL_MSG", player.UserIDString));
                return;
            }

            PMHistory[TargetUser] = player;
            PMHistory[player] = TargetUser;
            var DisplayNick = AdminSetting.RenameList.ContainsKey(player.userID) ? AdminSetting.RenameList[player.userID] : player.displayName;

            if (IQModalMenu)
            {
                if (ChatSettingUser[TargetUser.userID].GlobalChatTurn)
                    ReplySystem(Chat.ChatChannel.Global, TargetUser, GetLang("COMMAND_PM_SEND_MSG", player.UserIDString, DisplayNick, Message));
            }
            else ReplySystem(Chat.ChatChannel.Global, TargetUser, GetLang("COMMAND_PM_SEND_MSG", player.UserIDString, DisplayNick, Message));

            ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_PM_SUCCESS", player.UserIDString, Message));

            if (ChatSettingUser[TargetUser.userID].SoundTurn || (IQModalMenu && ChatSettingUser[TargetUser.userID].GlobalChatTurn))
                Effect.server.Run(config.MessageSetting.SoundPM, TargetUser.GetNetworkPosition());
            Log($"ЛИЧНЫЕ СООБЩЕНИЯ : {player.userID}({DisplayNick}) отправил сообщение игроку - {TargetUser.displayName}\nСООБЩЕНИЕ : {Message}");

            RCon.Broadcast(RCon.LogType.Chat, new Chat.ChatEntry
            {
                Message = $"ЛИЧНЫЕ СООБЩЕНИЯ : {DisplayNick}({player.userID}) -> {TargetUser.displayName} : СООБЩЕНИЕ : {Message}",
                UserId = player.UserIDString,
                Username = player.displayName,
                Channel = Chat.ChatChannel.Global,
                Time = (DateTime.UtcNow.Hour * 3600) + (DateTime.UtcNow.Minute * 60),
                Color = "#3f4bb8",
            });
            PrintWarning($"ЛИЧНЫЕ СООБЩЕНИЯ : {DisplayNick}({player.userID}) -> {TargetUser.displayName} : СООБЩЕНИЕ : {Message}");
        }

        [ChatCommand("r")]
        void RChat(BasePlayer player, string cmd, string[] arg)
        {
            if (!config.MessageSetting.PMActivate) return;
            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_R_NOTARG", player.UserIDString));
                return;
            }
            if (!PMHistory.ContainsKey(player))
            {
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_R_NOTMSG", player.UserIDString));
                return;
            }
            BasePlayer RetargetUser = PMHistory[player];
            if (RetargetUser == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_PM_NOT_USER", player.UserIDString));
                return;
            }
            if (!ChatSettingUser[RetargetUser.userID].PMTurn)
            {
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("FUNC_MESSAGE_PM_TURN_FALSE", player.UserIDString));
                return;
            }
            if (config.MessageSetting.IgnoreUsePM)
            {
                if (IsIgnored(RetargetUser.userID, player.userID))
                {
                    ReplySystem(Chat.ChatChannel.Global, player, GetLang("IGNORE_NO_PM", player.UserIDString));
                    return;
                }
                if (IsIgnored(player.userID, RetargetUser.userID))
                {
                    ReplySystem(Chat.ChatChannel.Global, player, GetLang("IGNORE_NO_PM_ME", player.UserIDString));
                    return;
                }
            }
            string Message = string.Join(" ", arg.ToArray());
            if (Message.Length > 125) return;
            if (Message.Length <= 0 || Message == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_PM_NOT_NULL_MSG", player.UserIDString));
                return;
            }
            PMHistory[RetargetUser] = player;
            var DisplayNick = AdminSetting.RenameList.ContainsKey(player.userID) ? AdminSetting.RenameList[player.userID] : player.displayName;

            if (IQModalMenu)
            {
                if (ChatSettingUser[RetargetUser.userID].GlobalChatTurn)
                    ReplySystem(Chat.ChatChannel.Global, RetargetUser, GetLang("COMMAND_PM_SEND_MSG", player.UserIDString, DisplayNick, Message));
            }
            else ReplySystem(Chat.ChatChannel.Global, RetargetUser, GetLang("COMMAND_PM_SEND_MSG", player.UserIDString, DisplayNick, Message));

            ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_PM_SUCCESS", player.UserIDString, Message));

            if (ChatSettingUser[RetargetUser.userID].SoundTurn || (ChatSettingUser[RetargetUser.userID].GlobalChatTurn && IQModalMenu))
                Effect.server.Run(config.MessageSetting.SoundPM, RetargetUser.GetNetworkPosition());

            Log($"ЛИЧНЫЕ СООБЩЕНИЯ : {player.displayName} отправил сообщение игроку - {RetargetUser.displayName}\nСООБЩЕНИЕ : {Message}");

            RCon.Broadcast(RCon.LogType.Chat, new Chat.ChatEntry
            {
                Message = $"ЛИЧНЫЕ СООБЩЕНИЯ : {DisplayNick}({player.userID}) -> {RetargetUser.displayName} : СООБЩЕНИЕ : {Message}",
                UserId = player.UserIDString,
                Username = player.displayName,
                Channel = Chat.ChatChannel.Global,
                Time = (DateTime.UtcNow.Hour * 3600) + (DateTime.UtcNow.Minute * 60),
                Color = "#3f4bb8",
            });
            PrintWarning($"ЛИЧНЫЕ СООБЩЕНИЯ : {DisplayNick}({player.userID}) -> {RetargetUser.displayName} : СООБЩЕНИЕ : {Message}");
        }

        [ChatCommand("ignore")]
        void IgnorePlayerPM(BasePlayer player, string cmd, string[] arg)
        {
            if (!config.MessageSetting.IgnoreUsePM) return;
            var ChatUser = ChatSettingUser[player.userID];
            if (arg.Length == 0 || arg == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("INGORE_NOTARG", this, player.UserIDString));
                return;
            }
            string NameUser = arg[0];
            BasePlayer TargetUser = BasePlayer.Find(NameUser);
            if (TargetUser == null || NameUser == null)
            {
                ReplySystem(Chat.ChatChannel.Global, player, lang.GetMessage("COMMAND_PM_NOT_USER", this, player.UserIDString));
                return;
            }

            string Lang = !IsIgnored(player.userID, TargetUser.userID) ? GetLang("IGNORE_ON_PLAYER", player.UserIDString, TargetUser.displayName) : GetLang("IGNORE_OFF_PLAYER", player.UserIDString, TargetUser.displayName);
            ReplySystem(Chat.ChatChannel.Global, player, Lang);
            if (!IsIgnored(player.userID, TargetUser.userID))
                ChatUser.IgnoredUsers.Add(TargetUser.userID);
            else ChatUser.IgnoredUsers.Remove(TargetUser.userID);
        }

        #endregion

        [ConsoleCommand("alert")]
        void ChatAlertPlayersCMD(ConsoleSystem.Arg arg) => Alert(arg.Player(), arg.Args, false);

        [ConsoleCommand("alertui")]
        void ChatAlertPlayersUICMD(ConsoleSystem.Arg arg) => AlertUI(arg.Player(), arg.Args);

        [ConsoleCommand("adminalert")]
        void ConsoleAdminAlert(ConsoleSystem.Arg arg) => Alert(arg.Player(), arg.Args, true);

        [ConsoleCommand("alertuip")]
        void CmodAlertOnlyUser(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2)
            {
                PrintWarning("Используйте правильно ситаксис : alertuip Steam64ID Сообщение");
                return;
            }
            var argList = arg.Args.ToArray();
            string Message = string.Join(" ", argList.ToArray().Skip(1));
            if (Message.Length > 125) return;
            if (Message.Length <= 0 || Message == null)
            {
                PrintWarning("Вы не указали сообщение игроку");
                return;
            }
            BasePlayer player = BasePlayer.Find(arg.Args[0]);
            if (player == null)
            {
                PrintWarning("Игрока нет в сети");
                return;
            }
            UIAlert(player, Message);
        }

        [ConsoleCommand("saybro")]
        void ChatAlertPlayerInPM(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2)
            {
                PrintWarning("Используйте правильно ситаксис : saybro NameOrID Сообщение");
                return;
            }
            var argList = arg.Args.ToArray();
            string Message = string.Join(" ", argList.ToArray());
            if (Message.Length > 125) return;
            if (Message.Length <= 0 || Message == null)
            {
                PrintWarning("Вы не указали сообщение игроку");
                return;
            }
            BasePlayer player = BasePlayer.Find(arg.Args[0]);
            if(player == null)
            {
                PrintWarning("Игрока нет в сети");
                return;
            }
            ReplySystem(Chat.ChatChannel.Global, player, Message.Replace(player.userID.ToString(), ""));
        }

        [ConsoleCommand("set")]
        private void ConsolesCommandPrefixSet(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length != 3)
            {
                PrintWarning("Используйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                return;
            }
            ulong Steam64ID = 0;
            BasePlayer player = null;
            if (ulong.TryParse(arg.Args[0], out Steam64ID))
                player = BasePlayer.FindByID(Steam64ID);
            if (player == null)
            {
                PrintWarning("Неверно указан SteamID игрока или ошибка в синтаксисе\nИспользуйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                return;
            }
            var DataPlayer = ChatSettingUser[player.userID];

            switch (arg.Args[1].ToLower())
            {
                case "prefix":
                    {
                        string KeyPrefix = arg.Args[2];
                        foreach (var Prefix in config.PrefixList.Where(x => x.Permissions == KeyPrefix))
                            if (config.PrefixList.Contains(Prefix))
                            {
                                DataPlayer.ChatPrefix = Prefix.Argument;
                                Puts($"Префикс успешно установлен на - {Prefix.Argument}");
                            }
                            else Puts("Неверно указан Permissions от префикса");
                        break;
                    }
                case "chat":
                    {
                        string KeyChatColor = arg.Args[2];
                        foreach (var ColorChat in config.PrefixList.Where(x => x.Permissions == KeyChatColor))
                            if (config.MessageColorList.Contains(ColorChat))
                            {
                                DataPlayer.MessageColor = ColorChat.Argument;
                                Puts($"Цвет сообщения успешно установлен на - {ColorChat.Argument}");
                            }
                            else Puts("Неверно указан Permissions от префикса");
                        break;
                    }
                case "nick":
                    {
                        string KeyNickColor = arg.Args[2];
                        foreach (var ColorChat in config.NickColorList.Where(x => x.Permissions == KeyNickColor))
                            if (config.NickColorList.Contains(ColorChat))
                            {
                                DataPlayer.NickColor = ColorChat.Argument;
                                Puts($"Цвет ника успешно установлен на - {ColorChat.Argument}");
                            }
                            else Puts("Неверно указан Permissions от префикса");
                        break;
                    }
                case "custom":
                    {
                        string CustomPrefix = arg.Args[2];
                        DataPlayer.ChatPrefix = CustomPrefix;
                        Puts($"Кастомный префикс успешно установлен на - {CustomPrefix}");
                        break;
                    }
                default:
                    {
                        PrintWarning("Используйте правильно ситаксис : set [Steam64ID] [prefix/chat/nick/custom] [Argument]");
                        break;
                    }
            }

        }

        #endregion

        #region FuncCommand

        [ConsoleCommand("iq.chat")]
        private void ConsoleCommandChat(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;
            var DataPlayer = ChatSettingUser[player.userID];

            switch (arg.Args[0])
            {
                case "take.element.droplist": 
                    {
                        bool IsOpened = bool.Parse(arg.Args[1]);
                        TakeElemntUser Type = (TakeElemntUser)Enum.Parse(typeof(TakeElemntUser), arg.Args[2]);
                        Take_DropList(player, IsOpened, Type);
                        break;
                    }
                case "drop.list.page.controller": 
                    {
                        string PageAction = (string)arg.Args[1];
                        TakeElemntUser Type = (TakeElemntUser)Enum.Parse(typeof(TakeElemntUser), arg.Args[2]);

                        switch (PageAction)
                        {
                            case "next":
                                {
                                    int Page = int.Parse(arg.Args[3]) + 1;
                                    LoadedElementList(player, Type, Page);
                                    break;
                                }
                            case "back":
                                {
                                    int Page = int.Parse(arg.Args[3]) - 1;
                                    LoadedElementList(player, Type, Page);
                                    break;
                                }
                        }
                        break;
                    }
                case "take.element.user": 
                    {
                        TakeElemntUser TypeElement = (TakeElemntUser)Enum.Parse(typeof(TakeElemntUser), arg.Args[1]);
                        switch(TypeElement)
                        {
                            case TakeElemntUser.Prefix:
                                {
                                    var Other = config.InterfaceChat.OtherSettingInterface;
                                    int Index = int.Parse(arg.Args[2]);
                                    int Page =  int.Parse(arg.Args[3]);
                                    var PrefixList = Other.DropListPrefixUse ? config.PrefixList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).Skip(8 * Page).ToList(): config.PrefixList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).ToList();
                                    DataPlayer.ChatPrefix = PrefixList[Index].Argument;
                                    UI_Prefix_Taked(player, Index, Page);
                                    break;
                                }
                            case TakeElemntUser.Nick:
                                {
                                    var Other = config.InterfaceChat.OtherSettingInterface;
                                    int Index = int.Parse(arg.Args[2]);
                                    int Page = int.Parse(arg.Args[3]);
                                    var ColorList = Other.DropListColorNickUse ? config.NickColorList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).Skip(8 * Page).ToList() : config.NickColorList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).ToList();
                                    DataPlayer.NickColor = ColorList[Index].Argument;
                                    UI_Nick_Taked(player, Index, Page);
                                    break;
                                }
                            case TakeElemntUser.Chat:
                                {
                                    var Other = config.InterfaceChat.OtherSettingInterface;
                                    int Index = int.Parse(arg.Args[2]);
                                    int Page = int.Parse(arg.Args[3]);
                                    var ColorList = Other.DropListColorChatUse ? config.MessageColorList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).Skip(8 * Page).ToList() : config.MessageColorList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).ToList();
                                    DataPlayer.MessageColor = ColorList[Index].Argument;
                                    UI_Chat_Taked(player, Index, Page);
                                    break;
                                }
                            case TakeElemntUser.Rank:
                                {
                                    var Other = config.InterfaceChat.OtherSettingInterface;
                                    int Index = int.Parse(arg.Args[2]);
                                    int Page = int.Parse(arg.Args[3]);
                                    var RankList = Other.DropListRankUse ? IQRankListKey(player.userID).Skip(8 * Page).ToList() : IQRankListKey(player.userID);
                                    IQRankSetRank(player.userID, RankList[Index]);
                                    UI_Rank_Taked(player, Index, Page);
                                    break;
                                }
                            case TakeElemntUser.MultiPrefix: 
                                {
                                    var Other = config.InterfaceChat.OtherSettingInterface;
                                    int Index = int.Parse(arg.Args[2]);
                                    int Page = int.Parse(arg.Args[3]);
                                    var PrefixList = config.PrefixList.Where(p => permission.UserHasPermission(player.UserIDString, p.Permissions)).Skip(8 * Page).ToList();
                                    string Prefix = PrefixList[Index].Argument;

                                    if (DataPlayer.MultiPrefix.Contains(Prefix))
                                        DataPlayer.MultiPrefix.Remove(Prefix);
                                    else DataPlayer.MultiPrefix.Add(Prefix);

                                    Take_DropList(player, false, TypeElement, Page);
                                    break;
                                }
                        }
                        if (!IQModalMenu)
                            UpdateNick(player);
                        else UpdateNickModal(player);
                        break;
                    }
                case "setting":
                    {
                        string Action = (string)arg.Args[1];
                        switch (Action)
                        {
                            case "turned.pm":
                                {
                                    bool Turn = bool.Parse(arg.Args[2]);
                                    ChatSettingUser[player.userID].PMTurn = Turn;
                                    TurnedPM(player);
                                    break;
                                }
                            case "turned.alert":
                                {
                                    bool Turn = bool.Parse(arg.Args[2]);
                                    ChatSettingUser[player.userID].AlertTurn = Turn;
                                    TurnedAlert(player);
                                    break;
                                }
                            case "turned.broadcast":
                                {
                                    bool Turn = bool.Parse(arg.Args[2]);
                                    ChatSettingUser[player.userID].BroadcastTurn = Turn;
                                    TurnedBroadcast(player);
                                    break;
                                }
                            case "turned.sound":
                                {
                                    bool Turn = bool.Parse(arg.Args[2]);
                                    ChatSettingUser[player.userID].SoundTurn = Turn;
                                    TurnedSound(player);
                                    break;
                                }         
                            case "turned.globalchat":
                                {
                                    bool Turn = bool.Parse(arg.Args[2]);
                                    ChatSettingUser[player.userID].GlobalChatTurn = Turn;
                                    TurnedGlobalChat(player);
                                    break;
                                }
                        }
                        break;
                    }
                case "ignore.controller":  
                    {
                        string Action = (string)arg.Args[1];
                        switch (Action)
                        {
                            case "open":
                                {
                                    UI_Ignored_Menu(player);
                                    break;
                                }
                            case "page.controller":
                                {
                                    string PageAction = (string)arg.Args[2];
                                    switch (PageAction)
                                    {
                                        case "next":
                                            {
                                                int Page = int.Parse(arg.Args[3]) + 1;
                                                UI_Ignored_Loaded_Players(player, Page);
                                                break;
                                            }
                                        case "back":
                                            {
                                                int Page = int.Parse(arg.Args[3]) - 1;
                                                UI_Ignored_Loaded_Players(player, Page);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case "search.player":
                                {
                                    string Search = arg.HasArgs(3) ? arg.Args[2] : "";
                                    UI_Ignored_Loaded_Players(player, 0, Search);
                                    break;
                                }
                            case "take.user": 
                                {
                                    string ActionUser = (string)arg.Args[2];
                                    switch (ActionUser)
                                    {
                                        case "take":
                                            {
                                                ulong TargetUserID = ulong.Parse(arg.Args[3]);
                                                Take_User_Ignored_Menu(player, TargetUserID);
                                                break;
                                            }
                                        case "user.confirm":
                                            {
                                                BasePlayer Target = BasePlayer.FindByID(ulong.Parse(arg.Args[3]));
                                                string ConfirmOut = (string)arg.Args[4];
                                                switch (ConfirmOut)
                                                {
                                                    case "yes":
                                                        {
                                                            if (Target == null || !Target.IsConnected)
                                                            {
                                                                AlertController(player, "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_NOT_CONNNECTED",false);
                                                                return;
                                                            }
                                                            if (!IsIgnored(player.userID, Target.userID))
                                                                DataPlayer.IgnoredUsers.Add(Target.userID);
                                                            else DataPlayer.IgnoredUsers.Remove(Target.userID);
                                                            AlertController(player, "UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_ACCESS",false);

                                                            var Interface = config.InterfaceChat;
                                                            float FadeIn = 0.3f;
                                                            CuiElementContainer container = new CuiElementContainer();
                                                            CuiHelper.DestroyUi(player, "TITLE_LABEL_INFORMATION_IGNORED_TITLE_COUNT");

                                                            string AcnhorMin = IQModalMenu ? "0.2033799 0.5130037" : "0.443033 0.366909";
                                                            string AnchorMax = IQModalMenu ? "0.3609195 0.553529" : "0.9584759 0.4173968";
                                                            container.Add(new CuiButton
                                                            {
                                                                RectTransform = { AnchorMin = AcnhorMin, AnchorMax = AnchorMax },
                                                                Button = { FadeIn = FadeIn, Command = "iq.chat ignore.controller open", Color = HexToRustFormat(Interface.HexButton) },
                                                                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_IGNORED_BUTTON_TITLE", player.UserIDString, DataPlayer.IgnoredUsers.Count), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter }
                                                            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION_IGNORED_TITLE_COUNT");

                                                            CuiHelper.AddUi(player, container);
                                                            break;
                                                        }
                                                    case "no":
                                                        {
                                                            CuiHelper.DestroyUi(player, "IQ_IGNORED_BLUR_CONTENT");
                                                            break;
                                                        }
                                                }
                                                break;
                                            }
                                    }
                                    break;
                                }
                        }
                        break;
                    }
                case "mute.controller":
                    {
                        string Action = (string)arg.Args[1];
                        switch (Action)
                        {
                            case "open.menu":
                                {
                                    IQChat_UI_Mute_Menu(player);
                                    break;
                                }
                            case "mute.all.players": 
                                {
                                    MuteType TypeMute = (MuteType)Enum.Parse(typeof(MuteType), arg.Args[2]);

                                    if (TypeMute == MuteType.Chat)
                                    {
                                        AdminSetting.MuteChatAll = true;
                                        MuteAllChatPlayer();
                                        ReplyBroadcast(GetLang("FUNC_MESSAGE_MUTE_ALL_CHAT", player.UserIDString));
                                        TurnedAdminButtonMuteAllChat(player); 
                                    }
                                    else
                                    {
                                        AdminSetting.MuteVoiceAll = true;
                                        MuteAllVoicePlayer();
                                        ReplyBroadcast(GetLang("FUNC_MESSAGE_MUTE_ALL_VOICE", player.UserIDString));
                                        TurnedAdminButtonMuteAllVoice(player);
                                    }
                                    break;
                                }
                            case "unmute.all.players":
                                {
                                    MuteType TypeMute = (MuteType)Enum.Parse(typeof(MuteType), arg.Args[2]);
                                    if (TypeMute == MuteType.Chat)
                                    {
                                        AdminSetting.MuteChatAll = false;
                                        UnMuteAllChatPlayer();
                                        ReplyBroadcast(GetLang("FUNC_MESSAGE_UNMUTE_ALL_CHAT", player.UserIDString));
                                        TurnedAdminButtonMuteAllChat(player);
                                    }
                                    else
                                    {
                                        AdminSetting.MuteVoiceAll = false;
                                        UnMuteAllVoicePlayer();
                                        ReplyBroadcast(GetLang("FUNC_MESSAGE_UNMUTE_ALL_VOICE", player.UserIDString));
                                        TurnedAdminButtonMuteAllVoice(player);
                                    }
                                    break;
                                }
                            case "page.controller":
                                {
                                    string PageAction = (string)arg.Args[2];
                                    switch (PageAction)
                                    {
                                        case "next":
                                            {
                                                int Page = int.Parse(arg.Args[3]) + 1;
                                                Loaded_Players_Mute_Menu(player, Page);
                                                break;
                                            }
                                        case "back":
                                            {
                                                int Page = int.Parse(arg.Args[3]) - 1;
                                                Loaded_Players_Mute_Menu(player, Page);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case "search.player":
                                {
                                    string Search = arg.HasArgs(3) ? arg.Args[2] : "";
                                    Loaded_Players_Mute_Menu(player, 0, Search);
                                    break;
                                }
                            case "take.user": 
                                { 
                                    string ActionUser = (string)arg.Args[2];
                                    switch(ActionUser)
                                    {
                                        case "take":
                                            {
                                                ulong TargetUserID = ulong.Parse(arg.Args[3]);
                                                Take_User_Mute_Menu(player, TargetUserID);
                                                break;
                                            }
                                        case "take.type":
                                            {
                                                MuteType TypeAction = (MuteType)Enum.Parse(typeof(MuteType), arg.Args[3]);
                                                ulong TargetUserID = ulong.Parse(arg.Args[4]);
                                                Take_Reason_Mute_Menu(player, TypeAction, TargetUserID);
                                                break;
                                            }
                                        case "user.action": 
                                            {
                                                BasePlayer Target = BasePlayer.FindByID(ulong.Parse(arg.Args[3]));
                                                string ActionMute = (string)arg.Args[4];
                                                switch (ActionMute)
                                                {
                                                    case "mute":
                                                        {
                                                            MuteType TypeAction = (MuteType)Enum.Parse(typeof(MuteType), arg.Args[5]);
                                                            int ReasonIndex = int.Parse(arg.Args[6]);
                                                            MutePlayer(Target, TypeAction, ReasonIndex, player);
                                                            break;
                                                        }
                                                    case "unmute":
                                                        {
                                                            MuteType TypeAction = (MuteType)Enum.Parse(typeof(MuteType), arg.Args[5]);
                                                            UnmutePlayer(Target, TypeAction, player);
                                                            break;
                                                        }
                                                }
                                                break;
                                            }
                                    }
                                    break;
                                }
                        }
                        break;
                    }
            }
        }
        #endregion

        #endregion

        #region Lang
        private new void LoadDefaultMessages()
        {
            PrintWarning("Языковой файл загружается...");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FUNC_MESSAGE_MUTE_CHAT"] = "{0} muted {1} for {2}\nReason : {3}",
                ["FUNC_MESSAGE_UNMUTE_CHAT"] = "{0} unmuted {1}",
                ["FUNC_MESSAGE_MUTE_VOICE"] = "{0} muted voice to {1} for {2}\nReason : {3}",
                ["FUNC_MESSAGE_UNMUTE_VOICE"] = "{0} unmuted voice to {1}",
                ["FUNC_MESSAGE_MUTE_ALL_CHAT"] = "Chat disabled",
                ["FUNC_MESSAGE_UNMUTE_ALL_CHAT"] = "Chat enabled",
                ["FUNC_MESSAGE_MUTE_ALL_VOICE"] = "Voice chat disabled",
                ["FUNC_MESSAGE_UNMUTE_ALL_VOICE"] = "Voice chat enabled",
                ["FUNC_MESSAGE_MUTE_ALL_ALERT"] = "Блокировка Администратором",
                ["FUNC_MESSAGE_PM_TURN_FALSE"] = "Игрок запретил присылать себе личные сообщения",
                ["FUNC_MESSAGE_ALERT_TURN_FALSE"] = "Игрок запретил уведомлять себя",

                ["FUNC_MESSAGE_ISMUTED_TRUE"] = "You can not send the messages {0}\nYou are muted",
                ["FUNC_MESSAGE_NO_ARG_BROADCAST"] = "You can not send an empty broadcast message!",

                ["UI_ALERT_TITLE"] = "<size=14><b>Уведомление</b></size>",

                ["UI_TITLE_PANEL_LABEL"] = "<size=25>НАСТРОЙКА ЧАТА</size>",
                ["UI_CHAT_PANEL_PREFIX_TITLE"] = "<size=20><b>ПРЕФИКС:</b></size>",
                ["UI_CHAT_PANEL_CHAT_TITLE"] = "<size=20><b>ЧАТ:</b></size>",
                ["UI_CHAT_PANEL_NICK_TITLE"] = "<size=20><b>НИК:</b></size>",
                ["UI_CHAT_PANEL_RANK_TITLE"] = "<size=20><b>РАНГ:</b></size>",

                ["UI_CHAT_PANEL_SETTINGS_TITLE"] = "<size=30><b>НАСТРОЙКИ:</b></size>",
                ["UI_CHAT_PANEL_SETTINGS_PM_TITLE"] = "<size=15><b>ЛИЧНЫЕ СООБЩЕНИЯ</b></size>",
                ["UI_CHAT_PANEL_SETTINGS_ALERT_TITLE"] = "<size=15><b>УПОМИНАНИЯ В ЧАТЕ</b></size>",
                ["UI_CHAT_PANEL_SETTINGS_BROADCAST_TITLE"] = "<size=15><b>ОПОВЕЩЕНИЕ В ЧАТЕ</b></size>",
                ["UI_CHAT_PANEL_SETTINGS_SOUND_TITLE"] = "<size=13><b>ЗВУКОВОЕ ОПОВЕЩЕНИЕ</b></size>",
                ["UI_CHAT_PANEL_SETTINGS_GLOBAL_TITLE"] = "<size=13><b>ГЛОБАЛЬНЫЙ ЧАТ</b></size>",

                ["UI_CHAT_PANEL_INFORMATION_TITLE"] = "<size=30><b>ИНФОРМАЦИЯ:</b></size>",
                ["UI_CHAT_PANEL_INFORMATION_NICK_TITLE"] = "<size=20><b>ВАШ НИК: {0}</b></size>",
                ["UI_CHAT_PANEL_INFORMATION_MUTED_TITLE"] = "<size=20><b>БЛОКИРОВКА ЧАТА: <color=#cc4228>{0}</color></b></size>",
                ["UI_CHAT_PANEL_INFORMATION_MUTED_TITLE_NOT"] = "<size=20><b><color=#A7F64FFF>ОТСУТСТВУЕТ</color></b></size>",
                ["UI_CHAT_PANEL_INFORMATION_IGNORED_TITLE"] = "<size=20><b>ИГНОРИРОВАНИЕ :</b></size>",
                ["UI_CHAT_PANEL_INFORMATION_IGNORED_BUTTON_TITLE"] = "<size=15><b>{0} ЧЕЛОВЕК(А)</b></size>",

                ["UI_CHAT_PANEL_MODERATOR_TITLE"] = "<size=30><b>ПАНЕЛЬ МОДЕРАТОРА:</b></size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_BUTTON_TITLE"] = "<size=17><b>УПРАВЛЕНИЕ БЛОКИРОВКАМИ ЧАТА</b></size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_CHAT_ALL_BUTTON_TITLE"] = "<size=17><b>ЗАБЛОКИРОВАТЬ ВСЕМ ЧАТ</b></size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_VOICE_ALL_BUTTON_TITLE"] = "<size=17><b>ЗАБЛОКИРОВАТЬ ВСЕМ ГОЛОС</b></size>",
                ["UI_CHAT_PANEL_MODERATOR_UNMUTE_CHAT_ALL_BUTTON_TITLE"] = "<size=17><b>РАЗБЛОКИРОВАТЬ ВСЕМ ЧАТ</b></size>",
                ["UI_CHAT_PANEL_MODERATOR_UNMUTE_VOICE_ALL_BUTTON_TITLE"] = "<size=17><b>РАЗБЛОКИРОВАТЬ ВСЕМ ГОЛОС</b></size>",

                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TITLE"] = "<size=25>УПРАВЛЕНИЕ БЛОКИРОВКАМИ ЧАТА</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TITLE_SEARCH"] = "<size=25>ПОИСК</size>",

                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_MUTE_TITLE"] = "<size=20>ВЫБЕРИТЕ ДЕЙСТВИЕ</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_REASON_MUTE_TITLE"] = "<size=20>ВЫБЕРИТЕ ПРИЧИНУ БЛОКИРОВКИ</size>",

                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_VOICE"] = "<size=20>ЗАБЛОКИРОВАТЬ ГОЛОСОВОЙ ЧАТ</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT"] = "<size=20>ЗАБЛОКИРОВАТЬ ЧАТ</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_VOICE_UNMUTE"] = "<size=20>РАЗБЛОКИРОВАТЬ ГОЛОСОВОЙ ЧАТ</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_UNMUTE"] = "<size=20>РАЗБЛОКИРОВАТЬ ЧАТ</size>",

                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_ACCESS"] = "<size=35>УСПЕШНО</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_NOT_CONNNECTED"] = "<size=30>ИГРОК ВЫШЕЛ С СЕРВЕРА</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_ITSMUTED"] = "<size=30>ИГРОК УЖЕ ЗАМУЧЕН</size>",

                ["UI_CHAT_PANEL_IGNORED_MENU_TITLE"] = "<size=25>УПРАВЛЕНИЕ ИГНОРИРОВАНИЕМ</size>",
                ["UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_ADD_IGNORED"] = "<size=20>ВЫ УВЕРЕНЫ, ЧТО ХОТИТЕ ДОБАВИТЬ {0} В ИГНОРИРОВАНИЕ?</size>",
                ["UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_REMOVE_IGNORED"] = "<size=20>ВЫ УВЕРЕНЫ, ЧТО ХОТИТЕ УБРАТЬ {0} ИЗ ИГНОРИРОВАНИЯ?</size>",
                ["UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_YES_BUTTON"] = "<size=20>ДА, Я УВЕРЕН</size>",
                ["UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_NO_BUTTON"] = "<size=20>НЕТ, Я ПЕРЕДУМАЛ</size>",

                ["UI_CHAT_PANEL_TAKE_PREFIX"] = "<size=10>У ВАС НЕТ ДОСТУПНЫХ ПРЕФИКСОВ</size>",
                ["UI_CHAT_PANEL_TAKE_PREFIX_MULTI"] = "<size=10>ВЫБЕРИТЕ ПРЕФИКСЫ</size>",
                ["UI_CHAT_PANEL_TAKE_COLOR"] = "<size=10>У ВАС НЕТ ДОСТУПНЫХ ЦВЕТОВ</size>",
                ["UI_CHAT_PANEL_TAKE_RANK"] = "<size=10>У ВАС НЕТ ДОСТУПНЫХ РАНГОВ</size>",

                ["UI_CHAT_PAGE_CONTROLLER_DROP_LIST_COUNT"] = "<size=12>СТРАНИЦА {0}</size>",

                ["UI_CHAT_MODAL_INSTRUCTION_TEXT"] = "<b><size=16>Данные настройки включают или отключают выбранный вами функционал чата</size></b><size=10>\n- Личные сообщения - отвечают за получения вами личных сообщений от любых пользователей\n- Оповщение в чате - отвечает за информативные или автоматические сообщения\n- Упоминания в чате - отвечают за @{0} упоминание вас в общем чате\n- Звуковые оповещения - данная функция отвечает за получение звуковых эффектов во время упоминания или личного сообщения\n- Глобальный чат - отвечает за получение каких-либо сообщений в чате</size>",

                ["COMMAND_NOT_PERMISSION"] = "You dont have permissions to use this command",
                ["COMMAND_RENAME_NOTARG"] = "For rename use : /rename New nickname",
                ["COMMAND_RENAME_SUCCES"] = "You have successful changed your name to {0}",

                ["COMMAND_PM_NOTARG"] = "To send pm use : /pm Nickname Message",
                ["COMMAND_PM_NOT_NULL_MSG"] = "Message is empty!",
                ["COMMAND_PM_NOT_USER"] = "User not found or offline",
                ["COMMAND_PM_SUCCESS"] = "Your private message sent successful\nMessage : {0}",
                ["COMMAND_PM_SEND_MSG"] = "Message from {0}\n{1}",

                ["COMMAND_R_NOTARG"] = "For reply use : /r Message",
                ["COMMAND_R_NOTMSG"] = "You dont have any private conversations yet!",

                ["FLOODERS_MESSAGE"] = "You're typing too fast! Please Wait {0} seconds",

                ["PREFIX_SETUP"] = "You have successfully removed the prefix {0}, it is already activated and installed",
                ["COLOR_CHAT_SETUP"] = "You have successfully picked up the <color={0}>chat color</color>, it is already activated and installed",
                ["COLOR_NICK_SETUP"] = "You have successfully taken the <color={0}>nickname color</color>, it is already activated and installed",

                ["PREFIX_RETURNRED"] = "Your prefix {0} expired, it was reset automatically",
                ["COLOR_CHAT_RETURNRED"] = "Action of your <color={0}>color chat</color> over, it is reset automatically",
                ["COLOR_NICK_RETURNRED"] = "Action of your <color={0}>color nick</color> over, it is reset automatically",

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
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FUNC_MESSAGE_MUTE_CHAT"] = "{0} заблокировал чат игроку {1} на {2}\nПричина : {3}",
                ["FUNC_MESSAGE_UNMUTE_CHAT"] = "{0} разблокировал чат игроку {1}",
                ["FUNC_MESSAGE_MUTE_VOICE"] = "{0} заблокировал голос игроку {1} на {2}\nПричина : {3}",
                ["FUNC_MESSAGE_UNMUTE_VOICE"] = "{0} разблокировал голос игроку {1}",
                ["FUNC_MESSAGE_MUTE_ALL_CHAT"] = "Всем игрокам был заблокирован чат",
                ["FUNC_MESSAGE_UNMUTE_ALL_CHAT"] = "Всем игрокам был разблокирован чат",
                ["FUNC_MESSAGE_MUTE_ALL_VOICE"] = "Всем игрокам был заблокирован голос",
                ["FUNC_MESSAGE_MUTE_ALL_ALERT"] = "Блокировка Администратором",
                ["FUNC_MESSAGE_UNMUTE_ALL_VOICE"] = "Всем игрокам был разблокирован голос",

                ["FUNC_MESSAGE_PM_TURN_FALSE"] = "Игрок запретил присылать себе личные сообщения",
                ["FUNC_MESSAGE_ALERT_TURN_FALSE"] = "Игрок запретил уведомлять себя",

                ["FUNC_MESSAGE_ISMUTED_TRUE"] = "Вы не можете отправлять сообщения еще {0}\nВаш чат заблокирован",
                ["FUNC_MESSAGE_NO_ARG_BROADCAST"] = "Вы не можете отправлять пустое сообщение в оповещение!",

                ["UI_ALERT_TITLE"] = "<size=14><b>Уведомление</b></size>",

                ["UI_TITLE_PANEL_LABEL"] = "<size=25>НАСТРОЙКА ЧАТА</size>",
                ["UI_CHAT_PANEL_PREFIX_TITLE"] = "<size=20><b>ПРЕФИКС:</b></size>",
                ["UI_CHAT_PANEL_CHAT_TITLE"] = "<size=20><b>ЧАТ:</b></size>",
                ["UI_CHAT_PANEL_NICK_TITLE"] = "<size=20><b>НИК:</b></size>",
                ["UI_CHAT_PANEL_RANK_TITLE"] = "<size=20><b>РАНГ:</b></size>",

                ["UI_CHAT_PANEL_SETTINGS_TITLE"] = "<size=30><b>НАСТРОЙКИ:</b></size>",
                ["UI_CHAT_PANEL_SETTINGS_PM_TITLE"] = "<size=15><b>ЛИЧНЫЕ СООБЩЕНИЯ</b></size>",
                ["UI_CHAT_PANEL_SETTINGS_ALERT_TITLE"] = "<size=15><b>УПОМИНАНИЯ В ЧАТЕ</b></size>",
                ["UI_CHAT_PANEL_SETTINGS_BROADCAST_TITLE"] = "<size=15><b>ОПОВЕЩЕНИЕ В ЧАТЕ</b></size>",
                ["UI_CHAT_PANEL_SETTINGS_SOUND_TITLE"] = "<size=13><b>ЗВУКОВОЕ ОПОВЕЩЕНИЕ</b></size>",
                ["UI_CHAT_PANEL_SETTINGS_GLOBAL_TITLE"] = "<size=13><b>ГЛОБАЛЬНЫЙ ЧАТ</b></size>",

                ["UI_CHAT_PANEL_INFORMATION_TITLE"] = "<size=30><b>ИНФОРМАЦИЯ:</b></size>",
                ["UI_CHAT_PANEL_INFORMATION_NICK_TITLE"] = "<size=20><b>ВАШ НИК: {0}</b></size>",
                ["UI_CHAT_PANEL_INFORMATION_MUTED_TITLE"] = "<size=20><b>БЛОКИРОВКА ЧАТА: <color=#cc4228>{0}</color></b></size>",
                ["UI_CHAT_PANEL_INFORMATION_MUTED_TITLE_NOT"] = "<size=20><b><color=#A7F64FFF>ОТСУТСТВУЕТ</color></b></size>",
                ["UI_CHAT_PANEL_INFORMATION_IGNORED_TITLE"] = "<size=20><b>ИГНОРИРОВАНИЕ :</b></size>",
                ["UI_CHAT_PANEL_INFORMATION_IGNORED_BUTTON_TITLE"] = "<size=15><b>{0} ЧЕЛОВЕК(А)</b></size>",

                ["UI_CHAT_PANEL_MODERATOR_TITLE"] = "<size=30><b>ПАНЕЛЬ МОДЕРАТОРА:</b></size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_BUTTON_TITLE"] = "<size=17><b>УПРАВЛЕНИЕ БЛОКИРОВКАМИ ЧАТА</b></size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_CHAT_ALL_BUTTON_TITLE"] = "<size=17><b>ЗАБЛОКИРОВАТЬ ВСЕМ ЧАТ</b></size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_VOICE_ALL_BUTTON_TITLE"] = "<size=17><b>ЗАБЛОКИРОВАТЬ ВСЕМ ГОЛОС</b></size>",

                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TITLE"] = "<size=25>УПРАВЛЕНИЕ БЛОКИРОВКАМИ ЧАТА</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TITLE_SEARCH"] = "<size=25>ПОИСК</size>",

                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_MUTE_TITLE"] = "<size=20>ВЫБЕРИТЕ ДЕЙСТВИЕ</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_REASON_MUTE_TITLE"] = "<size=20>ВЫБЕРИТЕ ПРИЧИНУ БЛОКИРОВКИ</size>",

                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_VOICE"] = "<size=20>ЗАБЛОКИРОВАТЬ ГОЛОСОВОЙ ЧАТ</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT"] = "<size=20>ЗАБЛОКИРОВАТЬ ЧАТ</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_VOICE_UNMUTE"] = "<size=20>РАЗБЛОКИРОВАТЬ ГОЛОСОВОЙ ЧАТ</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_UNMUTE"] = "<size=20>РАЗБЛОКИРОВАТЬ ЧАТ</size>",

                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_ACCESS"] = "<size=35>УСПЕШНО</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_NOT_CONNNECTED"] = "<size=30>ИГРОК ВЫШЕЛ С СЕРВЕРА</size>",
                ["UI_CHAT_PANEL_MODERATOR_MUTE_PANEL_TAKE_TYPE_CHAT_ACTION_ITSMUTED"] = "<size=30>ИГРОК УЖЕ ЗАМУЧЕН</size>",

                ["UI_CHAT_PANEL_IGNORED_MENU_TITLE"] = "<size=25>УПРАВЛЕНИЕ ИГНОРИРОВАНИЕМ</size>",
                ["UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_ADD_IGNORED"] = "<size=20>ВЫ УВЕРЕНЫ, ЧТО ХОТИТЕ ДОБАВИТЬ {0} В ИГНОРИРОВАНИЕ?</size>",
                ["UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_REMOVE_IGNORED"] = "<size=20>ВЫ УВЕРЕНЫ, ЧТО ХОТИТЕ УБРАТЬ {0} ИЗ ИГНОРИРОВАНИЯ?</size>",
                ["UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_YES_BUTTON"] = "<size=20>ДА, Я УВЕРЕН</size>",
                ["UI_CHAT_PANEL_IGNORED_MENU_TITLE_ALERT_NO_BUTTON"] = "<size=20>НЕТ, Я ПЕРЕДУМАЛ</size>",

                ["UI_CHAT_PANEL_TAKE_PREFIX"] = "<size=10>У ВАС НЕТ ДОСТУПНЫХ ПРЕФИКСОВ</size>",
                ["UI_CHAT_PANEL_TAKE_PREFIX_MULTI"] = "<size=10>ВЫБЕРИТЕ ПРЕФИКСЫ</size>",
                ["UI_CHAT_PANEL_TAKE_COLOR"] = "<size=10>У ВАС НЕТ ДОСТУПНЫХ ЦВЕТОВ</size>",
                ["UI_CHAT_PANEL_TAKE_RANK"] = "<size=10>У ВАС НЕТ ДОСТУПНЫХ РАНГОВ</size>",

                ["UI_CHAT_PAGE_CONTROLLER_DROP_LIST_COUNT"] = "<size=12>СТРАНИЦА {0}</size>",

                ["UI_CHAT_MODAL_INSTRUCTION_TEXT"] = "<b><size=16>Данные настройки включают или отключают выбранный вами функционал чата</size></b><size=10>\n- Личные сообщения - отвечают за получения вами личных сообщений от любых пользователей\n- Оповщение в чате - отвечает за информативные или автоматические сообщения\n- Упоминания в чате - отвечают за @{0} упоминание вас в общем чате\n- Звуковые оповещения - данная функция отвечает за получение звуковых эффектов во время упоминания или личного сообщения\n- Глобальный чат - отвечает за получение каких-либо сообщений в чате</size>",

                ["COMMAND_NOT_PERMISSION"] = "У вас недостаточно прав для данной команды",
                ["COMMAND_RENAME_NOTARG"] = "Используйте команду так : /rename Новый Ник",
                ["COMMAND_RENAME_SUCCES"] = "Вы успешно изменили ник на {0}",

                ["COMMAND_PM_NOTARG"] = "Используйте команду так : /pm Ник Игрока Сообщение",
                ["COMMAND_PM_NOT_NULL_MSG"] = "Вы не можете отправлять пустое сообщение",
                ["COMMAND_PM_NOT_USER"] = "Игрок не найден или не в сети",
                ["COMMAND_PM_SUCCESS"] = "Ваше сообщение успешно доставлено\nСообщение : {0}",
                ["COMMAND_PM_SEND_MSG"] = "Сообщение от {0}\n{1}",

                ["COMMAND_R_NOTARG"] = "Используйте команду так : /r Сообщение",
                ["COMMAND_R_NOTMSG"] = "Вам или вы ещё не писали игроку в личные сообщения!",

                ["FLOODERS_MESSAGE"] = "Вы пишите слишком быстро! Подождите {0} секунд",

                ["PREFIX_SETUP"] = "Вы успешно забрали префикс {0}, он уже активирован и установлен",
                ["COLOR_CHAT_SETUP"] = "Вы успешно забрали <color={0}>цвет чата</color>, он уже активирован и установлен",
                ["COLOR_NICK_SETUP"] = "Вы успешно забрали <color={0}>цвет ника</color>, он уже активирован и установлен",

                ["PREFIX_RETURNRED"] = "Действие вашего префикса {0} окончено, он сброшен автоматически",
                ["COLOR_CHAT_RETURNRED"] = "Действие вашего <color={0}>цвета чата</color> окончено, он сброшен автоматически",
                ["COLOR_NICK_RETURNRED"] = "Действие вашего <color={0}>цвет чата</color> окончено, он сброшен автоматически",

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
            }, this, "ru");
           
            PrintWarning("Языковой файл загружен успешно");
        }
        #endregion

        #region Helpers
        public void Log(string LoggedMessage) => LogToFile("IQChatLogs", LoggedMessage, this);
        public static string FormatTime(TimeSpan time)
        {
            string result = string.Empty;
            if (time.Days != 0)
                result += $"{Format(time.Days, "дней", "дня", "день")} ";

            if (time.Hours != 0)
                result += $"{Format(time.Hours, "часов", "часа", "час")} ";

            if (time.Minutes != 0)
                result += $"{Format(time.Minutes, "минут", "минуты", "минута")} ";

            if (time.Seconds != 0)
                result += $"{Format(time.Seconds, "секунд", "секунды", "секунд")} ";

            return result;
        }
        private static string Format(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;

            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                return $"{units} {form1}";

            if (tmp >= 2 && tmp <= 4)
                return $"{units} {form2}";

            return $"{units} {form3}";
        }
        private static string HexToRustFormat(string hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            sb.Clear();
            return sb.AppendFormat("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a).ToString();
        }
        #endregion

        #region ChatFunc

        public Dictionary<ulong, double> Flooders = new Dictionary<ulong, double>();
        void ReplyChat(Chat.ChatChannel channel, BasePlayer player, string OutMessage)
        {
            var MessageSetting = config.MessageSetting;
            if (MessageSetting.AntiSpamActivate)
                if (!permission.UserHasPermission(player.UserIDString, MessageSetting.PermAdminImmunitetAntispam))
                {
                    if (!Flooders.ContainsKey(player.userID))
                        Flooders.Add(player.userID, CurrentTime() + MessageSetting.FloodTime);
                    else
                        if (Flooders[player.userID] > CurrentTime())
                        {
                            ReplySystem(Chat.ChatChannel.Global, player, GetLang("FLOODERS_MESSAGE", player.UserIDString, Convert.ToInt32(Flooders[player.userID] - CurrentTime())));
                            return;
                        }

                    Flooders[player.userID] = MessageSetting.FloodTime + CurrentTime();
                }

            if (channel == Chat.ChatChannel.Global)
            {
                var List = IQModalMenu ? BasePlayer.activePlayerList.Where(p => ChatSettingUser[p.userID].GlobalChatTurn) : BasePlayer.activePlayerList;
                foreach (BasePlayer p in List)
                {
                    if (OutMessage.Contains("@")) 
                    {
                        if (OutMessage.ToLower().Contains(p.displayName.ToLower()))
                            if (ChatSettingUser[p.userID].AlertTurn)
                            {
                                ReplySystem(Chat.ChatChannel.Global, p, $"<size=16>{OutMessage}</size>", MessageSetting.AlertPlayerTitle, player.UserIDString, MessageSetting.AlertPlayerColor);
                                Effect.server.Run(MessageSetting.SoundAlertPlayer, p.GetNetworkPosition());
                            } else p.SendConsoleCommand("chat.add", new object[] { (int)channel, player.userID, OutMessage });
                         else p.SendConsoleCommand("chat.add", new object[] { (int)channel, player.userID, OutMessage });
                    }
                    else p.SendConsoleCommand("chat.add", new object[] { (int)channel, player.userID, OutMessage });
                }

                PrintToConsole(OutMessage);
            }
            if (channel == Chat.ChatChannel.Team)
            {
                RelationshipManager.PlayerTeam Team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                if (Team == null) return;
                foreach (var FindPlayers in Team.members)
                {
                    BasePlayer TeamPlayer = BasePlayer.FindByID(FindPlayers);
                    if (TeamPlayer == null) continue;

                    TeamPlayer.SendConsoleCommand("chat.add", channel, player.userID, OutMessage);
                }
            }
            if (channel == Chat.ChatChannel.Cards)
            {
                if (!player.isMounted)
                    return;

                CardTable cardTable = player.GetMountedVehicle() as CardTable;
                if (cardTable == null || !cardTable.GameController.PlayerIsInGame(player))
                    return;

                List<Network.Connection> PlayersCards = new List<Network.Connection>();
                cardTable.GameController.GetConnectionsInGame(PlayersCards);
                if (PlayersCards == null || PlayersCards.Count == 0)
                    return;

                foreach (var PCard in PlayersCards)
                {
                    BasePlayer PlayerInRound = BasePlayer.FindByID(PCard.userid);
                    if (PlayerInRound == null) return;
                    PlayerInRound.SendConsoleCommand("chat.add", channel, player.userID, OutMessage);
                }
            }
        }

        void ReplySystem(Chat.ChatChannel channel, BasePlayer player, string Message,string CustomPrefix = "", string CustomAvatar = "", string CustomHex = "")
        {
            string Prefix = string.IsNullOrEmpty(CustomPrefix) ? config.MessageSetting.BroadcastTitle : CustomPrefix;
            ulong Avatar = string.IsNullOrEmpty(CustomAvatar) ? config.MessageSetting.Steam64IDAvatar : ulong.Parse(CustomAvatar);
            string Hex = string.IsNullOrEmpty(CustomHex) ? config.MessageSetting.BroadcastColor : CustomHex;

            string FormatMessage = $"{Prefix}<color={Hex}>{Message}</color>";
            if (channel == Chat.ChatChannel.Global)
                player.SendConsoleCommand("chat.add", channel, Avatar, FormatMessage);         
        }

        void ReplyBroadcast(string Message, string CustomPrefix = "", string CustomAvatar = "", Boolean AdminAlert = false)
        {
            foreach (var p in !AdminAlert ? BasePlayer.activePlayerList.Where(p => ChatSettingUser[p.userID].BroadcastTurn || (IQModalMenu && ChatSettingUser[p.userID].GlobalChatTurn)) : BasePlayer.activePlayerList)
                ReplySystem(Chat.ChatChannel.Global, p, Message, CustomPrefix, CustomAvatar);
        }

        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;
        
        #endregion

        #region API

        void API_SEND_PLAYER(BasePlayer player,string PlayerFormat, string Message, string Avatar, Chat.ChatChannel channel = Chat.ChatChannel.Global)
        {
            var MessageSettings = config.MessageSetting;
            string OutMessage = Message;

            if (MessageSettings.FormatingMessage)
                OutMessage = $"{Message.ToLower().Substring(0, 1).ToUpper()}{Message.Remove(0, 1).ToLower()}";

            if (MessageSettings.UseBadWords)
                foreach (var DetectedMessage in OutMessage.Split(' '))
                    if (MessageSettings.BadWords.Contains(DetectedMessage.ToLower()))
                        OutMessage = OutMessage.Replace(DetectedMessage, MessageSettings.ReplaceBadWord);

            player.SendConsoleCommand("chat.add", channel, ulong.Parse(Avatar), $"{PlayerFormat}: {OutMessage}");
            PrintToConsole($"{PlayerFormat}: {OutMessage}");
        }
        void API_SEND_PLAYER_PM(BasePlayer player, string DisplayName, string Message)
        {
            ReplySystem(Chat.ChatChannel.Global, player, GetLang("COMMAND_PM_SEND_MSG", player.UserIDString, DisplayName, Message));
            if(ChatSettingUser[player.userID].SoundTurn)
            Effect.server.Run(config.MessageSetting.SoundPM, player.GetNetworkPosition());
        }
        void API_SEND_PLAYER_CONNECTED(BasePlayer player, string DisplayName, string country, string userID)
        {
            var Alert = config.AlertSettings;
            if (Alert.ConnectedAlert)
            {
                string Avatar = Alert.ConnectedAvatarUse ? userID : "";
                if (config.AlertSettings.ConnectedWorld)
                     ReplyBroadcast(GetLang("WELCOME_PLAYER_WORLD", player.UserIDString, DisplayName, country), "", Avatar);   
                else ReplyBroadcast(GetLang("WELCOME_PLAYER", player.UserIDString, DisplayName), "", Avatar);
            }
        }
        void API_SEND_PLAYER_DISCONNECTED(BasePlayer player, string DisplayName, string reason, string userID)
        {
            var Alert = config.AlertSettings;
            if (Alert.DisconnectedAlert)
            {
                string Avatar = Alert.ConnectedAvatarUse ? userID : "";
                string LangLeave = config.AlertSettings.DisconnectedReason ? GetLang("LEAVE_PLAYER_REASON",player.UserIDString, DisplayName, reason) : GetLang("LEAVE_PLAYER", player.UserIDString, DisplayName);
                ReplyBroadcast(LangLeave, "", Avatar);
            }
        }
        void API_ALERT(string Message, Chat.ChatChannel channel = Chat.ChatChannel.Global, string CustomPrefix = "", string CustomAvatar = "", string CustomHex = "")
        {
            foreach (var p in BasePlayer.activePlayerList)
                ReplySystem(channel, p, Message, CustomPrefix, CustomAvatar, CustomHex);
        }
        void API_ALERT_PLAYER(BasePlayer player,string Message, string CustomPrefix = "", string CustomAvatar = "", string CustomHex = "") => ReplySystem(Chat.ChatChannel.Global, player, Message, CustomPrefix, CustomAvatar, CustomHex);
        void API_ALERT_PLAYER_UI(BasePlayer player, string Message) => UIAlert(player, Message);
        bool API_CHECK_MUTE_CHAT(ulong ID) => IsMutedUser(ID);
        bool API_CHECK_VOICE_CHAT(ulong ID) => IsMutedVoiceUser(ID);
        bool API_IS_IGNORED(ulong UserHas, ulong User) => IsIgnored(UserHas, User);
        string API_GET_PREFIX(ulong ID)
        {
            var DataPlayer = ChatSettingUser[ID];
            return DataPlayer.ChatPrefix;
        }
        string API_GET_CHAT_COLOR(ulong ID)
        {
            var DataPlayer = ChatSettingUser[ID];
            return DataPlayer.MessageColor;
        }
        string API_GET_NICK_COLOR(ulong ID)
        {
            var DataPlayer = ChatSettingUser[ID];
            return DataPlayer.NickColor;
        }
        string API_GET_DEFUALT_PRFIX() => (string)config.AutoSetupSetting.ReturnDefaultSetting.PrefixDefault;
        string API_GET_DEFUALT_COLOR_NICK() => (string)config.AutoSetupSetting.ReturnDefaultSetting.NickDefault;
        string API_GET_DEFUALT_COLOR_CHAT() => (string)config.AutoSetupSetting.ReturnDefaultSetting.MessageDefault;
        #endregion

        #region IQModalMenu

        #region Command

        [ConsoleCommand("iq.chat.modal")]
        private void ConsoleCommandChatModal(ConsoleSystem.Arg arg)
        {
            if (!IQModalMenu) return;
            BasePlayer player = arg.Player();
            if (player == null) return;

            IQChat_UI_Modal(player);
        }

        #endregion

        #region Interface
        private static string IQMODAL_CONTENT = "IQMODAL_CONTENT";

        private void IQChat_UI_Modal(BasePlayer player)
        {
            CuiElementContainer container = new CuiElementContainer();
            CuiHelper.DestroyUi(player, IQMODAL_CONTENT);
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN);
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL);
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN_IGNORED_UI);
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN_CHAT_MUTE_PANEL);
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN_IGNORED_UI_TITLE);
            CuiHelper.DestroyUi(player, IQCHAT_PARENT_MAIN_IGNORED_UI);
            var Interface = config.InterfaceChat;
            float FadeIn = 0.3f;
            var DataPlayer = ChatSettingUser[player.userID];

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.9979 1" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) }
            }, IQMODAL_CONTENT, IQCHAT_PARENT_MAIN_CHAT_PANEL);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04152405 0.921147", AnchorMax = "0.3166667 1" },
                Text = { FadeIn = FadeIn, Text = lang.GetMessage("UI_TITLE_PANEL_LABEL", this, player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL, "TITLE_LABEL");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.94" },
                Image = { FadeIn = FadeIn, Color = "0 0 0 0" }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT);

            #region User Interface

            #region Settings

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.4798867 0.9036886", AnchorMax = "0.7550293 0.9825416" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_SETTINGS");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.4798867 0.8688836", AnchorMax = "0.654023 0.9170448" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_PM_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_SETTINGS_PM_TITLE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.4798867 0.8178514", AnchorMax = "0.654023 0.8660126" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_BROADCAST_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "UI_CHAT_PANEL_SETTINGS_BROADCAST_TITLE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.4798867 0.7668192", AnchorMax = "0.654023 0.8149804" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_ALERT_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_SETTINGS_ALERT_TITLE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.4798867 0.715787", AnchorMax = "0.654023 0.7639482" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_SOUND_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "UI_CHAT_PANEL_SETTINGS_SOUND_TITLE");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.4798867 0.6647548", AnchorMax = "0.654023 0.712916" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_SETTINGS_GLOBAL_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "UI_CHAT_PANEL_SETTINGS_SOUND_TITLE");

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT,
                Name = "TURNED_PM_BOX",
                Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.6080466 0.884999", AnchorMax = $"0.6229891 0.9172297" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
            });

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT,
                Name = "TURNED_BROADCAST_BOX",
                Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.6080466 0.8339668", AnchorMax = $"0.6229891 0.8661975" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
            });

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT,
                Name = "TURNED_SOUND_BOX",
                Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.6080466 0.7829346", AnchorMax = $"0.6229891 0.8151653" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.3", UseGraphicAlpha = true }
                }
            });

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT,
                Name = "TURNED_ALERT_BOX",
                Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.6080466 0.7319024", AnchorMax = $"0.6229891 0.7641331" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.2", UseGraphicAlpha = true }
                }
            });

            container.Add(new CuiElement
            {
                Parent = IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT,
                Name = "TURNED_GLOBAL_CHAT_BOX",
                Components =
                {
                        new CuiImageComponent { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexPanel) },
                        new CuiRectTransformComponent{ AnchorMin = $"0.6080466 0.6808702", AnchorMax = $"0.6229891 0.7131009" },
                        new CuiOutlineComponent { Color = HexToRustFormat(Interface.HexTitle), Distance = "-1.2 1.3", UseGraphicAlpha = true }
                }
            });

            #endregion

            #region Information

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04152404 0.6500363", AnchorMax = "0.3436781 0.7209859" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION");

            string FormatTimeMute = IsMutedUser(player.userID) ? FormatTime(TimeSpan.FromSeconds(DataPlayer.MuteChatTime - CurrentTime())) : GetLang("UI_CHAT_PANEL_INFORMATION_MUTED_TITLE_NOT", player.UserIDString);
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04152404 0.5796079", AnchorMax = "0.4 0.6287344" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_MUTED_TITLE", player.UserIDString, FormatTimeMute), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION_MUTE_TIME");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04152404 0.5326046", AnchorMax = "0.2 0.5817311" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_IGNORED_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION_IGNORED_TITLE");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.2033799 0.5386046", AnchorMax = $"0.3609195 0.5797311" },
                Button = { FadeIn = FadeIn, Command = "iq.chat ignore.controller open", Color = HexToRustFormat(Interface.HexButton) },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_IGNORED_BUTTON_TITLE", player.UserIDString, DataPlayer.IgnoredUsers.Count), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION_IGNORED_TITLE_COUNT");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.998 0.2175566" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexTitle) }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "PANEL_INFO_INSTRUCTION");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.08908045 0", AnchorMax = "0.8988506 1" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_MODAL_INSTRUCTION_TEXT",player.UserIDString, player.displayName), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleLeft }
            },  "PANEL_INFO_INSTRUCTION", "PANEL_INFO_INSTRUCTION_LABEL");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.8988506 0", AnchorMax = "1 1" },
                Text = { FadeIn = FadeIn, Text = "<b><size=30>FAQ</size></b>", Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter }
            }, "PANEL_INFO_INSTRUCTION", "PANEL_INFO_INSTRUCTION_LABEL_FAQ");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.006321848 0.1012346", AnchorMax = "0.08333334 0.891358" },
                Image = { Color = HexToRustFormat(Interface.HexLabel), Sprite = "assets/icons/player_assist.png" }
            }, $"PANEL_INFO_INSTRUCTION", "PANEL_INFO_INSTRUCTION_SPRITE");

            #endregion

            #region Rank
            if (IQRankSystem)
                if (config.ReferenceSetting.IQRankSystems.UseRankSystem)
                {
                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.04152405 0.761226", AnchorMax = "0.1568966 0.8103468" },
                        Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_RANK_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
                    }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_RANK");

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "0.1671733 0.7653192", AnchorMax = "0.3609195 0.81" },
                        Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) }
                    }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "RANK_TAKED_PANEL");
                }

            #endregion

            #region Chat

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04152405 0.8117096", AnchorMax = "0.1568966 0.8649233" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_CHAT_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_CHAT");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.1671733 0.8198956", AnchorMax = "0.3609195 0.8645764" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "CHAT_TAKED_PANEL");

            #endregion

            #region Nick

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04152405 0.8717443", AnchorMax = "0.1568966 0.9194997" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_NICK_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_NICK");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.1671733 0.8744721", AnchorMax = "0.3609195 0.9191527" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "NICKED_TAKED_PANEL");

            #endregion

            #region Prefix

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04152405 0.9222281", AnchorMax = "0.1568966 0.9740761" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_PREFIX_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_PREFIX");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.1671733 0.9290484", AnchorMax = "0.3609195 0.9737292" },
                Image = { FadeIn = FadeIn, Color = HexToRustFormat(Interface.HexButton) }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "PREFIX_TAKED_PANEL");

            #endregion

            #region Moderation Panel

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04152405 0.392139", AnchorMax = "0.4 0.4742948" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_MODERATOR_TITLE", player.UserIDString), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_SETTINGS");

            if (permission.UserHasPermission(player.UserIDString, PermMuteMenu))
            {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"0.04152405 0.3221469", AnchorMax = $"0.9584759 0.3930984" },
                    Button = { FadeIn = FadeIn, Command = "iq.chat mute.controller open.menu", Color = HexToRustFormat(Interface.HexButton) },
                    Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_MODERATOR_MUTE_BUTTON_TITLE", player.UserIDString, DataPlayer.IgnoredUsers.Count), Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.MiddleCenter }
                }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_PANEL_MODERATION_BUTTON_MUTE");
            }

            #endregion

            #endregion

            CuiHelper.AddUi(player, container);
            UpdateNickModal(player);
            UI_Prefix_Taked(player);
            UI_Nick_Taked(player);
            UI_Chat_Taked(player);
            if (IQRankSystem)
                if (config.ReferenceSetting.IQRankSystems.UseRankSystem)
                    UI_Rank_Taked(player);
            TurnedPM(player);
            TurnedAlert(player);
            TurnedBroadcast(player);
            TurnedSound(player);
            TurnedGlobalChat(player);
            if (player.IsAdmin)
            {
                TurnedAdminButtonMuteAllVoice(player);
                TurnedAdminButtonMuteAllChat(player);
            }
        }

        void UpdateNickModal(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "TITLE_LABEL_INFORMATION_MY_NICK");
            CuiElementContainer container = new CuiElementContainer();

            float FadeIn = 0.3f;
            var Interface = config.InterfaceChat;
            var DataPlayer = ChatSettingUser[player.userID];

            string Prefix = string.Empty;
            string Rank = IQRankSystem ? config.ReferenceSetting.IQRankSystems.UseRankSystem ? IQRankGetRank(player.userID) : string.Empty : string.Empty;
            if (config.MessageSetting.MultiPrefix)
            {
                if (DataPlayer.MultiPrefix != null)
                    for (int i = 0; i < DataPlayer.MultiPrefix.Count; i++)
                        Prefix += DataPlayer.MultiPrefix[i];
            }
            else Prefix = DataPlayer.ChatPrefix;
            string ResultNick = !String.IsNullOrEmpty(Rank) ? $"<b>[{Rank}] {Prefix}<color={DataPlayer.NickColor}>{player.displayName}</color> : <color={DataPlayer.MessageColor}> я лучший</color></b>" : $"<b>{Prefix}<color={DataPlayer.NickColor}>{player.displayName}</color> : <color={DataPlayer.MessageColor}> я лучший</color></b>";
            int Size = ResultNick.Length >= 300 ? 7 : ResultNick.Length >= 200 ? 10 : ResultNick.Length >= 100 ? 15 : 20;

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04152404 0.4785125", AnchorMax = "0.9486688 0.5385504" },
                Text = { FadeIn = FadeIn, Text = GetLang("UI_CHAT_PANEL_INFORMATION_NICK_TITLE", player.UserIDString, $"<size={Size}>{ResultNick}</size>"), Font = "robotocondensed-regular.ttf", Color = HexToRustFormat(Interface.HexLabel), Align = TextAnchor.UpperLeft }
            }, IQCHAT_PARENT_MAIN_CHAT_PANEL_CONTENT, "TITLE_LABEL_INFORMATION_MY_NICK");

            CuiHelper.AddUi(player, container);
        }

        #endregion

        #endregion
    }
}
