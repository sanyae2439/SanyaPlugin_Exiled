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

		private string _hudTemplate = "<align=left><voffset=38em><size=50%><alpha=#44>SanyaPlugin Ex-HUD [VERSION] ([STATS])\n<alpha=#ff></size></align><align=right>[LIST]</align><align=center>[CENTER_UP][CENTER][CENTER_DOWN][BOTTOM]</align></voffset>";
		private float _timer = 0f;
		private bool _detectHighPing = false;
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

			_timer += Time.deltaTime;

			UpdateTimers();
			UpdateScpLists();

			CheckVoiceChatting();				

			//EverySeconds
			if(_timer > 1f)
			{
				CheckHighPing();
				CheckSinkholeDistance();

				UpdateMyCustomText();
				UpdateRespawnCounter();
				UpdateExHud();

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

		private void CheckHighPing()
		{
			if(_plugin.Config.PingLimit <= 0 || _detectHighPing) return;

			if(LiteNetLib4MirrorServer.Peers[player.Connection.connectionId].Ping > _plugin.Config.PingLimit)
			{
				_detectHighPing = true;
				player.Kick(Subtitles.PingLimittedMessage, "SanyaPlugin_Exiled");
				Log.Warn($"[PingChecker] Kicked:{player.Nickname}({player.UserId}) Ping:{LiteNetLib4MirrorServer.Peers[player.Connection.connectionId].Ping}");
			}
		}

		private void CheckVoiceChatting()
		{
			if(!_plugin.Config.Scp939CanSeeVoiceChatting) return;

			if(player.IsHuman()
				&& player.GameObject.TryGetComponent(out Radio radio)
				&& (radio.isVoiceChatting || radio.isTransmitting))
				player.ReferenceHub.footstepSync._visionController.MakeNoise(35f);
		}

		private void CheckSinkholeDistance()
		{
			bool inRange = false;
			foreach(var sinkhole in UnityEngine.Object.FindObjectsOfType<SinkholeEnvironmentalHazard>())
				if(Vector3.Distance(player.Position, sinkhole.transform.position) <= 7f)
					inRange = true;

			if(!inRange && player.ReferenceHub.playerEffectsController.GetEffect<SinkHole>().Enabled)
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
			if(!RoundSummary.RoundInProgress() || Warhead.IsDetonated || player.Role != RoleType.Spectator) return;

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
				$"Em:{(int)EventHandlers.eventmode} " +
				$"Ti:{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.NineTailedFox)}/{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.ChaosInsurgency)} " +
				$"Vc:{(player.IsMuted ? "D" : "E")}");

			//[SCPLIST]
			if(RoundSummary.singleton._roundEnded && EventHandlers.sortedDamages != null)
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

				curText = curText.Replace("[LIST]", FormatStringForHud(resultList, 12));
			}
			else if(player.Team == Team.SCP)
			{
				string scpList = string.Empty;
				int scp0492counter = 0;
				foreach(var scp in scplists)
					if(scp.Role == RoleType.Scp0492)
						scp0492counter++;
					else if(scp.Role == RoleType.Scp079)
						scpList += $"{scp.ReferenceHub.characterClassManager.CurRole.fullName}:Tier{scp.ReferenceHub.scp079PlayerScript.curLvl + 1}/{Mathf.RoundToInt(scp.ReferenceHub.scp079PlayerScript.Mana)}AP/{Generator079.mainGenerator.totalVoltage}Gens\n";
					else
						scpList += $"{scp.ReferenceHub.characterClassManager.CurRole.fullName}:{scp.GetHealthAmountPercent()}%\n";
				if(scp0492counter > 0)
					scpList += $"SCP-049-2:({scp0492counter})\n";
				scpList = scpList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(scpList, 6));
			}
			else if(player.Team == Team.MTF)
			{
				string MtfList = string.Empty;
				MtfList += $"<color=#5b6370>FacilityGuard:{RoundSummary.singleton.CountRole(RoleType.FacilityGuard)}</color>\n";
				MtfList += $"<color=#003eca>Commander:{RoundSummary.singleton.CountRole(RoleType.NtfCommander)}</color>\n";
				MtfList += $"<color=#0096ff>Lieutenant:{RoundSummary.singleton.CountRole(RoleType.NtfLieutenant)}</color>\n";
				MtfList += $"<color=#6fc3ff>Cadet:{RoundSummary.singleton.CountRole(RoleType.NtfCadet)}</color>\n";
				MtfList += $"<color=#0096ff>NTFScientist:{RoundSummary.singleton.CountRole(RoleType.NtfScientist)}</color>\n";
				MtfList += $"<color=#ffff7c>Scientist:{RoundSummary.singleton.CountRole(RoleType.Scientist)}</color>\n";
				MtfList = MtfList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(MtfList, 6));
			}
			else if(player.Team == Team.CHI)
			{
				string CiList = string.Empty;
				CiList += $"<color=#008f1e>ChaosInsurgency:{RoundSummary.singleton.CountRole(RoleType.ChaosInsurgency)}</color>\n";
				CiList += $"<color=#ff8e00>ClassD:{RoundSummary.singleton.CountRole(RoleType.ClassD)}</color>\n";
				CiList = CiList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(CiList, 6));
			}
			else if(player.Role == RoleType.Spectator)
			{
				string RespawnList = string.Empty;
				RespawnList += $"Tickets:<color=#6fc3ff>{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.NineTailedFox)}</color>:";
				RespawnList += $"<color=#008f1e>{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.ChaosInsurgency)}</color>";
				RespawnList += $" Next({RoundSummary.singleton.CountRole(RoleType.Spectator)} Spectators):\n";

				if(RespawnManager.Singleton._prioritySpawn)
					if(Player.List.Where(x => x.Role == RoleType.Spectator && !x.IsOverwatchEnabled).OrderBy(x => x.ReferenceHub.characterClassManager.DeathTime).Take(5).Contains(player))
						RespawnList += $"次でリスポーンします";
					else
						RespawnList += $"対象ではありません";				
				else
					RespawnList += $"Random!";

				RespawnList = RespawnList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(RespawnList, 6));
			}
			else
				curText = curText.Replace("[LIST]", FormatStringForHud(string.Empty, 6));

			//[CENTER_UP]
			if(player.Role == RoleType.Scp079)
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
						else curText = curText.Replace("[CENTER_UP]", FormatStringForHud(string.Empty, 6));
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
			else if(!RoundSummary.singleton._roundEnded && EventHandlers.sortedKills != null)
				curText = curText.Replace("[CENTER_UP]", string.Empty);
			else
				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(string.Empty, 6));

			//[CENTER]
			if(AlphaWarheadController.Host.inProgress && !AlphaWarheadController.Host.detonated && !RoundSummary.singleton._roundEnded)
			{
				int TargettMinus = AlphaWarheadController._resumeScenario == -1
						? AlphaWarheadController.Host.scenarios_start[AlphaWarheadController._startScenario].tMinusTime
						: AlphaWarheadController.Host.scenarios_resume[AlphaWarheadController._resumeScenario].tMinusTime;

				if(!Methods.IsAlphaWarheadCountdown())
					curText = curText.Replace("[CENTER]", FormatStringForHud($"\n{TargettMinus / 60:00} : {TargettMinus % 60:00}", 6));
				else
					curText = curText.Replace("[CENTER]", FormatStringForHud($"<color=#ff0000>\n{Mathf.FloorToInt(AlphaWarheadController.Host.timeToDetonation) / 60:00} : {Mathf.FloorToInt(AlphaWarheadController.Host.timeToDetonation) % 60:00}</color>", 6));
			}
			else if(player.Role == RoleType.Scp079 && Generator079.Generators.Any(x => x.isTabletConnected))
			{
				string CounterList = "\n";
				foreach(var i in Generator079.Generators.Where(x => x.isTabletConnected).OrderBy(x => x.remainingPowerup))
					CounterList += $"<color=#ffff00>({i.CurRoom}){Mathf.FloorToInt(i.remainingPowerup) / 60:00} : {Mathf.FloorToInt(i.remainingPowerup) % 60:00}</color>\n";
				CounterList = CounterList.TrimEnd('\n');
				curText = curText.Replace("[CENTER]", FormatStringForHud(CounterList, 6));
			}
			else
				curText = curText.Replace("[CENTER]", FormatStringForHud(string.Empty, 6));

			//[CENTER_DOWN]
			if(player.Team == Team.RIP && _respawnCounter != -1 && !Warhead.IsDetonated && !RoundSummary.singleton._roundEnded)
			{
				if(RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.NineTailedFox) <= 0
				   && RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.ChaosInsurgency) <= 0)
					curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud($"リスポーンチケットがありません", 6));
				else if(RespawnManager.Singleton.NextKnownTeam != SpawnableTeamType.None)
					curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud($"突入まで{_respawnCounter}秒\nチーム:{RespawnManager.Singleton.NextKnownTeam}", 6));
				else
					curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud($"リスポーンまで{_respawnCounter}秒", 6));
			}
			else if(!string.IsNullOrEmpty(_hudCenterDownString))
				curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud(_hudCenterDownString, 6));
			else
				curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud(string.Empty, 6));

			//[BOTTOM]
			if(!string.IsNullOrEmpty(_hudBottomDownString))
				curText = curText.Replace("[BOTTOM]", _hudBottomDownString);
			else if(Intercom.host.speaking && Intercom.host.speaker != null)
				curText = curText.Replace("[BOTTOM]", $"{Player.Get(Intercom.host.speaker)?.Nickname}が放送中...");
			else
				curText = curText.Replace("[BOTTOM]", string.Empty);

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
