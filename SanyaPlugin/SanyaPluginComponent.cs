using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
using MEC;
using Mirror.LiteNetLib4Mirror;
using Respawning;
using SanyaPlugin.Data;
using SanyaPlugin.Functions;
using UnityEngine;

namespace SanyaPlugin
{
	public class SanyaPluginComponent : MonoBehaviour
	{

		public static readonly HashSet<Player> _scplists = new HashSet<Player>();
		private static Vector3 _espaceArea = new Vector3(177.5f, 985.0f, 29.0f);
		private static GameObject _portalPrefab;

		public Player Player { get; private set; }
		public bool DisableHud = false;
		public readonly HashSet<Scp939PlayerScript> Faked939s = new HashSet<Scp939PlayerScript>();

		private SanyaPlugin _plugin;

		private string _hudTemplate = "<align=left><voffset=38em><size=50%><alpha=#44>SanyaPlugin Ex-HUD [VERSION] ([STATS])\n<alpha=#ff></size></align><align=right>[LIST]</align><align=center>[CENTER_UP][CENTER][CENTER_DOWN][BOTTOM]</align></voffset>";
		private float _timer = 0f;
		private bool _detectHighPing = false;
		private int _respawnCounter = -1;
		private string _hudText = string.Empty;
		private string _hudCenterDownString = string.Empty;
		private float _hudCenterDownTime = -1f;
		private float _hudCenterDownTimer = 0f;
		private int _prevHealth = -1;
		

		private void Start()
		{
			if(_portalPrefab == null) _portalPrefab = GameObject.Find("SCP106_PORTAL");
			_plugin = SanyaPlugin.Instance;
			Player = Player.Get(gameObject);
			_hudTemplate = _hudTemplate.Replace("[VERSION]", $"Ver{SanyaPlugin.Instance.Version}");
		}

		private void FixedUpdate()
		{
			if(!_plugin.Config.IsEnabled) return;

			_timer += Time.deltaTime;

			UpdateTimers();

			CheckHighPing();
			CheckTraitor();
			CheckVoiceChatting();
			CheckOnPortal();
			CheckFake939();
			UpdateMyCustomText();
			UpdateRespawnCounter();
			UpdateScpLists();
			UpdateExHud();

			if(_timer > 1f)
				_timer = 0f;
		}

		public void AddHudCenterDownText(string text, ulong timer)
		{
			_hudCenterDownString = text;
			_hudCenterDownTime = timer;
			_hudCenterDownTimer = 0f;
		}

		public void ClearHudCenterDownText(string text, ulong timer)
		{
			_hudCenterDownTime = -1f;
		}

		public void UpdateTimers()
		{
			if(_hudCenterDownTimer < _hudCenterDownTime)
				_hudCenterDownTimer += Time.deltaTime;
			else
				_hudCenterDownString = string.Empty;
		}

		private void CheckTraitor()
		{
			if(_plugin.Config.TraitorChancePercent <= 0) return;

			if(Player.Team != Team.MTF && Player.Team != Team.CHI) return;
			if(!Player.IsCuffed) return;
			if(Vector3.Distance(Player.Position, _espaceArea) > Escape.radius) return;

			if(UnityEngine.Random.Range(0, 100) >= _plugin.Config.TraitorChancePercent)
			{
				switch(Player.Team)
				{
					case Team.MTF:
						Player.SetRole(RoleType.ChaosInsurgency);
						break;
					case Team.CHI:
						Player.SetRole(RoleType.NtfCadet);
						break;
				}
			}
			else
				Player.SetRole(RoleType.Spectator);
		}

		private void CheckHighPing()
		{
			if(_detectHighPing) return;
			if(_plugin.Config.PingLimit <= 0) return;

			if(LiteNetLib4MirrorServer.Peers[Player.Connection.connectionId].Ping > _plugin.Config.PingLimit)
			{
				_detectHighPing = true;
				Player.Kick(Subtitles.PingLimittedMessage, "SanyaPlugin_Exiled");
				Log.Warn($"[PingChecker] Kicked:{Player.Nickname}({Player.UserId}) Ping:{LiteNetLib4MirrorServer.Peers[Player.Connection.connectionId].Ping}");
			}
		}

