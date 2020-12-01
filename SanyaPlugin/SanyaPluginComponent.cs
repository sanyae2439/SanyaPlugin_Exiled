using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
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

		public bool DisableHud = false;

		private SanyaPlugin _plugin;
		private Player _player;
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
			_player = Player.Get(gameObject);
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

			if(_player.Team != Team.MTF && _player.Team != Team.CHI) return;
			if(!_player.IsCuffed) return;
			if(Vector3.Distance(_player.Position, _espaceArea) > Escape.radius) return;

			if(UnityEngine.Random.Range(0, 100) >= _plugin.Config.TraitorChancePercent)
			{
				switch(_player.Team)
				{
					case Team.MTF:
						_player.SetRole(RoleType.ChaosInsurgency);
						break;
					case Team.CHI:
						_player.SetRole(RoleType.NtfCadet);
						break;
				}
			}
			else
				_player.SetRole(RoleType.Spectator);
		}

		private void CheckHighPing()
		{
			if(_detectHighPing) return;
			if(_plugin.Config.PingLimit <= 0) return;

			if(LiteNetLib4MirrorServer.Peers[_player.Connection.connectionId].Ping > _plugin.Config.PingLimit)
			{
				_detectHighPing = true;
				_player.Kick(Subtitles.PingLimittedMessage, "SanyaPlugin_Exiled");
				Log.Warn($"[PingChecker] Kicked:{_player.Nickname}({_player.UserId}) Ping:{LiteNetLib4MirrorServer.Peers[_player.Connection.connectionId].Ping}");
			}
		}

		private void CheckVoiceChatting()
		{
			if(!_plugin.Config.Scp939CanSeeVoiceChatting) return;

			if(_player.IsHuman()
				&& _player.GameObject.TryGetComponent(out Radio radio)
				&& (radio.isVoiceChatting || radio.isTransmitting))
				_player.ReferenceHub.footstepSync._visionController.MakeNoise(25f);
		}

		private void CheckOnPortal()
		{
			if(_portalPrefab == null || !SanyaPlugin.Instance.Config.Scp106PocketTrap ||  !_player.IsHuman()) return;

			if(Vector3.Distance(_portalPrefab.transform.position + Vector3.up * 1.5f, _player.Position) < 1.5f)
			{
				foreach(var scp106 in Player.Get(RoleType.Scp106))
				{
					scp106.ShowHitmarker();
					if(SanyaPlugin.Instance.Config.Scp106SendPocketAhpAmount > 0)
						scp106.ReferenceHub.playerStats.NetworkmaxArtificialHealth += SanyaPlugin.Instance.Config.Scp106SendPocketAhpAmount;
				}

				_player.Position = Vector3.down * 1998.5f;
				_player.ReferenceHub.playerEffectsController.GetEffect<Corroding>().IsInPd = true;
				_player.EnableEffect<Corroding>();
				Log.Debug($"[PortalTrap]");
			}
		}

		private void UpdateMyCustomText()
		{
			if(!(_timer > 1f) || !_player.IsAlive || !SanyaPlugin.Instance.Config.PlayersInfoShowHp) return;
			if(_prevHealth != _player.Health) 
			{
				_prevHealth = (int)_player.Health;
				_player.ReferenceHub.nicknameSync.Network_customPlayerInfoString = $"{_prevHealth} HP";
			}
		}

		private void UpdateRespawnCounter()
		{
			if(!RoundSummary.RoundInProgress() || Warhead.IsDetonated || _player.Role != RoleType.Spectator || _timer < 1f) return;

			_respawnCounter = (int)Math.Truncate(RespawnManager.CurrentSequence() == RespawnManager.RespawnSequencePhase.RespawnCooldown ? RespawnManager.Singleton._timeForNextSequence - RespawnManager.Singleton._stopwatch.Elapsed.TotalSeconds : 0);
		}

		private void UpdateScpLists()
		{
			if((_player.Team != Team.SCP || _player.Role == RoleType.Scp0492) && _scplists.Contains(_player))
			{
				_scplists.Remove(_player);
				return;
			}

			if(_player.Team == Team.SCP && _player.Role != RoleType.Scp0492 && !_scplists.Contains(_player))
			{
				_scplists.Add(_player);
				return;
			}

		}

		private void UpdateExHud()
		{
			if(DisableHud || !_plugin.Config.ExHudEnabled) return;
			if(!(_timer > 1f)) return;

			string curText = _hudTemplate.Replace("[STATS]", $"St:{DateTime.Now:HH:mm:ss} Ps:{ServerConsole.PlayersAmount}/{CustomNetworkManager.slots} Rtt:{LiteNetLib4MirrorServer.Peers[_player.Connection.connectionId].Ping}ms Vc:{(_player.IsMuted ? "D" : "E")}");

			//[SCPLIST]
			if(_player.Team == Team.SCP)
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
			else if(_player.Team == Team.MTF)
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
			if(_player.Role == RoleType.Scp079)
				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(_player.ReferenceHub.animationController.curAnim == 1 ? "Extend:Enabled" : "Extend:Disabled", 6));
			else if(_player.Role == RoleType.Scp049)
				if(!_player.ReferenceHub.fpc.NetworkforceStopInputs)
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
			if(_player.Team == Team.RIP && _respawnCounter != -1)
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
			_player.SendTextHintNotEffect(_hudText, 2);
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
