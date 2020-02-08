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
        public static readonly string Version = "1.0.2a";

        public override void OnEnable()
        {
            SanyaPluginConfig.Reload();
            assembly = Assembly.GetAssembly(typeof(ServerConsole));

            try
            {
                EventHandlers = new EventHandlers(this);
                Events.RemoteAdminCommandEvent += EventHandlers.OnCommand;
                Events.ConsoleCommandEvent += EventHandlers.OnConsoleCommand;
                Events.WaitingForPlayersEvent += EventHandlers.OnWaintingForPlayers;
                Events.RoundStartEvent += EventHandlers.OnRoundStart;
                Events.RoundEndEvent += EventHandlers.OnRoundEnd;
                Events.RoundRestartEvent += EventHandlers.OnRoundRestart;
                Events.PlayerJoinEvent += EventHandlers.OnPlayerJoin;
                Events.PlayerLeaveEvent += EventHandlers.OnPlayerLeave;
                Events.SetClassEvent += EventHandlers.OnPlayerSetClass;
                Events.PlayerHurtEvent += EventHandlers.OnPlayerHurt;
                Events.PlayerDeathEvent += EventHandlers.OnPlayerDeath;
                Events.PocketDimDeathEvent += EventHandlers.OnPocketDimDeath;
                Events.TriggerTeslaEvent += EventHandlers.OnPlayerTriggerTesla;
                Events.GeneratorUnlockEvent += EventHandlers.OnGeneratorUnlock;
                Events.GeneratorOpenedEvent += EventHandlers.OnGeneratorOpen;
                Events.GeneratorClosedEvent += EventHandlers.OnGeneratorClose;
                Events.GeneratorFinishedEvent += EventHandlers.OnGeneratorFinish;
                Events.Scp914UpgradeEvent += EventHandlers.On914Upgrade;
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
            Timing.KillCoroutines(EventHandlers.infosenderhandle);
            Timing.KillCoroutines(EventHandlers.everySecondhandle);
            Timing.KillCoroutines(EventHandlers.fixedUpdatehandle);

            Events.RemoteAdminCommandEvent -= EventHandlers.OnCommand;
            Events.ConsoleCommandEvent -= EventHandlers.OnConsoleCommand;
            Events.WaitingForPlayersEvent -= EventHandlers.OnWaintingForPlayers;
            Events.RoundStartEvent -= EventHandlers.OnRoundStart;
            Events.RoundEndEvent -= EventHandlers.OnRoundEnd;
            Events.RoundRestartEvent -= EventHandlers.OnRoundRestart;
            Events.PlayerJoinEvent -= EventHandlers.OnPlayerJoin;
            Events.PlayerLeaveEvent -= EventHandlers.OnPlayerLeave;
            Events.SetClassEvent -= EventHandlers.OnPlayerSetClass;
            Events.PlayerHurtEvent -= EventHandlers.OnPlayerHurt;
            Events.PlayerDeathEvent -= EventHandlers.OnPlayerDeath;
            Events.TriggerTeslaEvent -= EventHandlers.OnPlayerTriggerTesla;
            Events.GeneratorUnlockEvent -= EventHandlers.OnGeneratorUnlock;
            Events.GeneratorOpenedEvent -= EventHandlers.OnGeneratorOpen;
            Events.GeneratorClosedEvent -= EventHandlers.OnGeneratorClose;
            Events.GeneratorFinishedEvent -= EventHandlers.OnGeneratorFinish;
            Events.Scp914UpgradeEvent -= EventHandlers.On914Upgrade;
            EventHandlers = null;

            Plugin.Info($"[OnDisable] SanyaPlugin({Version}) Disabled.");
        }

        public override void OnReload()
        {
            Plugin.Info($"[OnReload] SanyaPlugin({Version}) Reloaded.");
        }
    }
}
