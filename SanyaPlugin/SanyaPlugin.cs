﻿using System;
using System.Collections.Generic;
using System.IO;
using EXILED;
using Harmony;
using MEC;
using SanyaPlugin.Functions;

namespace SanyaPlugin
{
	public class SanyaPlugin : Plugin
	{
		public override string getName { get; } = "SanyaPlugin";
		public static readonly string harmonyId = "jp.sanyae2439.SanyaPlugin";
		public static readonly string Version = "2.0.4c";
		public static readonly string TargetVersion = "1.12.27";
		public static readonly string DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Plugins", "SanyaPlugin");

		public static SanyaPlugin instance { get; private set; }
		public EventHandlers EventHandlers;
		public HarmonyInstance harmony;
		public static Random random = new Random();

		public SanyaPlugin() => instance = this;

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
			if(Configs.kick_vpn) ShitChecker.LoadLists();

			EventHandlers = new EventHandlers(this);
			Events.RemoteAdminCommandEvent += EventHandlers.OnCommand;
			Events.WaitingForPlayersEvent += EventHandlers.OnWaintingForPlayers;
			Events.RoundStartEvent += EventHandlers.OnRoundStart;
			Events.RoundEndEvent += EventHandlers.OnRoundEnd;
			Events.RoundRestartEvent += EventHandlers.OnRoundRestart;
			Events.WarheadStartEvent += EventHandlers.OnWarheadStart;
			Events.WarheadCancelledEvent += EventHandlers.OnWarheadCancel;
			Events.WarheadDetonationEvent += EventHandlers.OnDetonated;
			Events.AnnounceDecontaminationEvent += EventHandlers.OnAnnounceDecont;
			Events.PreAuthEvent += EventHandlers.OnPreAuth;
			Events.PlayerJoinEvent += EventHandlers.OnPlayerJoin;
			Events.PlayerLeaveEvent += EventHandlers.OnPlayerLeave;
			Events.StartItemsEvent += EventHandlers.OnStartItems;
			Events.SetClassEvent += EventHandlers.OnPlayerSetClass;
			Events.PlayerSpawnEvent += EventHandlers.OnPlayerSpawn;
			Events.PlayerHurtEvent += EventHandlers.OnPlayerHurt;
			Events.PlayerDeathEvent += EventHandlers.OnPlayerDeath;
			Events.PocketDimDeathEvent += EventHandlers.OnPocketDimDeath;
			Events.UsedMedicalItemEvent += EventHandlers.OnPlayerUsedMedicalItem;
			Events.TriggerTeslaEvent += EventHandlers.OnPlayerTriggerTesla;
			Events.DoorInteractEvent += EventHandlers.OnPlayerDoorInteract;
			Events.LockerInteractEvent += EventHandlers.OnPlayerLockerInteract;
			Events.SyncDataEvent += EventHandlers.OnPlayerChangeAnim;
			Events.TeamRespawnEvent += EventHandlers.OnTeamRespawn;
			Events.GeneratorUnlockEvent += EventHandlers.OnGeneratorUnlock;
			Events.GeneratorOpenedEvent += EventHandlers.OnGeneratorOpen;
			Events.GeneratorClosedEvent += EventHandlers.OnGeneratorClose;
			Events.GeneratorInsertedEvent += EventHandlers.OnGeneratorInsert;
			Events.GeneratorFinishedEvent += EventHandlers.OnGeneratorFinish;
			Events.Scp106CreatedPortalEvent += EventHandlers.On106MakePortal;
			Events.Scp106TeleportEvent += EventHandlers.On106Teleport;
			Events.Scp079LvlGainEvent += EventHandlers.On079LevelGain;
			Events.Scp914UpgradeEvent += EventHandlers.On914Upgrade;
			Events.ShootEvent += EventHandlers.OnShoot;

			harmony = HarmonyInstance.Create(harmonyId);	
			harmony.PatchAll();

