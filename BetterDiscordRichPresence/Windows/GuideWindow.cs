using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;

namespace BetterDiscordRichPresence.Windows
{
    internal sealed class GuideWindow : Window, IDisposable
    {
        private const string DeveloperPortalUrl = "https://discord.com/developers/applications";
        private const string InstallerScriptUrl =
            "https://gist.githubusercontent.com/RayaSerahill/115b56f8551f5f64bbc748d7437de06f/raw/32ed2629eba718470cc5424b4a177b3250e7b4ef/FFXIV_Widget_Creator.js";
        private const string DiscordInviteUrl = "https://discord.gg/aSHVCS97HV";

        public GuideWindow()
            : base("Guide###BDRP_Guide")
        {
            Size = new Vector2(560, 420);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Open() => IsOpen = true;

        public void Dispose() { }

        public override void Draw()
        {
            ImGui.SetWindowFontScale(1.55f);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.25f, 0.18f, 1f));
            ImGui.TextWrapped("READ CAREFULLY!!");
            ImGui.PopStyleColor();
            ImGui.SetWindowFontScale(1f);

            ImGui.Spacing();
            ImGui.TextWrapped(
                "This setup touches Discord application details, bot permissions, and experimental discord API endpoints. All of this is subject to breaking at any moment if discord wants to mess with it. " +
                "Go slowly, double check every copied value, and keep tokens private. That said the guide requires you to execute a script I wrote, skipping the most technical steps of setting this up, but requiring some trust.");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawEasyGuide();
        }

        private static void DrawEasyGuide()
        {
            ImGui.TextWrapped(
                "The guide uses the installer script to create and publish the Discord widget layout for you. " +
                "You need to run one browser-console command, so read each step carefully. As for the script " +
                "on step 6, that requires some trust, though I do recommend you have a friend who can read code or " +
                "your favourite AI agent to check it. I promise it is not malicious but I encourage being safe regardless");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextWrapped(
                "1. Fill in every widget content field in this plugin first. The text does not need to be final; " +
                "temporary values are fine. The update button needs complete data later.");

            ImGui.Spacing();
            ImGui.TextWrapped(
                "2. Open the Discord Developer Portal and create a new application. If Discord asks why you are there, " +
                "Social SDK is a reasonable choice, but the exact answer is not important.");

            ImGui.Spacing();
            if (ImGui.Button("Copy developer portal URL"))
                ImGui.SetClipboardText(DeveloperPortalUrl);

            ImGui.Spacing();
            ImGui.TextWrapped(
                "3. Name the application carefully. The application name appears at the top of the widget. " +
                "Add an application icon too if you want one shown there.");

            ImGui.Spacing();
            ImGui.TextWrapped(
                "4. Head over to Games -> Social SDK section of the left navigation. You will see a form there, " +
                "you can invent details for here, except definitely use an email you actively check. Everything else is up to your imagination");

            ImGui.Spacing();
            ImGui.TextWrapped(
                "5. With your new application open in the Developer Portal, open your browser developer tools with F12 " +
                "and switch to the Console tab.");

            ImGui.Spacing();
            ImGui.TextWrapped(
                "6. Copy the installer script, paste it into the browser console, and press Enter. Only run it on " +
                "an application you own. The script uploads the embedded widget asset, creates or reuses a widget config, " +
                "publishes the layout, authorizes Social Layer Presence, and adds the widget to your Discord profile.");

            ImGui.Spacing();
            if (ImGui.Button("Copy script URL"))
                ImGui.SetClipboardText(InstallerScriptUrl);

            ImGui.Spacing();
            ImGui.TextWrapped(
                "7. Wait until the console says it is done. If it fails, read the error before retrying; Discord may " +
                "have changed something or the portal may not have fully loaded.");

            ImGui.Spacing();
            ImGui.TextWrapped(
                "8. Return to the plugin and fill in the bot information. Application ID is from Overview -> General Information. Bot token is from Overview -> Bot. Your user ID you can get by right clicking your own avatar with discord developer mode on.");

            ImGui.Spacing();
            ImGui.TextWrapped(
                "9. Once all the details are filled press Update widget button. If the widget does not show up right away, reload " +
                "Discord or restart it.");
            
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextWrapped(
                "If you have issues feel free to ask for help in either puni.sh discord or my own at ");
            if (ImGui.Button("Discord invite"))
                Util.OpenLink(DiscordInviteUrl);
        }

    }
}