		private void CheckVoiceChatting()
		{
			if(!_plugin.Config.Scp939CanSeeVoiceChatting) return;

			if(Player.IsHuman()
				&& Player.GameObject.TryGetComponent(out Radio radio)
				&& (radio.isVoiceChatting || radio.isTransmitting))
				Player.ReferenceHub.footstepSync._visionController.MakeNoise(25f);
		}

		private void CheckOnPortal()
		{
			if(_portalPrefab == null || !SanyaPlugin.Instance.Config.Scp106PocketTrap ||  !Player.IsHuman()) return;

			if(Vector3.Distance(_portalPrefab.transform.position + Vector3.up * 1.5f, Player.Position) < 1.5f)
			{
				foreach(var scp106 in Player.Get(RoleType.Scp106))
				{
					scp106.ShowHitmarker();
					if(SanyaPlugin.Instance.Config.Scp106SendPocketAhpAmount > 0)
						scp106.ReferenceHub.playerStats.NetworkmaxArtificialHealth += SanyaPlugin.Instance.Config.Scp106SendPocketAhpAmount;
				}

				Player.Position = Vector3.down * 1998.5f;
				Player.ReferenceHub.playerEffectsController.GetEffect<Corroding>().IsInPd = true;
				Player.EnableEffect<Corroding>();
			}
		}

