using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BetterDiscordRichPresence.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BetterDiscordRichPresence.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Configuration configuration;
        private readonly WidgetService widgetService = new();
        private readonly CancellationTokenSource disposeTokenSource = new();
        private Task<WidgetUpdateResult>? widgetUpdateTask;
        private string widgetUpdateStatus = string.Empty;
        private bool? widgetUpdateSucceeded;

        public ConfigWindow(Plugin plugin)
            : base("BetterDiscordRichPresence Settings###BDRP_Config")
        {
            Flags = ImGuiWindowFlags.NoCollapse;
            Size = new Vector2(700, 650);
            SizeCondition = ImGuiCond.FirstUseEver;

            configuration = plugin.Configuration;
        }

        public void Dispose()
        {
            disposeTokenSource.Cancel();
            disposeTokenSource.Dispose();
            widgetService.Dispose();
        }

        public override void Draw()
        {
            PollWidgetUpdate();

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

                if (ImGui.BeginTabItem("Widget"))
                {
                    DrawWidgetSettings();
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
        }

        private void DrawWidgetSettings()
        {
            var allFieldsComplete = AreWidgetFieldsComplete();
            var canUpdate = allFieldsComplete && widgetUpdateTask == null;

            if (!canUpdate)
                ImGui.BeginDisabled();

            if (ImGui.Button("update widget"))
            {
                widgetUpdateStatus = "Updating widget...";
                widgetUpdateSucceeded = null;
                var request = WidgetUpdateRequest.FromConfiguration(configuration);
                widgetUpdateTask = widgetService.UpdateAsync(request, disposeTokenSource.Token);
            }

            if (!canUpdate)
                ImGui.EndDisabled();

            ImGui.SameLine();
            if (widgetUpdateTask != null)
                ImGui.TextDisabled("Sending request...");
            else if (!allFieldsComplete)
                ImGui.TextDisabled("Complete every field to enable the update.");

            if (!string.IsNullOrEmpty(widgetUpdateStatus))
            {
                var statusColor = widgetUpdateSucceeded switch
                {
                    true => new Vector4(0.3f, 0.85f, 0.4f, 1f),
                    false => new Vector4(0.95f, 0.35f, 0.3f, 1f),
                    _ => new Vector4(0.7f, 0.7f, 0.7f, 1f),
                };

                ImGui.PushStyleColor(ImGuiCol.Text, statusColor);
                ImGui.TextWrapped(widgetUpdateStatus);
                ImGui.PopStyleColor();
            }

            ImGui.Separator();
            ImGui.Text("Required Discord information");

            if (ImGui.BeginTable("bd_widget_credentials", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthFixed, 160f);
                ImGui.TableSetupColumn("Value");

                DrawWidgetField(
                    "Application ID",
                    "##bd_widget_application_id",
                    () => configuration.DiscordApp,
                    value => configuration.DiscordApp = value);
                DrawWidgetField(
                    "Bot Token",
                    "##bd_widget_bot_token",
                    () => configuration.WidgetBotToken,
                    value => configuration.WidgetBotToken = value,
                    ImGuiInputTextFlags.Password);
                DrawWidgetField(
                    "User ID",
                    "##bd_widget_user_id",
                    () => configuration.WidgetUserId,
                    value => configuration.WidgetUserId = value);

                ImGui.EndTable();
            }

            ImGui.Separator();
            ImGui.Text("Widget content");

            if (ImGui.BeginTable("bd_widget_content", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthFixed, 160f);
                ImGui.TableSetupColumn("Value");

                DrawWidgetField("Title", "##bd_widget_title", () => configuration.WidgetTitle, value => configuration.WidgetTitle = value);
                DrawWidgetField("Description", "##bd_widget_description", () => configuration.WidgetDescription, value => configuration.WidgetDescription = value);
                DrawWidgetField("Description 2", "##bd_widget_description_2", () => configuration.WidgetDescription2, value => configuration.WidgetDescription2 = value);
                DrawWidgetField("Description 3", "##bd_widget_description_3", () => configuration.WidgetDescription3, value => configuration.WidgetDescription3 = value);
                DrawWidgetField("Mini Profile Text", "##bd_widget_mini_profile", () => configuration.WidgetMiniProfileText, value => configuration.WidgetMiniProfileText = value);
                DrawWidgetField("Main Image URL", "##bd_widget_main_image", () => configuration.WidgetMainImageUrl, value => configuration.WidgetMainImageUrl = value);
                DrawWidgetField("Profile Icon URL", "##bd_widget_profile_icon", () => configuration.WidgetProfileIconUrl, value => configuration.WidgetProfileIconUrl = value);

                ImGui.EndTable();
            }

            ImGui.Separator();
            ImGui.Text("Stats");

            if (ImGui.BeginTable("bd_widget_stats", 2, ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthFixed, 160f);
                ImGui.TableSetupColumn("Value");

                DrawWidgetField("Stat 1 Value", "##bd_widget_stat_1_value", () => configuration.WidgetStat1Value, value => configuration.WidgetStat1Value = value);
                DrawWidgetField("Stat 1 Label", "##bd_widget_stat_1_label", () => configuration.WidgetStat1Label, value => configuration.WidgetStat1Label = value);
                DrawWidgetField("Stat 2 Value", "##bd_widget_stat_2_value", () => configuration.WidgetStat2Value, value => configuration.WidgetStat2Value = value);
                DrawWidgetField("Stat 2 Label", "##bd_widget_stat_2_label", () => configuration.WidgetStat2Label, value => configuration.WidgetStat2Label = value);
                DrawWidgetField("Stat 3 Value", "##bd_widget_stat_3_value", () => configuration.WidgetStat3Value, value => configuration.WidgetStat3Value = value);
                DrawWidgetField("Stat 3 Label", "##bd_widget_stat_3_label", () => configuration.WidgetStat3Label, value => configuration.WidgetStat3Label = value);
                DrawWidgetField("Stat 4 Value", "##bd_widget_stat_4_value", () => configuration.WidgetStat4Value, value => configuration.WidgetStat4Value = value);
                DrawWidgetField("Stat 4 Label", "##bd_widget_stat_4_label", () => configuration.WidgetStat4Label, value => configuration.WidgetStat4Label = value);
                DrawWidgetField("Stat 5 Value", "##bd_widget_stat_5_value", () => configuration.WidgetStat5Value, value => configuration.WidgetStat5Value = value);
                DrawWidgetField("Stat 5 Label", "##bd_widget_stat_5_label", () => configuration.WidgetStat5Label, value => configuration.WidgetStat5Label = value);
                DrawWidgetField("Stat 6 Value", "##bd_widget_stat_6_value", () => configuration.WidgetStat6Value, value => configuration.WidgetStat6Value = value);
                DrawWidgetField("Stat 6 Label", "##bd_widget_stat_6_label", () => configuration.WidgetStat6Label, value => configuration.WidgetStat6Label = value);

                ImGui.EndTable();
            }
        }

        private void DrawWidgetField(
            string label,
            string id,
            Func<string> getValue,
            Action<string> setValue,
            ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.Text(label);

            ImGui.TableSetColumnIndex(1);
            ImGui.SetNextItemWidth(-1);
            var value = getValue() ?? string.Empty;
            if (ImGui.InputText(id, ref value, 2048, flags))
                UpdateConfig(() => setValue(value));
        }

        private bool AreWidgetFieldsComplete()
            => HasValue(configuration.DiscordApp)
               && HasValue(configuration.WidgetBotToken)
               && HasValue(configuration.WidgetUserId)
               && HasValue(configuration.WidgetTitle)
               && HasValue(configuration.WidgetDescription)
               && HasValue(configuration.WidgetDescription2)
               && HasValue(configuration.WidgetDescription3)
               && HasValue(configuration.WidgetMiniProfileText)
               && HasValue(configuration.WidgetMainImageUrl)
               && HasValue(configuration.WidgetProfileIconUrl)
               && HasValue(configuration.WidgetStat1Value)
               && HasValue(configuration.WidgetStat1Label)
               && HasValue(configuration.WidgetStat2Value)
               && HasValue(configuration.WidgetStat2Label)
               && HasValue(configuration.WidgetStat3Value)
               && HasValue(configuration.WidgetStat3Label)
               && HasValue(configuration.WidgetStat4Value)
               && HasValue(configuration.WidgetStat4Label)
               && HasValue(configuration.WidgetStat5Value)
               && HasValue(configuration.WidgetStat5Label)
               && HasValue(configuration.WidgetStat6Value)
               && HasValue(configuration.WidgetStat6Label);

        private void PollWidgetUpdate()
        {
            if (widgetUpdateTask is not { IsCompleted: true })
                return;

            try
            {
                var result = widgetUpdateTask.GetAwaiter().GetResult();
                widgetUpdateSucceeded = result.Success;
                widgetUpdateStatus = result.Message;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to read the Discord widget update result.");
                widgetUpdateSucceeded = false;
                widgetUpdateStatus = $"Widget update failed: {ex.Message}";
            }
            finally
            {
                widgetUpdateTask = null;
            }
        }

        private static bool HasValue(string? value)
            => !string.IsNullOrWhiteSpace(value);

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
            ImGui.Text("Discord Application ID");
            ImGui.SameLine();
            var discordApp = configuration.DiscordApp ?? string.Empty;
            if (ImGui.InputText("##bd_discord_app", ref discordApp, 512))
                UpdateConfig(() => configuration.DiscordApp = discordApp);

            ImGui.Spacing();
            var rpcBridgeEnabled = configuration.RPCBridgeEnabled;
            if (ImGui.Checkbox("Enable Wine RPC Bridge on Linux/Wine", ref rpcBridgeEnabled))
                UpdateConfig(() => configuration.RPCBridgeEnabled = rpcBridgeEnabled);

            ImGui.TextDisabled("Needed when XIVLauncher runs through Wine and Discord runs natively on Linux.");
        }

        private void UpdateConfig(Action applyChanges)
        {
            applyChanges();
            configuration.Save();
        }
    }
}
