using System;
using System.Linq;
using System.Reflection;
using Exiled.API.Enums;
using Exiled.API.Features;
using HarmonyLib;
using MEC;
using MapEvents = Exiled.Events.Handlers.Map;
using PlayerEvents = Exiled.Events.Handlers.Player;
using Scp079Events = Exiled.Events.Handlers.Scp079;
using Scp106Events = Exiled.Events.Handlers.Scp106;
using Scp914Events = Exiled.Events.Handlers.Scp914;
using ServerEvents = Exiled.Events.Handlers.Server;
using WarheadEvents = Exiled.Events.Handlers.Warhead;


namespace SanyaPlugin
{
	public class SanyaPlugin : Plugin<Configs, Translations>
	{
		//プラグイン設定
		public override string Name => "SanyaPlugin";
		public override string Prefix => "sanya";
		public override string Author => "sanyae2439";
		public override PluginPriority Priority => PluginPriority.Default;
		public override Version Version => new Version(Assembly.GetName().Version.Major, Assembly.GetName().Version.Minor, Assembly.GetName().Version.Build);
		public override Version RequiredExiledVersion => new Version(4, 0, 0);

		//インスタンス
		public static SanyaPlugin Instance { get; private set; }
		public EventHandlers Handlers { get; private set; }
		public Harmony Harmony { get; private set; }
		public PlayerDataManager PlayerDataManager { get; private set; }
		public ShitChecker ShitChecker { get; private set;}
		public TpsWatcher TpsWatcher { get; private set; }
		public string ExiledFullVersion { get; private set; }
		private int patchCounter;

		public override void OnEnabled()
		{
			base.OnEnabled();
			SanyaPlugin.Instance = this;
			ExiledFullVersion = Exiled.Loader.Loader.Dependencies.First(x => x.GetName().Name == "Exiled.API").GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
			Log.Info($"[OnEnabled] Detect EXILED Version:{ExiledFullVersion}");

			Log.Info("[OnEnabled] Registing events...");
			this.RegistEvents();

			Log.Info("[OnEnabled] Parse configs...");
			Config.ParseConfig();

			Log.Info("[OnEnabled] Loading InfoSender...");
			if(this.Config.InfosenderIp != "none" && this.Config.InfosenderPort != -1) Handlers.sendertask = Handlers.SenderAsync().StartSender();

			Log.Info("[OnEnabled] Loading PlayerDataManager...");
			this.PlayerDataManager = new PlayerDataManager();

			Log.Info("[OnEnabled] Loading ShitChecker...");
			this.ShitChecker = new ShitChecker();
			if(!string.IsNullOrEmpty(Config.KickVpnApikey)) this.ShitChecker.LoadLists();

			Log.Info("[OnEnabled] Loading TpsWatcher...");
			this.TpsWatcher = new TpsWatcher();

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
			Timing.KillCoroutines(TpsWatcher.Coroutine);

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
			ServerEvents.EndingRound += Handlers.OnEndingRound;
			ServerEvents.RoundEnded += Handlers.OnRoundEnded;
			ServerEvents.RestartingRound += Handlers.OnRestartingRound;
			ServerEvents.ReloadedConfigs += Handlers.OnReloadConfigs;
			ServerEvents.RespawningTeam += Handlers.OnRespawningTeam;
			MapEvents.ExplodingGrenade += Handlers.OnExplodingGrenade;
			WarheadEvents.Starting += Handlers.OnStarting;
			WarheadEvents.Stopping += Handlers.OnStopping;
			WarheadEvents.ChangingLeverStatus += Handlers.OnChangingLeverStatus;
			PlayerEvents.PreAuthenticating += Handlers.OnPreAuthenticating;
			PlayerEvents.Verified += Handlers.OnVerified;
			PlayerEvents.Destroying += Handlers.OnDestroying;
			PlayerEvents.ChangingRole += Handlers.OnChangingRole;
			PlayerEvents.Spawning += Handlers.OnSpawning;
			PlayerEvents.Hurting += Handlers.OnHurting;
			PlayerEvents.Dying += Handlers.OnDying;
			PlayerEvents.Died += Handlers.OnDied;
			PlayerEvents.Escaping += Handlers.OnEscaping;
			PlayerEvents.Handcuffing += Handlers.OnHandcuffing;
			PlayerEvents.Jumping += Handlers.OnJumping;
			PlayerEvents.SpawningRagdoll += Handlers.OnSpawningRagdoll;
			PlayerEvents.FailingEscapePocketDimension += Handlers.OnFailingEscapePocketDimension;
			PlayerEvents.TriggeringTesla += Handlers.OnTriggeringTesla;
			PlayerEvents.ProcessingHotkey += Handlers.OnProcessingHotkey;
			Scp079Events.TriggeringDoor += Handlers.OnTriggeringDoor;
			Scp079Events.LockingDown += Handlers.OnLockingDown;
			Scp106Events.CreatingPortal += Handlers.OnCreatingPortal;
			Scp106Events.Containing += Handlers.OnContaining;
			Scp914Events.UpgradingPlayer += Handlers.OnUpgradingPlayer;
		}

		private void UnRegistEvents()
		{
			ServerEvents.WaitingForPlayers -= Handlers.OnWaintingForPlayers;
			ServerEvents.RoundStarted -= Handlers.OnRoundStarted;
			ServerEvents.EndingRound -= Handlers.OnEndingRound;
			ServerEvents.RoundEnded -= Handlers.OnRoundEnded;
			ServerEvents.RestartingRound -= Handlers.OnRestartingRound;
			ServerEvents.ReloadedConfigs -= Handlers.OnReloadConfigs;
			ServerEvents.RespawningTeam -= Handlers.OnRespawningTeam;
			MapEvents.ExplodingGrenade -= Handlers.OnExplodingGrenade;
			WarheadEvents.Starting -= Handlers.OnStarting;
			WarheadEvents.Stopping -= Handlers.OnStopping;
			WarheadEvents.ChangingLeverStatus -= Handlers.OnChangingLeverStatus;
			PlayerEvents.PreAuthenticating -= Handlers.OnPreAuthenticating;
			PlayerEvents.Verified -= Handlers.OnVerified;
			PlayerEvents.Destroying -= Handlers.OnDestroying;
			PlayerEvents.ChangingRole -= Handlers.OnChangingRole;
			PlayerEvents.Spawning -= Handlers.OnSpawning;
			PlayerEvents.Hurting -= Handlers.OnHurting;
			PlayerEvents.Dying -= Handlers.OnDying;
			PlayerEvents.Died -= Handlers.OnDied;
			PlayerEvents.Escaping -= Handlers.OnEscaping;
			PlayerEvents.Handcuffing -= Handlers.OnHandcuffing;
			PlayerEvents.Jumping -= Handlers.OnJumping;
			PlayerEvents.SpawningRagdoll -= Handlers.OnSpawningRagdoll;
			PlayerEvents.FailingEscapePocketDimension -= Handlers.OnFailingEscapePocketDimension;
			PlayerEvents.TriggeringTesla -= Handlers.OnTriggeringTesla;
			PlayerEvents.ProcessingHotkey -= Handlers.OnProcessingHotkey;
			Scp079Events.TriggeringDoor -= Handlers.OnTriggeringDoor;
			Scp079Events.LockingDown -= Handlers.OnLockingDown;
			Scp106Events.CreatingPortal -= Handlers.OnCreatingPortal;
			Scp106Events.Containing -= Handlers.OnContaining;
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