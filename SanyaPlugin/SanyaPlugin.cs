using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events;
using Grenades;
using HarmonyLib;
using MEC;
using SanyaPlugin.Functions;
using MapEvents = Exiled.Events.Handlers.Map;
using PlayerEvents = Exiled.Events.Handlers.Player;
using Scp049Events = Exiled.Events.Handlers.Scp049;
using Scp079Events = Exiled.Events.Handlers.Scp079;
using Scp106Events = Exiled.Events.Handlers.Scp106;
using Scp173Events = Exiled.Events.Handlers.Scp173;
using Scp914Events = Exiled.Events.Handlers.Scp914;
using ServerEvents = Exiled.Events.Handlers.Server;
using WarheadEvents = Exiled.Events.Handlers.Warhead;


namespace SanyaPlugin
{
	public class SanyaPlugin : Plugin<Configs>
	{
		//プラグイン設定
		public override string Name => "SanyaPlugin";
		public override string Prefix => "sanya";
		public override string Author => "sanyae2439";
		public override PluginPriority Priority => PluginPriority.Default;
		public override Version Version => new Version(Assembly.GetName().Version.Major, Assembly.GetName().Version.Minor, Assembly.GetName().Version.Build);
		public override Version RequiredExiledVersion => new Version(2, 10, 0);

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

			Log.Info("[OnEnabled] Registing events...");
			RegistEvents();

			Log.Info("[OnEnabled] Parse configs...");
			Config.ParseConfig();

			if(!string.IsNullOrEmpty(Config.KickVpnApikey)) ShitChecker.LoadLists();
			if(Config.InfosenderIp != "none" && Config.InfosenderPort != -1) Handlers.sendertask = Handlers.SenderAsync().StartSender();

			Log.Info("[OnEnabled] Regist patches...");
			RegistPatch();

			Log.Info($"[OnEnabled] SanyaPlugin(Ver{Version}) Enabled Complete.");
		}

		public override void OnDisabled()
		{
			base.OnDisabled();

			foreach(var cor in Handlers.roundCoroutines)
				Timing.KillCoroutines(cor);
			Handlers.roundCoroutines.Clear();

			UnRegistEvents();
			UnRegistPatch();

			Log.Info($"[OnDisable] SanyaPlugin(Ver{Version}) Disabled Complete.");
		}

