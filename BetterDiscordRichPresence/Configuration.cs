using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace BetterDiscordRichPresence
{
    // Represents a single zone-specific image entry
    public class ZoneImage
    {
        public bool   Enabled  { get; set; } = true;
        public string Area     { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class WidgetFreeCompanyCacheEntry
    {
        public string CharacterName { get; set; } = string.Empty;
        public uint HomeWorldId { get; set; }
        public string FreeCompanyName { get; set; } = string.Empty;
        public string FreeCompanyTag { get; set; } = string.Empty;
    }

    // Plugin configuration storage
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        // Primary button settings
        public bool   Enabled  { get; set; } = true;
        public string Text     { get; set; } = string.Empty;
        public string Link     { get; set; } = string.Empty;

        // Secondary button settings
        public bool   Enabled2 { get; set; } = true;
        public string Text2    { get; set; } = string.Empty;
        public string Link2    { get; set; } = string.Empty;

        // Default image URL for rich presence
        public string ImageUrl    { get; set; } = string.Empty;

        // Rich presence status text templates
        public const string DefaultStatusDetails = "{CharacterName}{PartySize}";
        public const string DefaultStatusState = "in {Location}";
        public string StatusDetails { get; set; } = DefaultStatusDetails;
        public string StatusState   { get; set; } = DefaultStatusState;

        // Discord application ID for rich presence
        public string DiscordApp { get; set; } = "1398478033429598268";

        // Starts WineRPCBridge under Wine/Linux so native Discord can receive RPC updates
        public bool RPCBridgeEnabled { get; set; } = true;

        // Discord widget profile settings
        public string WidgetCharacterNameFilter { get; set; } = string.Empty;
        public string WidgetApplicationId     { get; set; } = string.Empty;
        public string WidgetBotToken          { get; set; } = string.Empty;
        public string WidgetUserId            { get; set; } = string.Empty;
        public string WidgetTitle             { get; set; } = string.Empty;
        public string WidgetDescription       { get; set; } = string.Empty;
        public string WidgetDescription2      { get; set; } = string.Empty;
        public string WidgetDescription3      { get; set; } = string.Empty;
        public string WidgetMiniProfileText   { get; set; } = string.Empty;
        public string WidgetMainImageUrl      { get; set; } = string.Empty;
        public string WidgetProfileIconUrl    { get; set; } = string.Empty;
        public string WidgetStat1Value        { get; set; } = string.Empty;
        public string WidgetStat1Label        { get; set; } = string.Empty;
        public string WidgetStat2Value        { get; set; } = string.Empty;
        public string WidgetStat2Label        { get; set; } = string.Empty;
        public string WidgetStat3Value        { get; set; } = string.Empty;
        public string WidgetStat3Label        { get; set; } = string.Empty;
        public string WidgetStat4Value        { get; set; } = string.Empty;
        public string WidgetStat4Label        { get; set; } = string.Empty;
        public string WidgetStat5Value        { get; set; } = string.Empty;
        public string WidgetStat5Label        { get; set; } = string.Empty;
        public string WidgetStat6Value        { get; set; } = string.Empty;
        public string WidgetStat6Label        { get; set; } = string.Empty;
        public List<WidgetFreeCompanyCacheEntry> WidgetFreeCompanyCache { get; set; } = new List<WidgetFreeCompanyCacheEntry>();

        // Collection of zone-specific image entries
        public List<ZoneImage> ZoneImages { get; set; } = new List<ZoneImage>();

        public bool IsWidgetConfigured()
            => HasValue(WidgetApplicationId)
               && HasValue(WidgetBotToken)
               && HasValue(WidgetUserId)
               && HasValue(WidgetTitle)
               && HasValue(WidgetDescription)
               && HasValue(WidgetDescription2)
               && HasValue(WidgetDescription3)
               && HasValue(WidgetMiniProfileText)
               && HasValue(WidgetMainImageUrl)
               && HasValue(WidgetProfileIconUrl)
               && HasValue(WidgetStat1Value)
               && HasValue(WidgetStat1Label)
               && HasValue(WidgetStat2Value)
               && HasValue(WidgetStat2Label)
               && HasValue(WidgetStat3Value)
               && HasValue(WidgetStat3Label)
               && HasValue(WidgetStat4Value)
               && HasValue(WidgetStat4Label)
               && HasValue(WidgetStat5Value)
               && HasValue(WidgetStat5Label)
               && HasValue(WidgetStat6Value)
               && HasValue(WidgetStat6Label);

        // Saves the current configuration to disk
        public void Save()
            => Plugin.PluginInterface.SavePluginConfig(this);

        private static bool HasValue(string? value)
            => !string.IsNullOrWhiteSpace(value);
    }
}
