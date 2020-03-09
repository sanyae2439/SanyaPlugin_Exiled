using System;
using System.IO;
using EXILED;
using Harmony;
using MEC;

namespace SanyaPlugin
{
    public class SanyaPlugin : Plugin
    {
        public override string getName { get; } = "SanyaPlugin";
        public static readonly string harmonyId = "com.sanyae2439.SanyaPlugin";
        public static readonly string Version = "1.2.6d";
        public static readonly string TargetVersion = "1.8.6";
        public static readonly string PlayersDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Plugins", "SanyaPlugin");

        public EventHandlers EventHandlers;
        public HarmonyInstance harmony;

        public override void OnEnable()
        {
            if(TargetVersion == $"{EventPlugin.Version.Major}.{EventPlugin.Version.Minor}.{EventPlugin.Version.Patch}")
            {
                Log.Info($"[OnEnabled] Version Match(SanyaPlugin:{TargetVersion} EXILED:{EventPlugin.Version.Major}.{EventPlugin.Version.Minor}.{EventPlugin.Version.Patch}). Loading Start...");
            }
            else
            {
                Log.Warn($"[OnEnabled] Version Mismatched(SanyaPlugin:{TargetVersion} EXILED:{EventPlugin.Version.Major}.{EventPlugin.Version.Minor}.{EventPlugin.Version.Patch}). May not work correctly.");
            }

            Configs.Reload();

            EventHandlers = new EventHandlers(this);
            Events.RemoteAdminCommandEvent += EventHandlers.OnCommand;
            Events.ConsoleCommandEvent += EventHandlers.OnConsoleCommand;
            Events.WaitingForPlayersEvent += EventHandlers.OnWaintingForPlayers;
            Events.RoundStartEvent += EventHandlers.OnRoundStart;
            Events.RoundEndEvent += EventHandlers.OnRoundEnd;
            Events.RoundRestartEvent += EventHandlers.OnRoundRestart;
            Events.WarheadDetonationEvent += EventHandlers.OnDetonated;
            Events.PlayerJoinEvent += EventHandlers.OnPlayerJoin;
            Events.PlayerLeaveEvent += EventHandlers.OnPlayerLeave;
            Events.StartItemsEvent += EventHandlers.OnStartItems;
            Events.SetClassEvent += EventHandlers.OnPlayerSetClass;
            Events.PlayerSpawnEvent += EventHandlers.OnPlayerSpawn;
            Events.PlayerHurtEvent += EventHandlers.OnPlayerHurt;
            Events.PlayerDeathEvent += EventHandlers.OnPlayerDeath;
            Events.PocketDimDeathEvent += EventHandlers.OnPocketDimDeath;
            Events.TriggerTeslaEvent += EventHandlers.OnPlayerTriggerTesla;
            Events.DoorInteractEvent += EventHandlers.OnPlayerDoorInteract;
            Events.LockerInteractEvent += EventHandlers.OnPlayerLockerInteract;
            Events.TeamRespawnEvent += EventHandlers.OnTeamRespawn;
            Events.GeneratorUnlockEvent += EventHandlers.OnGeneratorUnlock;
            Events.GeneratorOpenedEvent += EventHandlers.OnGeneratorOpen;
            Events.GeneratorClosedEvent += EventHandlers.OnGeneratorClose;
            Events.GeneratorInsertedEvent += EventHandlers.OnGeneratorInsert;
            Events.GeneratorFinishedEvent += EventHandlers.OnGeneratorFinish;
            Events.Scp914UpgradeEvent += EventHandlers.On914Upgrade;

            harmony = HarmonyInstance.Create(harmonyId);
            harmony.PatchAll();

            EventHandlers.sendertask = EventHandlers._SenderAsync().StartSender();

            Log.Info($"[OnEnabled] SanyaPlugin({Version}) Enabled.");
        }

        public override void OnDisable()
        {
            harmony.UnpatchAll();

            foreach(var cor in EventHandlers.roundCoroutines)
                Timing.KillCoroutines(cor);
            EventHandlers.roundCoroutines.Clear();

            Events.RemoteAdminCommandEvent -= EventHandlers.OnCommand;
            Events.ConsoleCommandEvent -= EventHandlers.OnConsoleCommand;
            Events.WaitingForPlayersEvent -= EventHandlers.OnWaintingForPlayers;
            Events.RoundStartEvent -= EventHandlers.OnRoundStart;
            Events.RoundEndEvent -= EventHandlers.OnRoundEnd;
            Events.RoundRestartEvent -= EventHandlers.OnRoundRestart;
            Events.WarheadDetonationEvent -= EventHandlers.OnDetonated;
            Events.PlayerJoinEvent -= EventHandlers.OnPlayerJoin;
            Events.PlayerLeaveEvent -= EventHandlers.OnPlayerLeave;
            Events.StartItemsEvent -= EventHandlers.OnStartItems;
            Events.SetClassEvent -= EventHandlers.OnPlayerSetClass;
            Events.PlayerSpawnEvent -= EventHandlers.OnPlayerSpawn;
            Events.PlayerHurtEvent -= EventHandlers.OnPlayerHurt;
            Events.PlayerDeathEvent -= EventHandlers.OnPlayerDeath;
            Events.TriggerTeslaEvent -= EventHandlers.OnPlayerTriggerTesla;
            Events.DoorInteractEvent -= EventHandlers.OnPlayerDoorInteract;
            Events.LockerInteractEvent -= EventHandlers.OnPlayerLockerInteract;
            Events.TeamRespawnEvent -= EventHandlers.OnTeamRespawn;
            Events.GeneratorUnlockEvent -= EventHandlers.OnGeneratorUnlock;
            Events.GeneratorOpenedEvent -= EventHandlers.OnGeneratorOpen;
            Events.GeneratorClosedEvent -= EventHandlers.OnGeneratorClose;
            Events.GeneratorInsertedEvent -= EventHandlers.OnGeneratorInsert;
            Events.GeneratorFinishedEvent -= EventHandlers.OnGeneratorFinish;
            Events.Scp914UpgradeEvent -= EventHandlers.On914Upgrade;
            EventHandlers = null;

            Log.Info($"[OnDisable] SanyaPlugin({Version}) Disabled.");
        }

        public override void OnReload()
        {
            Log.Info($"[OnReload] SanyaPlugin({Version}) Reloaded.");
        }
    }
}
