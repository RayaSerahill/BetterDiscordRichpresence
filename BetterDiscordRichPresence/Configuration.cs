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

        // Discord application ID for rich presence
        public string DiscordApp { get; set; } = "1398478033429598268";

        // Starts WineRPCBridge under Wine/Linux so native Discord can receive RPC updates
        public bool RPCBridgeEnabled { get; set; } = true;

        // Discord widget profile settings
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

        // Collection of zone-specific image entries
        public List<ZoneImage> ZoneImages { get; set; } = new List<ZoneImage>();

        // Saves the current configuration to disk
        public void Save()
            => Plugin.PluginInterface.SavePluginConfig(this);
    }
}
