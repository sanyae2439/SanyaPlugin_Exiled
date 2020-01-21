using System.Reflection;
using EXILED;
using Harmony;
using MEC;

namespace SanyaPlugin
{
    public class SanyaPlugin : Plugin
    {
        public EventHandlers EventHandlers;
        public HarmonyInstance harmony;
        public Assembly assembly;
        public override string getName { get; } = "SanyaPlugin";
        public static readonly string harmonyId = "com.sanyae2439.SanyaPlugin";
        public static readonly string Version = "1.0.0a";

        public override void OnEnable()
        {
            SanyaPluginConfig.Reload();
            assembly = Assembly.GetAssembly(typeof(ServerConsole));

            try
            {
                EventHandlers = new EventHandlers(this);
                Events.WaitingForPlayersEvent += EventHandlers.OnWaintingForPlayers;
                Events.PlayerJoinEvent += EventHandlers.OnPlayerJoin;
                Events.PlayerLeaveEvent += EventHandlers.OnPlayerLeave;
                Events.SetClassEvent += EventHandlers.OnPlayerSetClass;
                Events.PlayerHurtEvent += EventHandlers.OnPlayerHurt;
                Events.TriggerTeslaEvent += EventHandlers.OnPlayerTriggerTesla;
            }
            catch(System.Exception e)
            {
                Error($"[OnEnable] Add Event Error:{e}");
            }

            harmony = HarmonyInstance.Create(harmonyId);
            harmony.PatchAll();

            

            Plugin.Info($"[OnEnabled] SanyaPlugin({Version}) Enabled.");
        }

        public override void OnDisable()
        {
            harmony.UnpatchAll();
            Timing.KillCoroutines("SanyaPlugin_Sender");

            Events.WaitingForPlayersEvent -= EventHandlers.OnWaintingForPlayers;
            Events.PlayerJoinEvent -= EventHandlers.OnPlayerJoin;
            Events.PlayerLeaveEvent -= EventHandlers.OnPlayerLeave;
            Events.SetClassEvent -= EventHandlers.OnPlayerSetClass;
            Events.PlayerHurtEvent -= EventHandlers.OnPlayerHurt;
            Events.TriggerTeslaEvent -= EventHandlers.OnPlayerTriggerTesla;
            EventHandlers = null;

            Plugin.Info($"[OnDisable] SanyaPlugin({Version}) Disabled.");
        }

        public override void OnReload()
        {

        }
    }
}
