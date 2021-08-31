﻿using System;
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

		public static readonly HashSet<Player> scplists = new HashSet<Player>();
		private static Vector3 _espaceArea = new Vector3(177.5f, 985.0f, 29.0f);

		public Player player { get; private set; }
		public bool DisableHud = false;

		private SanyaPlugin _plugin;

		private string _hudTemplate = "<line-height=95%><voffset=8.5em><align=left><size=50%><alpha=#44>さにゃぷらぐいん(SanyaPlugin) Ex-HUD [VERSION] ([STATS])<alpha=#ff></size></align>\n<align=right>[LIST]</align><align=center>[CENTER_UP][CENTER][CENTER_DOWN][BOTTOM]";
		private float _timer = 0f;
		private int _respawnCounter = -1;
		private string _hudText = string.Empty;
		private string _hudCenterDownString = string.Empty;
		private float _hudCenterDownTime = -1f;
		private float _hudCenterDownTimer = 0f;
		private string _hudBottomDownString = string.Empty;
		private float _hudBottomDownTime = -1f;
		private float _hudBottomDownTimer = 0f;
		private int _prevHealth = -1;

		private void Start()
		{
			_plugin = SanyaPlugin.Instance;
			player = Player.Get(gameObject);
			_hudTemplate = _hudTemplate.Replace("[VERSION]", $"Ver{SanyaPlugin.Instance.Version}");
		}

		private void OnDestroy()
		{
			if(scplists.Contains(player)) 
				scplists.Remove(player);
		}

		private void FixedUpdate()
		{
			if(!_plugin.Config.IsEnabled) return;
			if(!_plugin.Config.ExHudEnabled) return;

			_timer += Time.deltaTime;

			UpdateTimers();
			CheckVoiceChatting();				

			//EverySeconds
			if(_timer > 1f)
			{
				UpdateScpLists();
				UpdateMyCustomText();
				UpdateRespawnCounter();
				UpdateExHud();
				CheckSinkholeDistance();

				_timer = 0f;
			}				
		}

		public void AddHudCenterDownText(string text, ulong timer)
		{
			_hudCenterDownString = text;
			_hudCenterDownTime = timer;
			_hudCenterDownTimer = 0f;
		}

		public void ClearHudCenterDownText()
		{
			_hudCenterDownTime = -1f;
		}

		public void AddHudBottomText(string text, ulong timer)
		{
			_hudBottomDownString = text;
			_hudBottomDownTime = timer;
			_hudBottomDownTimer = 0f;
		}

		public void ClearHudBottomText()
		{
			_hudBottomDownTime = -1f;
		}

		public void UpdateTimers()
		{
			if(_hudCenterDownTimer < _hudCenterDownTime)
				_hudCenterDownTimer += Time.deltaTime;
			else
				_hudCenterDownString = string.Empty;

			if(_hudBottomDownTimer < _hudBottomDownTime)
				_hudBottomDownTimer += Time.deltaTime;
			else
				_hudBottomDownString = string.Empty;
		}

		private void CheckVoiceChatting()
		{
			if(!_plugin.Config.Scp939CanSeeVoiceChatting) return;

			if(player.IsHuman()
				&& player.GameObject.TryGetComponent(out Radio radio)
				&& (radio._syncPrimaryVoicechatButton || radio._syncAltVoicechatButton))
				player.ReferenceHub.footstepSync._visionController.MakeNoise(35f);
		}

		private void CheckSinkholeDistance()
		{
			if(!_plugin.Config.FixSinkhole || SanyaPlugin.Instance.Handlers.Sinkhole == null) return;

			if(!(Vector3.Distance(player.Position, SanyaPlugin.Instance.Handlers.Sinkhole.transform.position) <= 7f) 
				&& player.ReferenceHub.playerEffectsController.GetEffect<SinkHole>().IsEnabled
				)
				player.DisableEffect<SinkHole>();
		}

		private void UpdateMyCustomText()
		{
			if(!player.IsAlive || !SanyaPlugin.Instance.Config.PlayersInfoShowHp) return;
			if(_prevHealth != player.Health) 
			{
				_prevHealth = (int)player.Health;
				player.ReferenceHub.nicknameSync.Network_customPlayerInfoString = $"{_prevHealth} HP";
			}
		}

		private void UpdateRespawnCounter()
		{
			if(!RoundSummary.RoundInProgress() || player.Role != RoleType.Spectator) return;

			if(RespawnManager.CurrentSequence() == RespawnManager.RespawnSequencePhase.RespawnCooldown || RespawnManager.CurrentSequence() == RespawnManager.RespawnSequencePhase.PlayingEntryAnimations)
				_respawnCounter = (int)Math.Truncate(RespawnManager.Singleton._timeForNextSequence - RespawnManager.Singleton._stopwatch.Elapsed.TotalSeconds);
			else
				_respawnCounter = 0;
		}

		private void UpdateScpLists()
		{
			if(player.Team != Team.SCP && scplists.Contains(player))
			{
				scplists.Remove(player);
				return;
			}

			if(player.Team == Team.SCP && !scplists.Contains(player))
			{
				scplists.Add(player);
				return;
			}

		}

		private void UpdateExHud()
		{
			if(DisableHud || !_plugin.Config.ExHudEnabled) return;

			string curText = _hudTemplate.Replace("[STATS]",
				$"St:{DateTime.Now:HH:mm:ss} " +
				$"Rtt:{LiteNetLib4MirrorServer.Peers[player.Connection.connectionId].Ping}ms " +
				$"Ps:{ServerConsole.PlayersAmount}/{CustomNetworkManager.slots} " +
				$"Ti:{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.NineTailedFox)}/{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.ChaosInsurgency)} " +
				$"Vc:{(player.IsMuted ? "D" : "E")}");

			//[SCPLIST]
			if(RoundSummary.singleton.RoundEnded && EventHandlers.sortedDamages != null)
			{
				int rankcounter = 1;
				string resultList = string.Empty;
				resultList += "Round Damage Ranking:\n";
				foreach(var stats in EventHandlers.sortedDamages)
				{
					if(stats.Value == 0) continue;
					resultList += $"[{rankcounter}]{stats.Key}({stats.Value}Damage)\n";
					rankcounter++;
					if(rankcounter > 5) break;
				}
				resultList = resultList.TrimEnd('\n');

				resultList += '\n';

				resultList += "Round Kill Ranking:\n";
				rankcounter = 1;
				foreach(var stats in EventHandlers.sortedKills)
				{
					if(stats.Value == 0) continue;
					resultList += $"[{rankcounter}]{stats.Key}({stats.Value}Kill)\n";
					rankcounter++;
					if(rankcounter > 5) break;
				}
				resultList = resultList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(resultList, 13));
			}
			else if(player.Team == Team.SCP)
			{
				string scpList = string.Empty;
				int scp0492counter = 0;
				foreach(var scp in scplists)
					if(scp.Role == RoleType.Scp0492)
						scp0492counter++;
					else if(scp.Role == RoleType.Scp079)
						scpList += $"{scp.ReferenceHub.characterClassManager.CurRole.fullName}:Tier{scp.ReferenceHub.scp079PlayerScript._curLvl + 1}/{Mathf.RoundToInt(scp.ReferenceHub.scp079PlayerScript.Mana)}AP\n";
					else
						scpList += $"{scp.ReferenceHub.characterClassManager.CurRole.fullName}:{scp.GetHealthAmountPercent()}%\n";
				if(scp0492counter > 0)
					scpList += $"SCP-049-2:({scp0492counter})\n";
				scpList = scpList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(scpList, 7));
			}
			else if(player.Team == Team.MTF)
			{
				string MtfList = string.Empty;
				MtfList += $"<color=#5b6370>FacilityGuard:{RoundSummary.singleton.CountRole(RoleType.FacilityGuard)}</color>\n";
				MtfList += $"<color=#003eca>Captain:{RoundSummary.singleton.CountRole(RoleType.NtfCaptain)}</color>\n";
				MtfList += $"<color=#0096ff>Sergeant:{RoundSummary.singleton.CountRole(RoleType.NtfSergeant)}</color>\n";
				MtfList += $"<color=#6fc3ff>Private:{RoundSummary.singleton.CountRole(RoleType.NtfPrivate)}</color>\n";
				MtfList += $"<color=#0096ff>Specialist:{RoundSummary.singleton.CountRole(RoleType.NtfSpecialist)}</color>\n";
				MtfList += $"<color=#ffff7c>Scientist:{RoundSummary.singleton.CountRole(RoleType.Scientist)}</color>\n";
				MtfList = MtfList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(MtfList, 7));
			}
			else if(player.Team == Team.CHI)
			{
				string CiList = string.Empty;
				CiList += $"<color=#008f1e>Rifleman:{RoundSummary.singleton.CountRole(RoleType.ChaosRifleman)}</color>\n";
				CiList += $"<color=#0a7d34>Repressor:{RoundSummary.singleton.CountRole(RoleType.ChaosRepressor)}</color>\n";
				CiList += $"<color=#006728>Marauder:{RoundSummary.singleton.CountRole(RoleType.ChaosMarauder)}</color>\n";
				CiList += $"<color=#ff8e00>ClassD:{RoundSummary.singleton.CountRole(RoleType.ClassD)}</color>\n";
				CiList = CiList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(CiList, 7));
			}
			else if(player.Role == RoleType.Spectator)
			{
				string RespawnList = string.Empty;
				RespawnList += $"Tickets:<color=#6fc3ff>{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.NineTailedFox)}</color>:";
				RespawnList += $"<color=#008f1e>{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.ChaosInsurgency)}</color>";
				RespawnList += $"({RoundSummary.singleton.CountRole(RoleType.Spectator)} Spectators)\n";

				if(RespawnManager.Singleton.NextKnownTeam != SpawnableTeamType.None && RespawnWaveGenerator.SpawnableTeams.TryGetValue(RespawnManager.Singleton.NextKnownTeam, out var spawnableTeamHandlerBase))
				{
					if(RespawnManager.Singleton._prioritySpawn)
						if(Player.List.Where(x => x.Role == RoleType.Spectator && !x.IsOverwatchEnabled).OrderBy(x => x.ReferenceHub.characterClassManager.DeathTime).Take(spawnableTeamHandlerBase.MaxWaveSize).Contains(player))
							RespawnList += $"あなたは次でリスポーンします";
						else
							RespawnList += $"あなたは対象ではありません";
					else
						RespawnList += $"Random!";
				}

				RespawnList = RespawnList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(RespawnList, 7));
			}
			else
				curText = curText.Replace("[LIST]", FormatStringForHud(string.Empty, 7));

			//[CENTER_UP]
			if(RoundSummary.singleton.RoundEnded && EventHandlers.sortedKills != null)
				curText = curText.Replace("[CENTER_UP]", string.Empty);
			else if(player.Role == RoleType.Scp079)
				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(player.ReferenceHub.animationController.curAnim == 1 ? "Extend:Enabled" : "Extend:Disabled", 6));
			else if(player.Role == RoleType.Scp049)
				if(!player.ReferenceHub.fpc.NetworkforceStopInputs)
					curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"Corpse in stack:{SanyaPlugin.Instance.Handlers.scp049stackAmount}", 6));
				else
					curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"Trying to cure...", 6));
			else if(player.Role == RoleType.Scp096 && player.CurrentScp is PlayableScps.Scp096 scp096)
				switch(scp096.PlayerState)
				{
					case PlayableScps.Scp096PlayerState.TryNotToCry:
					case PlayableScps.Scp096PlayerState.Docile:
						if(scp096.IsPreWindup) curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"PreWindup:{ Mathf.RoundToInt(scp096._preWindupTime)}s", 6));
						else if(!scp096.CanEnrage) curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"Docile:{ Mathf.RoundToInt(scp096.RemainingEnrageCooldown)}s", 6));
						else curText = curText.Replace("[CENTER_UP]", FormatStringForHud("Ready for Enrage...", 6));
						break;
					case PlayableScps.Scp096PlayerState.Enraging:
						curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"Enraging:{ Mathf.RoundToInt(scp096._enrageWindupRemaining)}s", 6));
						break;
					case PlayableScps.Scp096PlayerState.PryGate:
					case PlayableScps.Scp096PlayerState.Enraged:
					case PlayableScps.Scp096PlayerState.Attacking:
					case PlayableScps.Scp096PlayerState.Charging:
						curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"Enraging:{Mathf.RoundToInt(scp096.EnrageTimeLeft)}s", 6));
						break;
					case PlayableScps.Scp096PlayerState.Calming:
						curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"Calming:{Mathf.RoundToInt(scp096._calmingTime)}s", 6));
						break;
					default:
						curText = curText.Replace("[CENTER_UP]", FormatStringForHud(string.Empty, 6));
						break;
				}
			else
				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(string.Empty, 6));

			//[CENTER]
			if(AlphaWarheadController.Host.inProgress && !AlphaWarheadController.Host.detonated && !RoundSummary.singleton.RoundEnded)
			{
				int TargettMinus = AlphaWarheadController._resumeScenario == -1
						? AlphaWarheadController.Host.scenarios_start[AlphaWarheadController._startScenario].tMinusTime
						: AlphaWarheadController.Host.scenarios_resume[AlphaWarheadController._resumeScenario].tMinusTime;

				if(!Methods.IsAlphaWarheadCountdown())
					curText = curText.Replace("[CENTER]", FormatStringForHud($"\n{TargettMinus / 60:00} : {TargettMinus % 60:00}", 6));
				else
					curText = curText.Replace("[CENTER]", FormatStringForHud($"<color=#ff0000>\n{Mathf.FloorToInt(AlphaWarheadController.Host.timeToDetonation) / 60:00} : {Mathf.FloorToInt(AlphaWarheadController.Host.timeToDetonation) % 60:00}</color>", 6));
			}
			else
				curText = curText.Replace("[CENTER]", FormatStringForHud(string.Empty, 6));

			//[CENTER_DOWN]
			if(player.Team == Team.RIP && _respawnCounter != -1 && (!SanyaPlugin.Instance.Config.StopRespawnAfterDetonated || !Warhead.IsDetonated) && !RoundSummary.singleton.RoundEnded)
			{
				if(RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.NineTailedFox) <= 0
				   && RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.ChaosInsurgency) <= 0)
					curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud($"リスポーンチケットがありません", 7));
				else if(RespawnManager.Singleton.NextKnownTeam != SpawnableTeamType.None)
					curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud($"突入まで{_respawnCounter}秒\nチーム:{RespawnManager.Singleton.NextKnownTeam}", 7));
				else
					curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud($"リスポーンまで{_respawnCounter}秒", 7));
			}
			else if(!string.IsNullOrEmpty(_hudCenterDownString))
				curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud(_hudCenterDownString, 7));
			else
				curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud(string.Empty, 7));

			//[BOTTOM]
			if(!string.IsNullOrEmpty(_hudBottomDownString))
				curText = curText.Replace("[BOTTOM]", _hudBottomDownString);
			else if(Intercom.host.speaking && Intercom.host.speaker != null)
				curText = curText.Replace("[BOTTOM]", $"{Player.Get(Intercom.host.speaker)?.Nickname}が放送中...");
			else
				curText = curText.Replace("[BOTTOM]", "　");

			_hudText = curText;
			player.SendTextHintNotEffect(_hudText, 2);
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
