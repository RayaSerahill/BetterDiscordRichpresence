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
        public string DiscordApp { get; set; } = string.Empty;

        // Collection of zone-specific image entries
        public List<ZoneImage> ZoneImages { get; set; } = new List<ZoneImage>();

        // Saves the current configuration to disk
        public void Save()
            => Plugin.PluginInterface.SavePluginConfig(this);
    }
}
