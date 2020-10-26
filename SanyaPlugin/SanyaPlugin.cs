using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using HarmonyLib;
using MEC;
using SanyaPlugin.Functions;

using ServerEvents = Exiled.Events.Handlers.Server;
using MapEvents = Exiled.Events.Handlers.Map;
using WarheadEvents = Exiled.Events.Handlers.Warhead;
using PlayerEvents = Exiled.Events.Handlers.Player;
using Scp049Events = Exiled.Events.Handlers.Scp049;
using Scp079Events = Exiled.Events.Handlers.Scp079;
using Scp914Events = Exiled.Events.Handlers.Scp914;


namespace SanyaPlugin
{
	public class SanyaPlugin : Plugin<Configs>
	{
		//プラグイン設定
		public override string Name => "SanyaPlugin";
		public override string Prefix => "sanya";
		public override string Author => "sanyae2439";
		public override PluginPriority Priority => PluginPriority.Default;
		public override Version Version => new Version(2, 9, 1);
		public override Version RequiredExiledVersion => new Version(2, 1, 9);

		public static SanyaPlugin Instance { get; private set; }
		public EventHandlers Handlers { get; private set; }
		public Harmony Harmony { get; private set; }
		public Random Random { get; } = new Random();
		private int patchCounter;

		public SanyaPlugin() => Instance = this;

		public override void OnEnabled()
		{
			if(!Config.IsEnabled) return;

			base.OnEnabled();

			RegistEvents();
			Config.ParseConfig();

			if(!string.IsNullOrEmpty(Config.KickVpnApikey)) ShitChecker.LoadLists();
			if(Config.InfosenderIp != "none" && Config.InfosenderPort != -1) Handlers.sendertask = Handlers.SenderAsync().StartSender();

			RegistPatch();

			Log.Info($"[OnEnabled] SanyaPlugin({Version}) Enabled Complete.");
		}

		public override void OnDisabled()
		{
			base.OnDisabled();

			foreach(var cor in Handlers.roundCoroutines)
				Timing.KillCoroutines(cor);
			Handlers.roundCoroutines.Clear();

			UnRegistEvents();
			UnRegistPatch();

			Log.Info($"[OnDisable] SanyaPlugin({Version}) Disabled Complete.");
		}

		private void RegistEvents()
		{
			Handlers = new EventHandlers(this);
			ServerEvents.WaitingForPlayers += Handlers.OnWaintingForPlayers;
			ServerEvents.RoundStarted += Handlers.OnRoundStarted;
			ServerEvents.RoundEnded += Handlers.OnRoundEnded;
			ServerEvents.RestartingRound += Handlers.OnRestartingRound;
			ServerEvents.ReloadedConfigs += Handlers.OnReloadConfigs;
			ServerEvents.RespawningTeam += Handlers.OnRespawningTeam;
			MapEvents.AnnouncingDecontamination += Handlers.OnAnnouncingDecontamination;
			MapEvents.Decontaminating += Handlers.OnDecontaminating;
			MapEvents.GeneratorActivated += Handlers.OnGeneratorActivated;
			WarheadEvents.Starting += Handlers.OnStarting;
			WarheadEvents.Stopping += Handlers.OnStopping;
			WarheadEvents.Detonated += Handlers.OnDetonated;
			PlayerEvents.PreAuthenticating += Handlers.OnPreAuthenticating;
			PlayerEvents.Joined += Handlers.OnJoined;
			PlayerEvents.Left += Handlers.OnLeft;
			PlayerEvents.ChangingRole += Handlers.OnChangingRole;
			PlayerEvents.Spawning += Handlers.OnSpawning;
			PlayerEvents.Hurting += Handlers.OnHurting;
			PlayerEvents.Died += Handlers.OnDied;
			PlayerEvents.FailingEscapePocketDimension += Handlers.OnFailingEscapePocketDimension;
			PlayerEvents.SyncingData += Handlers.OnSyncingData;
			PlayerEvents.MedicalItemUsed += Handlers.OnUsedMedicalItem;
			PlayerEvents.TriggeringTesla += Handlers.OnTriggeringTesla;
			PlayerEvents.InteractingDoor += Handlers.OnInteractingDoor;
			PlayerEvents.InteractingLocker += Handlers.OnInteractingLocker;
			PlayerEvents.UnlockingGenerator += Handlers.OnUnlockingGenerator;
			PlayerEvents.OpeningGenerator += Handlers.OnOpeningGenerator;
			Scp049Events.FinishingRecall += Handlers.OnFinishingRecall;
			Scp079Events.GainingLevel += Handlers.OnGainingLevel;
			Scp914Events.UpgradingItems += Handlers.OnUpgradingItems;
		}

		private void UnRegistEvents()
		{
			ServerEvents.WaitingForPlayers -= Handlers.OnWaintingForPlayers;
			ServerEvents.RoundStarted -= Handlers.OnRoundStarted;
			ServerEvents.RoundEnded -= Handlers.OnRoundEnded;
			ServerEvents.RestartingRound -= Handlers.OnRestartingRound;
			ServerEvents.ReloadedConfigs -= Handlers.OnReloadConfigs;
			ServerEvents.RespawningTeam -= Handlers.OnRespawningTeam;
			MapEvents.AnnouncingDecontamination -= Handlers.OnAnnouncingDecontamination;
			MapEvents.GeneratorActivated -= Handlers.OnGeneratorActivated;
			WarheadEvents.Starting -= Handlers.OnStarting;
			WarheadEvents.Stopping -= Handlers.OnStopping;
			WarheadEvents.Detonated -= Handlers.OnDetonated;
			PlayerEvents.PreAuthenticating -= Handlers.OnPreAuthenticating;
			PlayerEvents.Joined -= Handlers.OnJoined;
			PlayerEvents.Left -= Handlers.OnLeft;
			PlayerEvents.ChangingRole -= Handlers.OnChangingRole;
			PlayerEvents.Spawning -= Handlers.OnSpawning;
			PlayerEvents.Hurting -= Handlers.OnHurting;
			PlayerEvents.Died -= Handlers.OnDied;
			PlayerEvents.FailingEscapePocketDimension -= Handlers.OnFailingEscapePocketDimension;
			PlayerEvents.SyncingData -= Handlers.OnSyncingData;
			PlayerEvents.MedicalItemUsed -= Handlers.OnUsedMedicalItem;
			PlayerEvents.TriggeringTesla -= Handlers.OnTriggeringTesla;
			PlayerEvents.InteractingDoor -= Handlers.OnInteractingDoor;
			PlayerEvents.InteractingLocker -= Handlers.OnInteractingLocker;
			PlayerEvents.UnlockingGenerator -= Handlers.OnUnlockingGenerator;
			PlayerEvents.OpeningGenerator -= Handlers.OnOpeningGenerator;
			Scp079Events.GainingLevel -= Handlers.OnGainingLevel;
			Scp914Events.UpgradingItems -= Handlers.OnUpgradingItems;
			Handlers = null;
		}

		private void RegistPatch()
		{
			try
			{
				Harmony = new Harmony(Author + "." + Name + ++patchCounter);
				Harmony.PatchAll();
			}
			catch(Exception ex)
			{
				Log.Error($"[RegistPatch] Patching Failed : {ex}");
			}
		}

		private void UnRegistPatch()
		{
			Harmony.UnpatchAll();
		}
	}
}