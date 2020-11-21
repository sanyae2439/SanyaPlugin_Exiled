using System;
using System.Linq;
using UnityEngine;
using Mirror.LiteNetLib4Mirror;
using Respawning;
using Exiled.API.Features;

using SanyaPlugin.Data;
using SanyaPlugin.Functions;
using System.Collections.Generic;

namespace SanyaPlugin
{
	public class SanyaPluginComponent : MonoBehaviour
	{

		public static readonly HashSet<Player> _scplists = new HashSet<Player>();

		public bool DisableHud = false;

		private SanyaPlugin _plugin;
		private Player _player;
		private Vector3 _espaceArea;
		private string _hudTemplate = "<align=left><voffset=38em><size=50%><alpha=#44>SanyaPlugin Ex-HUD [VERSION] ([STATS])\n<alpha=#ff></size></align><align=right>[LIST]</align><align=center>[CENTER_UP][CENTER][CENTER_DOWN][BOTTOM]</align></voffset>";
		private float _timer = 0f;
		private bool _detectHighPing = false;
		private int _respawnCounter = -1;
		private string _hudText = string.Empty;
		private string _hudCenterDownString = string.Empty;
		private float _hudCenterDownTime = -1f;
		private float _hudCenterDownTimer = 0f;
		private Player _targetedPlayer = null;

		private void Start()
		{
			_plugin = SanyaPlugin.Instance;
			_player = Player.Get(gameObject);
			_espaceArea = new Vector3(177.5f, 985.0f, 29.0f);
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
			CheckTargetPlayer();
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

		private void CheckTargetPlayer()
		{
			if(!(_timer > 1f)) return;
			if(_targetedPlayer != null && !_player.IsHuman()) _targetedPlayer = null;
			if(!_player.IsHuman()) return;
			Vector3 forward = _player.CameraTransform.forward;
			forward.Scale(new Vector3(0.1f, 0.1f, 0.1f));
			if(Physics.Raycast(this._player.CameraTransform.position + forward, forward, out var hit, 2f, _player.ReferenceHub.characterClassManager.Scp939.attackMask))
			{
				_targetedPlayer = Player.Get(hit.transform.gameObject);
				if(_targetedPlayer != null && _targetedPlayer == _player) _targetedPlayer = null;
			}
			else
				_targetedPlayer = null;
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
			else if(_player.Role == RoleType.Scp106)
				if(SanyaPlugin.Instance.Handlers.last106walkthrough.Elapsed.TotalSeconds > _plugin.Config.Scp106WalkthroughCooldown || _player.IsBypassModeEnabled)
					curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"Extend:Ready", 6));
				else
					curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"Extend:Charging({_plugin.Config.Scp106WalkthroughCooldown - (int)SanyaPlugin.Instance.Handlers.last106walkthrough.Elapsed.TotalSeconds}s left)", 6));
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
			else if(_targetedPlayer != null && !_targetedPlayer.IsEnemy(_player.Team))
				curText = curText.Replace("[CENTER]", FormatStringForHud($"\n\n\n\nTarget HP:{_targetedPlayer.GetHealthAmountPercent()}%", 6));
			else
				curText = curText.Replace("[CENTER]", FormatStringForHud(string.Empty, 6));

			//[CENTER_DOWN]
			if(_player.Team == Team.RIP)
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
