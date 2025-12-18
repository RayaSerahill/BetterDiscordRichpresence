using System;
using ECommons.EzIpcManager;

namespace BetterDiscordRichPresence;

public class SimpleBlackjackIpc
{
    private readonly Plugin plugin;
    public SimpleBlackjackIpc(Plugin plugin, Func<bool> isLoggedIn)
    {
        this.plugin = plugin;
        IsLoggedIn = isLoggedIn;
        EzIPC.Init(this, "SimpleBlackjack", SafeWrapper.AnyException);
    }
    
    [EzIPC]
    public Func<bool> IsLoggedIn;
    
    [EzIPCEvent]
    public void OnGameFinished()
    {
        if (!plugin.IsHosting)
        {
            plugin.IsHosting = true;
            plugin.UpdateRichPresence();
        }
    }
}
