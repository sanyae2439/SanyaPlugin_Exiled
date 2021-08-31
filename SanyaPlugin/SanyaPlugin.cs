using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using HarmonyLib;
using MEC;
using SanyaPlugin.Functions;
using Scp914;
using MapEvents = Exiled.Events.Handlers.Map;
using PlayerEvents = Exiled.Events.Handlers.Player;
using Scp106Events = Exiled.Events.Handlers.Scp106;
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
		public override Version RequiredExiledVersion => new Version(3, 0, 0);

		//インスタンス
		public static SanyaPlugin Instance { get; private set; }
		public EventHandlers Handlers { get; private set; }
		public Harmony Harmony { get; private set; }
		private int patchCounter;

		public override void OnEnabled()
		{
			base.OnEnabled();
			SanyaPlugin.Instance = this;

			Log.Info("[OnEnabled] Registing events...");
			this.RegistEvents();

			Log.Info("[OnEnabled] Parse configs...");
			Config.ParseConfig();

			Log.Info("[OnEnabled] Loading extra functions...");
			if(!string.IsNullOrEmpty(this.Config.KickVpnApikey)) ShitChecker.LoadLists();
			if(this.Config.InfosenderIp != "none" && this.Config.InfosenderPort != -1) Handlers.sendertask = Handlers.SenderAsync().StartSender();

			Log.Info("[OnEnabled] Patching...");
			this.Patch();

			Log.Info($"[OnEnabled] SanyaPlugin(Ver{Version}) Enabled Complete.");
		}

		public override void OnDisabled()
		{
			base.OnDisabled();
			SanyaPlugin.Instance = null;

			Log.Info("[OnDisabled] Cleanup coroutines...");
			foreach(var cor in Handlers.roundCoroutines)
				Timing.KillCoroutines(cor);
			this.Handlers.roundCoroutines.Clear();

			Log.Info("[OnDisabled] Unregisting events...");
			this.UnRegistEvents();

			Log.Info("[OnDisabled] Unpatching...");
			this.Unpatch();

			Log.Info($"[OnDisabled] SanyaPlugin(Ver{Version}) Disabled Complete.");
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
			PlayerEvents.SpawningRagdoll += Handlers.OnSpawningRagdoll;
			PlayerEvents.FailingEscapePocketDimension += Handlers.OnFailingEscapePocketDimension;
			PlayerEvents.SyncingData += Handlers.OnSyncingData;
			PlayerEvents.TriggeringTesla += Handlers.OnTriggeringTesla;
			Scp106Events.CreatingPortal += Handlers.OnCreatingPortal;
			Scp914Events.UpgradingPlayer += Handlers.OnUpgradingPlayer;
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
			MapEvents.Decontaminating -= Handlers.OnDecontaminating;
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
			PlayerEvents.SpawningRagdoll -= Handlers.OnSpawningRagdoll;
			PlayerEvents.FailingEscapePocketDimension -= Handlers.OnFailingEscapePocketDimension;
			PlayerEvents.SyncingData -= Handlers.OnSyncingData;
			PlayerEvents.TriggeringTesla -= Handlers.OnTriggeringTesla;
			Scp106Events.CreatingPortal -= Handlers.OnCreatingPortal;
			Scp914Events.UpgradingPlayer -= Handlers.OnUpgradingPlayer;
			Handlers = null;
		}

		private void Patch()
		{
			try
			{
				Harmony = new Harmony(Author + "." + Name + ++patchCounter);
				Harmony.DEBUG = false;
				Harmony.PatchAll();
			}
			catch(Exception ex)
			{
				Log.Error($"[Patch] Patching Failed : {ex}");
			}
		}

		private void Unpatch()
		{
			try
			{
				Harmony.UnpatchAll(this.Harmony.Id);
			}
			catch(Exception ex)
			{
				Log.Error($"[Unpatch] Unpatching Failed : {ex}");
			}
		}
	}
}