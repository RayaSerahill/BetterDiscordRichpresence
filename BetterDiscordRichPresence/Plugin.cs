using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using DiscordRPC;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
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
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] private static IPartyList PartyList { get; set; } = null!;

        private const string CommandName = "/drp";

        public Configuration Configuration { get; }
        private readonly WindowSystem windowSystem = new("BetterDiscordRichPresence");
        private readonly ConfigWindow configWindow;
        private DiscordRpcClient? discordClient;
        private DateTime startTime;
        private bool pendingTerritoryUpdate;
        private DateTime territoryUpdateTime;
        private ExcelSheet<TerritoryType>? territories;

        private DateTime nextPartyCheckTime = DateTime.MinValue;
        private int lastPartySize = -1;
        private string lastPartyState = string.Empty;

        public Plugin()
        {
            ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configWindow = new ConfigWindow(this);
            windowSystem.AddWindow(configWindow);

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
            windowSystem.RemoveAllWindows();
            configWindow.Dispose();
            CommandManager.RemoveHandler(CommandName);

            if (discordClient != null)
            {
                discordClient.ClearPresence();
                discordClient.Dispose();
            }

            ClientState.TerritoryChanged -= OnTerritoryChanged;
            ClientState.Login -= OnLogin;
            ClientState.Logout -= OnLogout;
            Framework.Update -= OnFrameworkUpdate;
        }

        private void InitializeDiscord()
        {
            discordClient = new DiscordRpcClient(Configuration.DiscordApp);
            discordClient.Initialize();
            startTime = DateTime.UtcNow;
        }

        private void OnLogin()
        {
            startTime = DateTime.UtcNow;
            lastPartySize = -1;
            lastPartyState = string.Empty;
            UpdateRichPresence();
        }

        private void OnTerritoryChanged(ushort _)
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

        private void OnCommand(string command, string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                ToggleConfigUI();
            }
            else if (args.Trim().Equals("refresh", StringComparison.OrdinalIgnoreCase))
            {
                UpdateRichPresence();
            }
        }

        private void OnLogout(int type, int code)
        {
            lastPartySize = -1;
            lastPartyState = string.Empty;

            if (discordClient?.IsInitialized == true)
                discordClient.ClearPresence();
        }

        internal void UpdateRichPresence()
        {
            if (discordClient == null || !discordClient.IsInitialized)
                InitializeDiscord();

            if (!ClientState.IsLoggedIn || discordClient == null || !discordClient.IsInitialized)
                return;

            var character = ClientState.LocalPlayer;
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
            }
            
            var partySize = GetPartySize();
            var maxParty = 4;

            if (partySize > 4) maxParty = 8;
            if (partySize > 8) maxParty = 24;

            var partyString = partySize > 1 ? $" ({partySize} of {maxParty})" : string.Empty;

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
                Details = $"{character.Name} {partyString}",
                State = $"in {territoryName}",
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

            discordClient.SetPresence(presence);
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