		private void CheckFake939()
		{
			if(SanyaPlugin.Instance.Config.Scp939FakeHumansRange < 0) return;

			foreach(var scp939 in Scp939PlayerScript.instances)
			{
				bool isNear = false;
				if(Vector3.Distance(scp939._hub.playerMovementSync.RealModelPosition, Player.Position) < SanyaPlugin.Instance.Config.Scp939FakeHumansRange) isNear = true;

				if(!Faked939s.Contains(scp939))
				{
					if(!isNear && Player.IsHuman()) 
					{
						Faked939s.Add(scp939);
						SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp939SetFake(Player.ReferenceHub, scp939._hub, Player.Role, (ItemType)UnityEngine.Random.Range((int)ItemType.KeycardJanitor, (int)ItemType.Coin))));
					}
				}
				else
				{
					if(isNear || !Player.IsHuman())
					{
						Faked939s.Remove(scp939);
						SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp939SetFake(Player.ReferenceHub, scp939._hub, scp939._hub.characterClassManager.CurClass, ItemType.None)));
					}
				}
			}
		}

		private void UpdateMyCustomText()
		{
			if(!(_timer > 1f) || !Player.IsAlive || !SanyaPlugin.Instance.Config.PlayersInfoShowHp) return;
			if(_prevHealth != Player.Health) 
			{
				_prevHealth = (int)Player.Health;
				Player.ReferenceHub.nicknameSync.Network_customPlayerInfoString = $"{_prevHealth} HP";
			}
		}

		private void UpdateRespawnCounter()
		{
			if(!RoundSummary.RoundInProgress() || Warhead.IsDetonated || Player.Role != RoleType.Spectator || _timer < 1f) return;

			_respawnCounter = (int)Math.Truncate(RespawnManager.CurrentSequence() == RespawnManager.RespawnSequencePhase.RespawnCooldown ? RespawnManager.Singleton._timeForNextSequence - RespawnManager.Singleton._stopwatch.Elapsed.TotalSeconds : 0);
		}

		private void UpdateScpLists()
		{
			if((Player.Team != Team.SCP || Player.Role == RoleType.Scp0492) && _scplists.Contains(Player))
			{
				_scplists.Remove(Player);
				return;
			}

			if(Player.Team == Team.SCP && Player.Role != RoleType.Scp0492 && !_scplists.Contains(Player))
			{
				_scplists.Add(Player);
				return;
			}

		}

		private void UpdateExHud()
		{
			if(DisableHud || !_plugin.Config.ExHudEnabled) return;
			if(!(_timer > 1f)) return;

			string curText = _hudTemplate.Replace("[STATS]", $"St:{DateTime.Now:HH:mm:ss} Em:{(int)EventHandlers.eventmode} Ps:{ServerConsole.PlayersAmount}/{CustomNetworkManager.slots} Rtt:{LiteNetLib4MirrorServer.Peers[Player.Connection.connectionId].Ping}ms Vc:{(Player.IsMuted ? "D" : "E")}");

			//[SCPLIST]
			if(Player.Team == Team.SCP)
			{
				string scpList = string.Empty;
				foreach(var scp in _scplists)
					if(scp.Role == RoleType.Scp079)
						scpList += $"{scp.ReferenceHub.characterClassManager.CurRole.fullName}:Tier{scp.ReferenceHub.scp079PlayerScript.curLvl + 1}\n";
					else
						scpList += $"{scp.ReferenceHub.characterClassManager.CurRole.fullName}:{scp.GetHealthAmountPercent()}%\n";
				scpList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(scpList, 6));
			}
			else if(Player.Team == Team.MTF)
			{
				string MtfList = string.Empty;
				MtfList += $"FacilityGuard:{RoundSummary.singleton.CountRole(RoleType.FacilityGuard)}\n";
				MtfList += $"Commander:{RoundSummary.singleton.CountRole(RoleType.NtfCommander)}\n";
				MtfList += $"Lieutenant:{RoundSummary.singleton.CountRole(RoleType.NtfLieutenant)}\n";
				MtfList += $"Cadet:{RoundSummary.singleton.CountRole(RoleType.NtfCadet)}\n";
				MtfList += $"NTFScientist:{RoundSummary.singleton.CountRole(RoleType.NtfScientist)}";
				MtfList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(MtfList, 6));
			}
			else
				curText = curText.Replace("[LIST]", FormatStringForHud(string.Empty, 6));

			//[CENTER_UP]
			if(Player.Role == RoleType.Scp079)
				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(Player.ReferenceHub.animationController.curAnim == 1 ? "Extend:Enabled" : "Extend:Disabled", 6));
			else if(Player.Role == RoleType.Scp049)
				if(!Player.ReferenceHub.fpc.NetworkforceStopInputs)
					curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"Corpse in stack:{SanyaPlugin.Instance.Handlers.scp049stackAmount}", 6));
				else
					curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"Trying to cure...", 6));
			else
				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(string.Empty, 6));

			//[CENTER]
			if(AlphaWarheadController.Host.inProgress && !AlphaWarheadController.Host.detonated)
				if(!AlphaWarheadController.Host.doorsOpen)
					curText = curText.Replace("[CENTER]", FormatStringForHud(
						(AlphaWarheadController._resumeScenario < 0
						? AlphaWarheadController.Host.scenarios_resume[AlphaWarheadController._startScenario].tMinusTime.ToString("\n00 : 00")
						: AlphaWarheadController.Host.scenarios_resume[AlphaWarheadController._resumeScenario].tMinusTime.ToString("\n00 : 00")
					), 6));
				else
					curText = curText.Replace("[CENTER]", FormatStringForHud($"<color=#ff0000>{AlphaWarheadController.Host.timeToDetonation.ToString("\n00 : 00")}</color>", 6));
			else
				curText = curText.Replace("[CENTER]", FormatStringForHud(string.Empty, 6));

			//[CENTER_DOWN]
			if(Player.Team == Team.RIP && _respawnCounter != -1 && !Warhead.IsDetonated)
				if(_respawnCounter == 0)
					curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud($"間もなくリスポーンします", 6));
				else
					curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud($"リスポーンまで{_respawnCounter}秒", 6));
			else if(!string.IsNullOrEmpty(_hudCenterDownString))
				curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud(_hudCenterDownString, 6));
			else
				curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud(string.Empty, 6));

			//[BOTTOM]
			if(Intercom.host.speaking && Intercom.host.speaker != null)
				curText = curText.Replace("[BOTTOM]", $"{Player.Get(Intercom.host.speaker)?.Nickname}が放送中...");
			else
				curText = curText.Replace("[BOTTOM]", string.Empty);

			_hudText = curText;
			Player.SendTextHintNotEffect(_hudText, 2);
		}

		private string FormatStringForHud(string text, int needNewLine)
		{
			int curNewLine = text.Count(x => x == '\n');
			for(int i = 0; i < needNewLine - curNewLine; i++)
				text += '\n';
			return text;
		}
	}
}
