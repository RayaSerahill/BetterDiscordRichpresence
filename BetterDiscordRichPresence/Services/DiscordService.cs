using System;
using System.Diagnostics;
using System.IO;
using Dalamud.Utility;
using DiscordRPC;
using DiscordRPC.Logging;

namespace BetterDiscordRichPresence.Services
{
    internal sealed class DiscordService : IDisposable
    {
        private static DirectoryInfo WineRpcBridgePath => new(Path.Combine(Plugin.PluginInterface.AssemblyLocation.Directory!.FullName, "Resources/binaries", "WineRPCBridge.exe"));

        private readonly Plugin plugin;
        private DiscordRpcClient? rpcClient;
        private Process? rpcBridgeProcess;
        private bool bridgeStartAttempted;

        public bool IsInitialized => rpcClient?.IsInitialized == true;

        public DiscordService(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void Initialize()
        {
            CreateClient();

            if (Util.IsWine() && plugin.Configuration.RPCBridgeEnabled && !bridgeStartAttempted)
            {
                bridgeStartAttempted = true;
                StartWineRpcBridge();
            }
        }

        private void CreateClient()
        {
            if (rpcClient == null || rpcClient.IsDisposed)
            {
                rpcClient = new DiscordRpcClient(plugin.Configuration.DiscordApp)
                {
                    SkipIdenticalPresence = true,
                    Logger = new ConsoleLogger { Level = LogLevel.Warning },
                };
            }

            if (!rpcClient.IsInitialized)
                rpcClient.Initialize();
        }

        private void StartWineRpcBridge()
        {
            try
            {
                var bridgeProcessName = Path.GetFileNameWithoutExtension(WineRpcBridgePath.Name);
                var existingBridge = Process.GetProcessesByName(bridgeProcessName);
                if (existingBridge.Length > 0)
                {
                    Plugin.Log.Information($"Wine RPC Bridge already running (PID: {existingBridge[0].Id}).");
                    rpcBridgeProcess = existingBridge[0];
                    return;
                }

                if (!File.Exists(WineRpcBridgePath.FullName))
                {
                    Plugin.Log.Warning($"Wine RPC Bridge not found at {WineRpcBridgePath.FullName}.");
                    return;
                }

                rpcBridgeProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = WineRpcBridgePath.FullName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                if (rpcBridgeProcess != null)
                    Plugin.Log.Information($"Started Wine RPC Bridge (PID: {rpcBridgeProcess.Id}).");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to start Wine RPC Bridge.");
            }
        }

        public void SetPresence(RichPresence presence)
        {
            Initialize();
            rpcClient?.SetPresence(presence);
        }

        public void ClearPresence()
        {
            if (rpcClient == null)
                return;

            CreateClient();
            rpcClient.ClearPresence();
        }

        public void Dispose()
        {
            rpcBridgeProcess?.Dispose();
            rpcClient?.Dispose();
        }
    }
}
