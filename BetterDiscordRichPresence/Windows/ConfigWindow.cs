using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BetterDiscordRichPresence.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Configuration configuration;

        public ConfigWindow(Plugin plugin)
            : base("BetterDiscordRichPresence Settings###BDRP_Config")
        {
            Flags = ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse |
                    ImGuiWindowFlags.NoCollapse;
            Size = new Vector2(320, 140);
            SizeCondition = ImGuiCond.FirstUseEver;

            configuration = plugin.Configuration;
        }

        public void Dispose() { }

        public override void Draw()
        {
            if (ImGui.BeginTabBar("SettingsTabs"))
            {
                if (ImGui.BeginTabItem("General"))
                {
                    DrawGeneralSettings();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Buttons"))
                {
                    DrawButtonSettings();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Images"))
                {
                    DrawImageSettings();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private void DrawButtonSettings()
        {
            if (!ImGui.BeginTable("bd_config_table", 4, ImGuiTableFlags.SizingStretchProp))
                return;

            ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, 90f);
            ImGui.TableSetupColumn("Value1");
            ImGui.TableSetupColumn("Value2");
            ImGui.TableSetupColumn("Value3");

            // Button 1
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Button 1");

            ImGui.TableSetColumnIndex(1);
            var isEnabled1 = configuration.Enabled;
            if (ImGui.Checkbox("##bd_enabled1", ref isEnabled1))
                UpdateConfig(() => configuration.Enabled = isEnabled1);
            ImGui.SameLine(); ImGui.TextDisabled("Enabled");

            ImGui.TableSetColumnIndex(2);
            var text1 = configuration.Text ?? string.Empty;
            if (ImGui.InputText("##bd_text1", ref text1, 512))
                UpdateConfig(() => configuration.Text = text1);
            ImGui.SameLine(); ImGui.TextDisabled("Text");

            ImGui.TableSetColumnIndex(3);
            var link1 = configuration.Link ?? string.Empty;
            if (ImGui.InputText("##bd_link1", ref link1, 512))
                UpdateConfig(() => configuration.Link = link1);
            ImGui.SameLine(); ImGui.TextDisabled("Link");

            // Button 2
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text("Button 2");

            ImGui.TableSetColumnIndex(1);
            var isEnabled2 = configuration.Enabled2;
            if (ImGui.Checkbox("##bd_enabled2", ref isEnabled2))
                UpdateConfig(() => configuration.Enabled2 = isEnabled2);
            ImGui.SameLine(); ImGui.TextDisabled("Enabled");

            ImGui.TableSetColumnIndex(2);
            var text2 = configuration.Text2 ?? string.Empty;
            if (ImGui.InputText("##bd_text2", ref text2, 512))
                UpdateConfig(() => configuration.Text2 = text2);
            ImGui.SameLine(); ImGui.TextDisabled("Text");

            ImGui.TableSetColumnIndex(3);
            var link2 = configuration.Link2 ?? string.Empty;
            if (ImGui.InputText("##bd_link2", ref link2, 512))
                UpdateConfig(() => configuration.Link2 = link2);
            ImGui.SameLine(); ImGui.TextDisabled("Link");

            ImGui.EndTable();
        }

        private void DrawImageSettings()
        {
            ImGui.Text("Default Image URL");
            ImGui.SameLine();
            var imageUrl = configuration.ImageUrl ?? string.Empty;
            if (ImGui.InputText("##bd_image_url", ref imageUrl, 512))
                UpdateConfig(() => configuration.ImageUrl = imageUrl);

            ImGui.Separator();
            ImGui.Text("Zone Specific Images");
            ImGui.Separator();

            if (ImGui.Button("Add Zone"))
                UpdateConfig(() => configuration.ZoneImages.Add(new ZoneImage()));

            if (ImGui.BeginTable("bd_zone_images_table", 4, ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("Enabled", ImGuiTableColumnFlags.WidthFixed, 80f);
                ImGui.TableSetupColumn("Area", ImGuiTableColumnFlags.WidthFixed, 200f);      // Set uniform width
                ImGui.TableSetupColumn("Image URL", ImGuiTableColumnFlags.WidthFixed, 200f); // Set uniform width
                ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed, 90f);


                for (var i = 0; i < configuration.ZoneImages.Count; i++)
                {
                    var zone = configuration.ZoneImages[i];
                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    var zoneEnabled = zone.Enabled;
                    if (ImGui.Checkbox($"##zone_enabled_{i}", ref zoneEnabled))
                        UpdateConfig(() => configuration.ZoneImages[i].Enabled = zoneEnabled);
                    ImGui.SameLine(); ImGui.TextDisabled("Enabled");

                    ImGui.TableSetColumnIndex(1);
                    var area = zone.Area ?? string.Empty;
                    if (ImGui.InputText($"##zone_area_{i}", ref area, 100))
                        UpdateConfig(() => configuration.ZoneImages[i].Area = area);
                    ImGui.SameLine(); ImGui.TextDisabled("Zone");

                    ImGui.TableSetColumnIndex(2);
                    var url = zone.ImageUrl ?? string.Empty;
                    if (ImGui.InputText($"##zone_url_{i}", ref url, 100))
                        UpdateConfig(() => configuration.ZoneImages[i].ImageUrl = url);
                    ImGui.SameLine(); ImGui.TextDisabled("Image URL");

                    ImGui.TableSetColumnIndex(3);
                    if (ImGui.Button($"Remove##zone_remove_{i}"))
                    {
                        UpdateConfig(() => configuration.ZoneImages.RemoveAt(i));
                        break;
                    }
                }

                ImGui.EndTable();
            }
        }

        private void DrawGeneralSettings()
        {
            ImGui.Text("Discord Configuration");
            ImGui.SameLine();
            var discordApp = configuration.DiscordApp ?? string.Empty;
            if (ImGui.InputText("##bd_discord_app", ref discordApp, 512))
                UpdateConfig(() => configuration.DiscordApp = discordApp);
        }

        private void UpdateConfig(Action applyChanges)
        {
            applyChanges();
            configuration.Save();
        }
    }
}