		private void RegistEvents()
		{
			Events.DisabledPatchesHashSet.Add(typeof(FlashGrenade).GetMethod(nameof(FlashGrenade.ServersideExplosion)));
			Events.DisabledPatchesHashSet.Add(typeof(Handcuffs).GetMethod(nameof(Handcuffs.CallCmdCuffTarget)));
			Events.Instance.ReloadDisabledPatches();

			Handlers = new EventHandlers(this);
			ServerEvents.WaitingForPlayers += Handlers.OnWaintingForPlayers;
			ServerEvents.RoundStarted += Handlers.OnRoundStarted;
			ServerEvents.RoundEnded += Handlers.OnRoundEnded;
			ServerEvents.RestartingRound += Handlers.OnRestartingRound;
			ServerEvents.ReloadedConfigs += Handlers.OnReloadConfigs;
			ServerEvents.RespawningTeam += Handlers.OnRespawningTeam;
			MapEvents.AnnouncingDecontamination += Handlers.OnAnnouncingDecontamination;
			MapEvents.AnnouncingNtfEntrance += Handlers.OnAnnouncingNtfEntrance;
			MapEvents.Decontaminating += Handlers.OnDecontaminating;
			MapEvents.GeneratorActivated += Handlers.OnGeneratorActivated;
			WarheadEvents.Starting += Handlers.OnStarting;
			WarheadEvents.Stopping += Handlers.OnStopping;
			WarheadEvents.Detonated += Handlers.OnDetonated;
			PlayerEvents.PreAuthenticating += Handlers.OnPreAuthenticating;
			PlayerEvents.Verified += Handlers.OnVerified;
			PlayerEvents.Destroying += Handlers.OnDestroying;
			PlayerEvents.ChangingRole += Handlers.OnChangingRole;
			PlayerEvents.Spawning += Handlers.OnSpawning;
			PlayerEvents.Hurting += Handlers.OnHurting;
			PlayerEvents.Died += Handlers.OnDied;
			PlayerEvents.FailingEscapePocketDimension += Handlers.OnFailingEscapePocketDimension;
			PlayerEvents.SyncingData += Handlers.OnSyncingData;
			PlayerEvents.MedicalItemDequipped += Handlers.OnDequippedMedicalItem;
			PlayerEvents.InteractingDoor += Handlers.OnInteractingDoor;
			PlayerEvents.InteractingLocker += Handlers.OnInteractingLocker;
			PlayerEvents.UnlockingGenerator += Handlers.OnUnlockingGenerator;
			PlayerEvents.OpeningGenerator += Handlers.OnOpeningGenerator;
			PlayerEvents.TriggeringTesla += Handlers.OnTriggeringTesla;
			PlayerEvents.ChangingItem += Handlers.OnChangingItem;
			PlayerEvents.ActivatingWarheadPanel += Handlers.OnActivatingWarheadPanel;
			Scp049Events.FinishingRecall += Handlers.OnFinishingRecall;
			Scp079Events.GainingLevel += Handlers.OnGainingLevel;
			Scp106Events.CreatingPortal += Handlers.OnCreatingPortal;
			Scp173Events.Blinking += Handlers.OnBlinking;
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
			MapEvents.AnnouncingNtfEntrance -= Handlers.OnAnnouncingNtfEntrance;
			MapEvents.Decontaminating -= Handlers.OnDecontaminating;
			MapEvents.GeneratorActivated -= Handlers.OnGeneratorActivated;
			WarheadEvents.Starting -= Handlers.OnStarting;
			WarheadEvents.Stopping -= Handlers.OnStopping;
			WarheadEvents.Detonated -= Handlers.OnDetonated;
			PlayerEvents.PreAuthenticating -= Handlers.OnPreAuthenticating;
			PlayerEvents.Verified -= Handlers.OnVerified;
			PlayerEvents.Destroying -= Handlers.OnDestroying;
			PlayerEvents.ChangingRole -= Handlers.OnChangingRole;
			PlayerEvents.Spawning -= Handlers.OnSpawning;
			PlayerEvents.Hurting -= Handlers.OnHurting;
			PlayerEvents.Died -= Handlers.OnDied;
			PlayerEvents.FailingEscapePocketDimension -= Handlers.OnFailingEscapePocketDimension;
			PlayerEvents.SyncingData -= Handlers.OnSyncingData;
			PlayerEvents.MedicalItemDequipped -= Handlers.OnDequippedMedicalItem;
			PlayerEvents.InteractingDoor -= Handlers.OnInteractingDoor;
			PlayerEvents.InteractingLocker -= Handlers.OnInteractingLocker;
			PlayerEvents.UnlockingGenerator -= Handlers.OnUnlockingGenerator;
			PlayerEvents.OpeningGenerator -= Handlers.OnOpeningGenerator;
			PlayerEvents.TriggeringTesla -= Handlers.OnTriggeringTesla;
			PlayerEvents.ChangingItem -= Handlers.OnChangingItem;
			PlayerEvents.ActivatingWarheadPanel -= Handlers.OnActivatingWarheadPanel;
			Scp049Events.FinishingRecall -= Handlers.OnFinishingRecall;
			Scp079Events.GainingLevel -= Handlers.OnGainingLevel;
			Scp106Events.CreatingPortal -= Handlers.OnCreatingPortal;
			Scp173Events.Blinking -= Handlers.OnBlinking;
			Scp914Events.UpgradingItems -= Handlers.OnUpgradingItems;
			Handlers = null;
		}

		private void RegistPatch()
		{
			try
			{
				Harmony = new Harmony(Author + "." + Name + ++patchCounter);
				Harmony.DEBUG = false;
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