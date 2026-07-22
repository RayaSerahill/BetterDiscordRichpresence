using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using DiscordRPC;
using BetterDiscordRichPresence.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using BetterDiscordRichPresence.Windows;
using ECommons;
using ECommons.GameFunctions;

namespace BetterDiscordRichPresence
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => PluginInterface.Manifest.Name;

        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; set; } = null!;
        [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
        [PluginService] private static IClientState ClientState { get; set; } = null!;
        [PluginService] private static IDataManager DataManager { get; set; } = null!;
        [PluginService] private static IFramework Framework { get; set; } = null!;
        [PluginService] private static IObjectTable ObjectTable { get; set; } = null!;
        [PluginService] private static IUnlockState UnlockState { get; set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] private static IPartyList PartyList { get; set; } = null!;

        private const string CommandName = "/drp";
        private static readonly TimeSpan WidgetLoginUpdateDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan WidgetLoginUpdateTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan WidgetAutomaticUpdateInterval = TimeSpan.FromHours(24);

        public Configuration Configuration { get; }
        private readonly WindowSystem windowSystem = new("BetterDiscordRichPresence");
        private readonly WidgetService widgetService = new();
        private readonly WidgetPlaceholderResolver widgetPlaceholderResolver = new();
        private readonly StatusTextPlaceholderResolver statusTextPlaceholderResolver = new();
        private readonly CancellationTokenSource disposeTokenSource = new();
        private readonly ConfigWindow configWindow;
        private readonly PlaceholderWindow placeholderWindow;
        private readonly GuideWindow guideWindow;
        private DiscordService? discordService;
        private DateTime startTime;
        private bool pendingTerritoryUpdate;
        private DateTime territoryUpdateTime;
        private ExcelSheet<TerritoryType>? territories;
        private ExcelSheet<TripleTriadCard>? tripleTriadCards;
        private ExcelSheet<Mount>? mounts;
        private ExcelSheet<Companion>? companions;
        private int tripleTriadCardTotal = -1;
        private int mountTotal = -1;
        private int companionTotal = -1;

        private DateTime nextPartyCheckTime = DateTime.MinValue;
        private int lastPartySize = -1;
        private string lastPartyState = string.Empty;
        private bool pendingLoginWidgetUpdate;
        private DateTime loginWidgetUpdateTime;
        private DateTime loginWidgetUpdateDeadline;
        private DateTime nextAutomaticWidgetUpdateTime;
        private Task? automaticWidgetUpdateTask;

        public Plugin()
        {
            ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            nextAutomaticWidgetUpdateTime = NormalizeUtc(Configuration.WidgetNextAutomaticUpdateUtc);
            configWindow = new ConfigWindow(this);
            placeholderWindow = new PlaceholderWindow(this);
            guideWindow = new GuideWindow();
            windowSystem.AddWindow(configWindow);
            windowSystem.AddWindow(placeholderWindow);
            windowSystem.AddWindow(guideWindow);

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Discord Rich Presence configuration"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

            ClientState.TerritoryChanged += OnTerritoryChanged;
            ClientState.Login += OnLogin;
            ClientState.Logout += OnLogout;
            Framework.Update += OnFrameworkUpdate;
        }

        public void Dispose()
        {
            pendingLoginWidgetUpdate = false;
            windowSystem.RemoveAllWindows();
            configWindow.Dispose();
            placeholderWindow.Dispose();
            guideWindow.Dispose();
            CommandManager.RemoveHandler(CommandName);

            if (discordService != null)
            {
                discordService.ClearPresence();
                discordService.Dispose();
            }

            ClientState.TerritoryChanged -= OnTerritoryChanged;
            ClientState.Login -= OnLogin;
            ClientState.Logout -= OnLogout;
            Framework.Update -= OnFrameworkUpdate;

            disposeTokenSource.Cancel();
            widgetService.Dispose();
            disposeTokenSource.Dispose();
        }

        private void InitializeDiscord()
        {
            discordService ??= new DiscordService(this);
            discordService.Initialize();
            startTime = DateTime.UtcNow;
        }

        private void OnLogin()
        {
            startTime = DateTime.UtcNow;
            lastPartySize = -1;
            lastPartyState = string.Empty;
            pendingLoginWidgetUpdate = true;
            loginWidgetUpdateTime = DateTime.UtcNow.Add(WidgetLoginUpdateDelay);
            loginWidgetUpdateDeadline = DateTime.UtcNow.Add(WidgetLoginUpdateTimeout);
            UpdateRichPresence();
        }

        private void OnTerritoryChanged(uint _)
        {
            pendingTerritoryUpdate = true;
            territoryUpdateTime = DateTime.UtcNow.AddSeconds(5);
            startTime = DateTime.UtcNow;
        }

        private void OnFrameworkUpdate(IFramework _)
        {
            if (!ClientState.IsLoggedIn)
                return;

            if (DateTime.UtcNow < nextPartyCheckTime)
                return;

            nextPartyCheckTime = DateTime.UtcNow.AddSeconds(1);
            TrySendLoginWidgetUpdate();
            TrySendAutomaticWidgetUpdate();

            var partySize = GetPartySize();
            var partyState = GetPartyStateSignature();

            if (partySize != lastPartySize || partyState != lastPartyState)
            {
                lastPartySize = partySize;
                lastPartyState = partyState;
                UpdateRichPresence();
            }
        }

        private void DrawUI()
        {
            if (pendingTerritoryUpdate && DateTime.UtcNow >= territoryUpdateTime)
            {
                pendingTerritoryUpdate = false;
                UpdateRichPresence();
            }

            windowSystem.Draw();
        }

        public void ToggleConfigUI() => configWindow.Toggle();

        internal void OpenPlaceholderWindow() => placeholderWindow.Open();

        internal void OpenGuideWindow() => guideWindow.Open();

        internal bool IsCurrentCharacterAllowedForWidget()
        {
            var characterFilter = Configuration.WidgetCharacterNameFilter?.Trim();
            if (string.IsNullOrEmpty(characterFilter))
                return true;

            var currentCharacterName = ObjectTable.LocalPlayer?.Name.TextValue;
            return !string.IsNullOrEmpty(currentCharacterName)
                   && string.Equals(
                       characterFilter,
                       currentCharacterName.Trim(),
                       StringComparison.OrdinalIgnoreCase);
        }

        internal IReadOnlyList<WidgetPlaceholderValue> GetWidgetPlaceholderValues()
        {
            try
            {
                return widgetPlaceholderResolver.GetValues(GetWidgetPlaceholderContext());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to read current widget placeholder values.");
                return widgetPlaceholderResolver.GetValues(
                    new WidgetPlaceholderContext(
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty));
            }
        }

        private void OnCommand(string command, string args)
        {
            var trimmedArgs = args.Trim();

            if (string.IsNullOrWhiteSpace(trimmedArgs))
            {
                ToggleConfigUI();
            }
            else if (trimmedArgs.Equals("refresh", StringComparison.OrdinalIgnoreCase))
            {
                UpdateRichPresence();
            }
            else if (trimmedArgs.Equals("debug", StringComparison.OrdinalIgnoreCase))
            {
                configWindow.ShowDebugTab();
            }
        }

        private void OnLogout(int type, int code)
        {
            pendingLoginWidgetUpdate = false;
            lastPartySize = -1;
            lastPartyState = string.Empty;

            if (discordService?.IsInitialized == true)
                discordService.ClearPresence();
        }

        internal async Task<WidgetUpdateResult> UpdateWidgetAsync()
        {
            ResetAutomaticWidgetUpdateTimer();
            return await SendWidgetUpdateAsync().ConfigureAwait(false);
        }

        internal void ResetAutomaticWidgetUpdateTimer(bool save = true)
            => ScheduleNextAutomaticWidgetUpdate(DateTime.UtcNow, save);

        internal WidgetAutomaticUpdateDebugInfo GetAutomaticWidgetUpdateDebugInfo()
        {
            var now = DateTime.UtcNow;
            var nextUpdateUtc = nextAutomaticWidgetUpdateTime == DateTime.MinValue
                ? now
                : nextAutomaticWidgetUpdateTime;
            var remaining = nextUpdateUtc <= now
                ? TimeSpan.Zero
                : nextUpdateUtc - now;

            return new WidgetAutomaticUpdateDebugInfo(
                WidgetAutomaticUpdateInterval,
                nextUpdateUtc,
                remaining,
                automaticWidgetUpdateTask is { IsCompleted: false },
                ClientState.IsLoggedIn,
                Configuration.IsWidgetConfigured(),
                IsCurrentCharacterAllowedForWidget());
        }

        private async Task<WidgetUpdateResult> SendWidgetUpdateAsync()
        {
            if (!Configuration.IsWidgetConfigured())
                return new WidgetUpdateResult(false, "Complete every widget field before updating.");

            if (!IsCurrentCharacterAllowedForWidget())
                return new WidgetUpdateResult(
                    false,
                    "The current character does not match the widget character filter.");

            try
            {
                var template = WidgetUpdateRequest.FromConfiguration(Configuration);
                var context = GetWidgetPlaceholderContext();
                var request = widgetPlaceholderResolver.Resolve(template, context);
                return await widgetService.UpdateAsync(request, disposeTokenSource.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to prepare the Discord widget update.");
                return new WidgetUpdateResult(false, $"Widget update failed: {ex.Message}");
            }
        }

        private void TrySendAutomaticWidgetUpdate()
        {
            if (automaticWidgetUpdateTask is { IsCompleted: false })
                return;

            automaticWidgetUpdateTask = null;

            var now = DateTime.UtcNow;
            if (pendingLoginWidgetUpdate || !IsAutomaticWidgetUpdateDue(now))
                return;

            if (!Configuration.IsWidgetConfigured())
                return;

            if (!IsCurrentCharacterAllowedForWidget())
                return;

            ScheduleNextAutomaticWidgetUpdate(now);
            automaticWidgetUpdateTask = SendAutomaticWidgetUpdateAsync();
        }

        private async Task SendAutomaticWidgetUpdateAsync()
        {
            try
            {
                var result = await SendWidgetUpdateAsync().ConfigureAwait(false);

                if (result.Success)
                    Log.Information("Updated the Discord widget automatically.");
                else
                    Log.Warning($"Discord widget automatic update failed: {result.Message}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update the Discord widget automatically.");
            }
        }

        private void TrySendLoginWidgetUpdate()
        {
            var now = DateTime.UtcNow;
            if (!pendingLoginWidgetUpdate || now < loginWidgetUpdateTime)
                return;

            if (!Configuration.IsWidgetConfigured())
            {
                pendingLoginWidgetUpdate = false;
                return;
            }

            if (ObjectTable.LocalPlayer == null && now < loginWidgetUpdateDeadline)
            {
                loginWidgetUpdateTime = now.AddSeconds(1);
                return;
            }

            if (!IsCurrentCharacterAllowedForWidget())
            {
                pendingLoginWidgetUpdate = false;
                Log.Information("Skipped the Discord widget login update because the character filter did not match.");
                return;
            }

            if (automaticWidgetUpdateTask is { IsCompleted: false })
            {
                loginWidgetUpdateTime = now.AddSeconds(1);
                return;
            }

            if (!IsAutomaticWidgetUpdateDue(now))
            {
                pendingLoginWidgetUpdate = false;
                return;
            }

            try
            {
                var context = GetWidgetPlaceholderContext();
                if (!string.IsNullOrEmpty(context.FreeCompanyTag)
                    && string.IsNullOrEmpty(context.FreeCompanyName)
                    && now < loginWidgetUpdateDeadline)
                {
                    loginWidgetUpdateTime = now.AddSeconds(1);
                    return;
                }

                pendingLoginWidgetUpdate = false;
                ScheduleNextAutomaticWidgetUpdate(now);
                automaticWidgetUpdateTask = SendLoginWidgetUpdateAsync(context);
            }
            catch (Exception ex)
            {
                pendingLoginWidgetUpdate = false;
                Log.Error(ex, "Failed to read widget placeholder data after character login.");
            }
        }

        private async Task SendLoginWidgetUpdateAsync(WidgetPlaceholderContext context)
        {
            try
            {
                var template = WidgetUpdateRequest.FromConfiguration(Configuration);
                var request = widgetPlaceholderResolver.Resolve(template, context);
                var result = await widgetService.UpdateAsync(request, disposeTokenSource.Token)
                    .ConfigureAwait(false);

                if (result.Success)
                    Log.Information("Updated the Discord widget after character login.");
                else
                    Log.Warning($"Discord widget login update failed: {result.Message}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update the Discord widget after character login.");
            }
        }

        private bool IsAutomaticWidgetUpdateDue(DateTime now)
            => nextAutomaticWidgetUpdateTime == DateTime.MinValue
               || now >= nextAutomaticWidgetUpdateTime;

        private void ScheduleNextAutomaticWidgetUpdate(DateTime now, bool save = true)
        {
            nextAutomaticWidgetUpdateTime = now.Add(WidgetAutomaticUpdateInterval);
            Configuration.WidgetNextAutomaticUpdateUtc = nextAutomaticWidgetUpdateTime;

            if (save)
                Configuration.Save();
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == DateTime.MinValue)
                return value;

            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
        }

        private unsafe WidgetPlaceholderContext GetWidgetPlaceholderContext()
        {
            var character = ObjectTable.LocalPlayer;
            var freeCompanyTag = character?.CompanyTag.TextValue ?? string.Empty;
            var freeCompanyName = ReadFreeCompanyName(freeCompanyTag);
            var characterName = character?.Name.TextValue ?? string.Empty;
            var currentWorld = string.Empty;
            var homeWorld = string.Empty;
            var job = string.Empty;
            var jobAbbreviation = string.Empty;
            var level = string.Empty;

            if (character != null)
            {
                var freeCompanyValues = ResolveWidgetFreeCompanyValues(
                    characterName,
                    character.HomeWorld.RowId,
                    character.CurrentWorld.RowId,
                    freeCompanyName,
                    freeCompanyTag);

                freeCompanyName = freeCompanyValues.FreeCompanyName;
                freeCompanyTag = freeCompanyValues.FreeCompanyTag;
                currentWorld = character.CurrentWorld.Value.Name.ToString();
                homeWorld = character.HomeWorld.Value.Name.ToString();
                job = character.ClassJob.Value.Name.ToString();
                jobAbbreviation = character.ClassJob.Value.Abbreviation.ToString();
                level = character.Level.ToString();
            }

            return new WidgetPlaceholderContext(
                freeCompanyName,
                freeCompanyTag,
                GetTripleTriadProgress(),
                GetMountsCollected(),
                GetMinionsCollected(),
                characterName,
                currentWorld,
                homeWorld,
                GetCurrentLocationName(),
                job,
                jobAbbreviation,
                level,
                GetWidgetPartySize());
        }

        private unsafe string ReadFreeCompanyName(string freeCompanyTag)
        {
            if (string.IsNullOrEmpty(freeCompanyTag))
                return string.Empty;

            var infoModule = InfoModule.Instance();
            var freeCompany = infoModule == null
                ? null
                : (InfoProxyFreeCompany*)infoModule->GetInfoProxyById(InfoProxyId.FreeCompany);

            return freeCompany == null
                ? string.Empty
                : freeCompany->NameString;
        }

        private (string FreeCompanyName, string FreeCompanyTag) ResolveWidgetFreeCompanyValues(
            string characterName,
            uint homeWorldId,
            uint currentWorldId,
            string currentFreeCompanyName,
            string currentFreeCompanyTag)
        {
            characterName = characterName.Trim();
            if (string.IsNullOrWhiteSpace(characterName) || homeWorldId == 0 || currentWorldId == 0)
                return (currentFreeCompanyName, currentFreeCompanyTag);

            if (currentWorldId != homeWorldId)
            {
                var cached = FindWidgetFreeCompanyCache(characterName, homeWorldId);
                return cached == null
                    ? (currentFreeCompanyName, currentFreeCompanyTag)
                    : (cached.FreeCompanyName, cached.FreeCompanyTag);
            }

            UpdateWidgetFreeCompanyCache(
                characterName,
                homeWorldId,
                currentFreeCompanyName,
                currentFreeCompanyTag);

            return (currentFreeCompanyName, currentFreeCompanyTag);
        }

        private WidgetFreeCompanyCacheEntry? FindWidgetFreeCompanyCache(string characterName, uint homeWorldId)
        {
            Configuration.WidgetFreeCompanyCache ??= new List<WidgetFreeCompanyCacheEntry>();

            foreach (var entry in Configuration.WidgetFreeCompanyCache)
            {
                if (entry.HomeWorldId == homeWorldId
                    && string.Equals(entry.CharacterName, characterName, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        private void UpdateWidgetFreeCompanyCache(
            string characterName,
            uint homeWorldId,
            string freeCompanyName,
            string freeCompanyTag)
        {
            if (!CanCacheWidgetFreeCompany(freeCompanyName, freeCompanyTag))
                return;

            var entry = FindWidgetFreeCompanyCache(characterName, homeWorldId);
            if (entry == null)
            {
                Configuration.WidgetFreeCompanyCache.Add(new WidgetFreeCompanyCacheEntry
                {
                    CharacterName = characterName,
                    HomeWorldId = homeWorldId,
                    FreeCompanyName = freeCompanyName,
                    FreeCompanyTag = freeCompanyTag,
                });
                Configuration.Save();
                return;
            }

            if (entry.FreeCompanyName == freeCompanyName && entry.FreeCompanyTag == freeCompanyTag)
                return;

            entry.FreeCompanyName = freeCompanyName;
            entry.FreeCompanyTag = freeCompanyTag;
            Configuration.Save();
        }

        private static bool CanCacheWidgetFreeCompany(string freeCompanyName, string freeCompanyTag)
            => string.IsNullOrEmpty(freeCompanyTag) || !string.IsNullOrEmpty(freeCompanyName);

        private string GetCurrentLocationName()
        {
            if (!ClientState.IsLoggedIn)
                return string.Empty;

            territories ??= DataManager.GetExcelSheet<TerritoryType>();
            var territory = territories.GetRow(ClientState.TerritoryType);
            var territoryName = territory.PlaceName.Value.Name.ToString() ?? "Unknown Location";

            switch (ClientState.TerritoryType)
            {
                case 1250: //Minimalist Private House 
                    return "Private House - Minimalist";
                case 1251: //Minimalist Private Mansion 
                    return "Private Mansion - Minimalist";
                case 1375: //Minimalist Private House Dark 
                    return "Private House - Minimalist";
                case 1376: //Minimalist Private Mansion Dark 
                    return "Private Mansion - Minimalist";
                default:
                    return string.IsNullOrEmpty(territoryName)
                        ? "Unknown Location"
                        : territoryName;
            }
        }

        private string GetWidgetPartySize()
        {
            if (!ClientState.IsLoggedIn)
                return string.Empty;

            return Math.Max(1, GetPartySize()).ToString();
        }

        private string GetTripleTriadProgress()
        {
            if (!ClientState.IsLoggedIn)
                return string.Empty;

            tripleTriadCards ??= DataManager.GetExcelSheet<TripleTriadCard>();

            if (tripleTriadCardTotal < 0)
            {
                tripleTriadCardTotal = 0;
                foreach (var card in tripleTriadCards)
                {
                    if (card.RowId != 0)
                        tripleTriadCardTotal++;
                }
            }

            var collected = 0;
            foreach (var card in tripleTriadCards)
            {
                if (card.RowId != 0 && UnlockState.IsTripleTriadCardUnlocked(card))
                    collected++;
            }

            return $"{collected}/{tripleTriadCardTotal}";
        }

        private string GetMountsCollected()
        {
            if (!ClientState.IsLoggedIn)
                return string.Empty;

            mounts ??= DataManager.GetExcelSheet<Mount>();

            if (mountTotal < 0)
            {
                mountTotal = 0;
                foreach (var mount in mounts)
                {
                    if (mount.RowId != 0)
                        mountTotal++;
                }
            }

            var collected = 0;
            foreach (var mount in mounts)
            {
                if (mount.RowId != 0 && UnlockState.IsMountUnlocked(mount))
                    collected++;
            }

            return $"{collected}/{mountTotal}";
        }

        private string GetMinionsCollected()
        {
            if (!ClientState.IsLoggedIn)
                return string.Empty;

            companions ??= DataManager.GetExcelSheet<Companion>();

            if (companionTotal < 0)
            {
                companionTotal = 0;
                foreach (var companion in companions)
                {
                    if (companion.RowId != 0)
                        companionTotal++;
                }
            }

            var collected = 0;
            foreach (var companion in companions)
            {
                if (companion.RowId != 0 && UnlockState.IsCompanionUnlocked(companion))
                    collected++;
            }

            return $"{collected}/{companionTotal}";
        }

        internal void UpdateRichPresence()
        {
            if (discordService == null || !discordService.IsInitialized)
                InitializeDiscord();

            if (!ClientState.IsLoggedIn || discordService == null || !discordService.IsInitialized)
                return;

            var character = ObjectTable.LocalPlayer;
            if (character == null)
                return;

            var territoryName = GetCurrentLocationName();
            Log.Information("{TerritoryIsd}", ClientState.TerritoryType);
            
            var partySize = GetPartySize();
            var maxParty = 4;

            if (partySize > 4) maxParty = 8;
            if (partySize > 8) maxParty = 24;

            var partyString = partySize > 1 ? $" ({partySize} of {maxParty})" : string.Empty;
            var statusContext = new StatusTextPlaceholderContext(
                character.Name.TextValue,
                partyString,
                territoryName);

            var zoneMatch = FindZoneMatch(territoryName);

            string imageKey;
            if (zoneMatch != null && !string.IsNullOrEmpty(zoneMatch.ImageUrl))
            {
                imageKey = zoneMatch.ImageUrl;
            }
            else if (!string.IsNullOrEmpty(Configuration.ImageUrl))
            {
                imageKey = Configuration.ImageUrl;
            }
            else
            {
                imageKey = "default";
            }

            var presence = new RichPresence
            {
                Details = statusTextPlaceholderResolver.Resolve(
                    Configuration.StatusDetails ?? Configuration.DefaultStatusDetails,
                    statusContext),
                State = statusTextPlaceholderResolver.Resolve(
                    Configuration.StatusState ?? Configuration.DefaultStatusState,
                    statusContext),
                Assets = new Assets
                {
                    LargeImageKey = imageKey,
                    LargeImageText = "Final Fantasy XIV"
                },
                Timestamps = new Timestamps { Start = startTime },
            };

            var buttons = new List<Button>();
            if (Configuration.Enabled)
                buttons.Add(new Button { Label = Configuration.Text, Url = Configuration.Link });
            if (Configuration.Enabled2)
                buttons.Add(new Button { Label = Configuration.Text2, Url = Configuration.Link2 });
            presence.Buttons = buttons.ToArray();

            discordService.SetPresence(presence);
        }

        private unsafe int GetPartySize()
        {
            var partyManager = GroupManager.Instance();
            return partyManager == null ? 0 : partyManager->MainGroup.MemberCount;
        }

        private unsafe string GetPartyStateSignature()
        {
            var partyManager = GroupManager.Instance();
            if (partyManager == null)
                return string.Empty;

            var memberCount = partyManager->MainGroup.MemberCount;
            var parts = new List<string>(memberCount);
            var isAlliance = partyManager->GetGroup()->IsAlliance;

            if (isAlliance)
            {
                var allianceMembers = partyManager->GetGroup()->AllianceMembers;
                for (var i = 0; i < allianceMembers.Length; i++)
                {
                    var member = allianceMembers[i];
                    if (member.Name.IsEmpty) continue;

                    parts.Add($"{member.ContentId}:{member.TerritoryType}");
                }
            }
            else
            {
                for (var i = 0; i < memberCount; i++)
                {
                    var member = partyManager->MainGroup.GetPartyMemberByIndex(i);
                    if (member == null)
                        continue;

                    parts.Add($"{member->ContentId}:{member->TerritoryType}");
                }
            }

            return string.Join("|", parts);
        }

        private ZoneImage? FindZoneMatch(object territoryNameObj)
        {
            var territoryName = territoryNameObj?.ToString() ?? string.Empty;

            foreach (var z in Configuration.ZoneImages)
            {
                if (z.Enabled && string.Equals(z.Area, territoryName, StringComparison.OrdinalIgnoreCase))
                    return z;
            }

            return null;
        }
    }

    internal readonly record struct WidgetAutomaticUpdateDebugInfo(
        TimeSpan Interval,
        DateTime NextUpdateUtc,
        TimeSpan Remaining,
        bool IsUpdateRunning,
        bool IsLoggedIn,
        bool IsWidgetConfigured,
        bool IsCurrentCharacterAllowed);
}