			EventHandlers.sendertask = EventHandlers.SenderAsync().StartSender();

			ServerConsole.singleton.NameFormatter.Commands.Add("mtf_tickets", (List<string> args) => Methods.GetMTFTickets().ToString());
			ServerConsole.singleton.NameFormatter.Commands.Add("ci_tickets", (List<string> args) => Methods.GetCITickets().ToString());

			Log.Info($"[OnEnabled] SanyaPlugin({Version}) Enabled.");
		}

		public override void OnDisable()
		{
			harmony.UnpatchAll();

			foreach(var cor in EventHandlers.roundCoroutines)
				Timing.KillCoroutines(cor);
			EventHandlers.roundCoroutines.Clear();

			Events.RemoteAdminCommandEvent -= EventHandlers.OnCommand;
			Events.WaitingForPlayersEvent -= EventHandlers.OnWaintingForPlayers;
			Events.RoundStartEvent -= EventHandlers.OnRoundStart;
			Events.RoundEndEvent -= EventHandlers.OnRoundEnd;
			Events.RoundRestartEvent -= EventHandlers.OnRoundRestart;
			Events.WarheadStartEvent -= EventHandlers.OnWarheadStart;
			Events.WarheadCancelledEvent -= EventHandlers.OnWarheadCancel;
			Events.WarheadDetonationEvent -= EventHandlers.OnDetonated;
			Events.AnnounceDecontaminationEvent -= EventHandlers.OnAnnounceDecont;
			Events.PreAuthEvent -= EventHandlers.OnPreAuth;
			Events.PlayerJoinEvent -= EventHandlers.OnPlayerJoin;
			Events.PlayerLeaveEvent -= EventHandlers.OnPlayerLeave;
			Events.StartItemsEvent -= EventHandlers.OnStartItems;
			Events.SetClassEvent -= EventHandlers.OnPlayerSetClass;
			Events.PlayerSpawnEvent -= EventHandlers.OnPlayerSpawn;
			Events.PlayerHurtEvent -= EventHandlers.OnPlayerHurt;
			Events.PlayerDeathEvent -= EventHandlers.OnPlayerDeath;
			Events.PocketDimDeathEvent -= EventHandlers.OnPocketDimDeath;
			Events.UsedMedicalItemEvent -= EventHandlers.OnPlayerUsedMedicalItem;
			Events.TriggerTeslaEvent -= EventHandlers.OnPlayerTriggerTesla;
			Events.DoorInteractEvent -= EventHandlers.OnPlayerDoorInteract;
			Events.LockerInteractEvent -= EventHandlers.OnPlayerLockerInteract;
			Events.SyncDataEvent -= EventHandlers.OnPlayerChangeAnim;
			Events.TeamRespawnEvent -= EventHandlers.OnTeamRespawn;
			Events.GeneratorUnlockEvent -= EventHandlers.OnGeneratorUnlock;
			Events.GeneratorOpenedEvent -= EventHandlers.OnGeneratorOpen;
			Events.GeneratorClosedEvent -= EventHandlers.OnGeneratorClose;
			Events.GeneratorInsertedEvent -= EventHandlers.OnGeneratorInsert;
			Events.GeneratorFinishedEvent -= EventHandlers.OnGeneratorFinish;
			Events.Scp106CreatedPortalEvent -= EventHandlers.On106MakePortal;
			Events.Scp106TeleportEvent -= EventHandlers.On106Teleport;
			Events.Scp079LvlGainEvent -= EventHandlers.On079LevelGain;
			Events.Scp914UpgradeEvent -= EventHandlers.On914Upgrade;
			Events.ShootEvent -= EventHandlers.OnShoot;
			EventHandlers = null;

			Log.Info($"[OnDisable] SanyaPlugin({Version}) Disabled.");
		}

		public override void OnReload()
		{
			Log.Info($"[OnReload] SanyaPlugin({Version}) Reloaded.");
		}
	}
}