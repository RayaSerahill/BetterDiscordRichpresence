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
        private int tripleTriadCardTotal = -1;

        private DateTime nextPartyCheckTime = DateTime.MinValue;
        private int lastPartySize = -1;
        private string lastPartyState = string.Empty;
        private bool pendingLoginWidgetUpdate;
        private DateTime loginWidgetUpdateTime;
        private DateTime loginWidgetUpdateDeadline;

        public Plugin()
        {
            ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
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
                    new WidgetPlaceholderContext(string.Empty, string.Empty, string.Empty));
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
                _ = SendLoginWidgetUpdateAsync(context);
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

        private unsafe WidgetPlaceholderContext GetWidgetPlaceholderContext()
        {
            var freeCompanyTag = ObjectTable.LocalPlayer?.CompanyTag.TextValue ?? string.Empty;
            var freeCompanyName = string.Empty;

            if (!string.IsNullOrEmpty(freeCompanyTag))
            {
                var infoModule = InfoModule.Instance();
                var freeCompany = infoModule == null
                    ? null
                    : (InfoProxyFreeCompany*)infoModule->GetInfoProxyById(InfoProxyId.FreeCompany);

                if (freeCompany != null)
                    freeCompanyName = freeCompany->NameString;
            }

            return new WidgetPlaceholderContext(
                freeCompanyName,
                freeCompanyTag,
                GetTripleTriadProgress());
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

        internal void UpdateRichPresence()
        {
            if (discordService == null || !discordService.IsInitialized)
                InitializeDiscord();

            if (!ClientState.IsLoggedIn || discordService == null || !discordService.IsInitialized)
                return;

            var character = ObjectTable.LocalPlayer;
            if (character == null)
                return;

            territories ??= DataManager.GetExcelSheet<TerritoryType>();
            var territory = territories.GetRow(ClientState.TerritoryType);
            var territoryName = territory.PlaceName.Value.Name.ToString() ?? "Unknown Location";
            Log.Information("{TerritoryIsd}", ClientState.TerritoryType);
            switch (ClientState.TerritoryType)
            {
                case 1250: //Minimalist Private House 
                    territoryName = "Private House - Minimalist";
                    break;
                case 1251: //Minimalist Private Mansion 
                    territoryName = "Private Mansion - Minimalist";
                    break;
                case 1375: //Minimalist Private House Dark 
                    territoryName = "Private House - Minimalist";
                    break;
                case 1376: //Minimalist Private Mansion Dark 
                    territoryName = "Private Mansion - Minimalist";
                    break;
            }
            
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
                    LargeImageText = territoryName
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
}
