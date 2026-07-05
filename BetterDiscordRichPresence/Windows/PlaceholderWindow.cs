using System;
using System.Collections.Generic;
using System.Numerics;
using BetterDiscordRichPresence.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace BetterDiscordRichPresence.Windows
{
    internal sealed class PlaceholderWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private IReadOnlyList<WidgetPlaceholderValue> values = Array.Empty<WidgetPlaceholderValue>();
        private DateTime nextRefreshTime = DateTime.MinValue;
        private string filter = string.Empty;

        public PlaceholderWindow(Plugin plugin)
            : base("Widget Placeholders###BDRP_Widget_Placeholders")
        {
            this.plugin = plugin;
            Size = new Vector2(480, 320);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Open()
        {
            nextRefreshTime = DateTime.MinValue;
            IsOpen = true;
        }

        public void Dispose() { }

        public override void Draw()
        {
            if (DateTime.UtcNow >= nextRefreshTime)
            {
                values = plugin.GetWidgetPlaceholderValues();
                nextRefreshTime = DateTime.UtcNow.AddSeconds(1);
            }

            ImGui.Text("Filter");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##bd_widget_placeholder_filter", ref filter, 256);

            if (!ImGui.BeginTable(
                    "bd_widget_placeholder_values",
                    2,
                    ImGuiTableFlags.SizingStretchProp
                    | ImGuiTableFlags.RowBg
                    | ImGuiTableFlags.BordersInnerH))
            {
                return;
            }

            ImGui.TableSetupColumn("Placeholder", ImGuiTableColumnFlags.WidthFixed, 150f);
            ImGui.TableSetupColumn("Current value");
            ImGui.TableHeadersRow();

            foreach (var placeholder in values)
            {
                if (!MatchesFilter(placeholder))
                    continue;

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(placeholder.Token);

                ImGui.TableSetColumnIndex(1);
                ImGui.TextWrapped(string.IsNullOrEmpty(placeholder.Value)
                    ? "(not available)"
                    : placeholder.Value);
            }

            ImGui.EndTable();
        }

        private bool MatchesFilter(WidgetPlaceholderValue placeholder)
            => string.IsNullOrWhiteSpace(filter)
               || placeholder.Token.Contains(filter, StringComparison.OrdinalIgnoreCase)
               || placeholder.Value.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
