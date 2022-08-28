using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
using Respawning;
using UnityEngine;

namespace SanyaPlugin
{
	public class SanyaPluginComponent : MonoBehaviour
	{

		public static readonly Dictionary<Player, SanyaPluginComponent> Instances = new();
		public static readonly HashSet<Player> scplists = new();

		public Player Player { get; private set; }
		public bool DisableHud = false;

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

		private void Start()
		{
			Player = Player.Get(gameObject);
			if(!Instances.TryGetValue(Player, out _))
				Instances.Add(Player, this);
			_hudTemplate = _hudTemplate.Replace("[VERSION]", $"Ver{SanyaPlugin.Instance.Version}/{SanyaPlugin.Instance.ExiledFullVersion}");
		}

		private void OnDestroy()
		{
			if(scplists.Contains(Player))
				scplists.Remove(Player);
			if(Instances.TryGetValue(Player, out _))
				Instances.Remove(Player);
		}

		private void FixedUpdate()
		{
			if(!SanyaPlugin.Instance.Config.IsEnabled) return;
			if(!SanyaPlugin.Instance.Config.ExHudEnabled) return;
	
			UpdateTimers();

			//EverySeconds
			if(_timer > 1f)
			{
				UpdateScpLists();
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

		private void UpdateTimers()
		{
			_timer += Time.deltaTime;

			if(_hudCenterDownTimer < _hudCenterDownTime)
				_hudCenterDownTimer += Time.deltaTime;
			else
				_hudCenterDownString = string.Empty;

			if(_hudBottomDownTimer < _hudBottomDownTime)
				_hudBottomDownTimer += Time.deltaTime;
			else
				_hudBottomDownString = string.Empty;
		}

		private void UpdateRespawnCounter()
		{
			if(!RoundSummary.RoundInProgress() || Player.Role != RoleType.Spectator) return;

			if(RespawnManager.CurrentSequence() == RespawnManager.RespawnSequencePhase.RespawnCooldown || RespawnManager.CurrentSequence() == RespawnManager.RespawnSequencePhase.PlayingEntryAnimations)
				_respawnCounter = (int)Math.Truncate(RespawnManager.Singleton._timeForNextSequence - RespawnManager.Singleton._stopwatch.Elapsed.TotalSeconds);
			else
				_respawnCounter = 0;
		}

		private void UpdateScpLists()
		{
			if(Player.Role.Team != Team.SCP && scplists.Contains(Player))
			{
				scplists.Remove(Player);
				return;
			}

			if(Player.Role.Team == Team.SCP && !scplists.Contains(Player))
			{
				scplists.Add(Player);
				return;
			}

		}

		private void UpdateExHud()
		{
			if(DisableHud || !SanyaPlugin.Instance.Config.ExHudEnabled) return;

			string curText = _hudTemplate.Replace("[STATS]",
				$"ServerTime:{DateTime.Now:HH:mm:ss} " +
				$"TPS:{TpsWatcher.CurrentTPSInt}");

			/**
			 * [LIST]        = 7
			 * [CENTER_UP]   = 6
			 * [CENTER]      = 6
			 * [CENTER_DOWN] = 7
			 * [BOTTOM]      = Space(" ")
			 * 
			 **/


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
				if(!EventHandlers.sortedDamages.Any(x => x.Value != 0)) resultList += $"無し\n";
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
				if(!EventHandlers.sortedKills.Any(x => x.Value != 0)) resultList += $"無し\n";
				resultList = resultList.TrimEnd('\n');

				resultList += '\n';

				resultList += "Escaped ClassD:\n";
				foreach(var stats in EventHandlers.EscapedClassDDict)
					resultList += $"[{(stats.Value ? "脱出" : "確保")}]{stats.Key}\n";
				if(EventHandlers.EscapedClassDDict.Count == 0) resultList += $"無し\n";
				resultList = resultList.TrimEnd('\n');

				resultList += '\n';

				resultList += "Escaped Scientist:\n";
				foreach(var stats in EventHandlers.EscapedScientistDict)
					resultList += $"[{(stats.Value ? "脱出" : "確保")}]{stats.Key}\n";
				if(EventHandlers.EscapedScientistDict.Count == 0) resultList += $"無し\n";
				resultList = resultList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(resultList, 26));
			}
			else if(Player.Role.Team == Team.SCP)
			{
				string scpList = string.Empty;
				int scp0492counter = 0;
				foreach(var scp in scplists)
				{
					var room = scp.CurrentRoom;
					if(scp.Role == RoleType.Scp0492)
						scp0492counter++;
					else if(scp.Role == RoleType.Scp079)
					{
						if(room.Zone == Exiled.API.Enums.ZoneType.Surface)
							scpList += $"[{Methods.TranslateZoneNameForShort(room.Zone)}:{scp.ReferenceHub.scp079PlayerScript.currentCamera.cameraName}]{scp.ReferenceHub.characterClassManager.CurRole.fullName}:Tier{scp.ReferenceHub.scp079PlayerScript._curLvl + 1}/{Mathf.RoundToInt(scp.ReferenceHub.scp079PlayerScript.Mana)}AP\n";
						else
							scpList += $"[{Methods.TranslateZoneNameForShort(room.Zone)}:{Methods.TranslateRoomName(room.Type)}]{scp.ReferenceHub.characterClassManager.CurRole.fullName}:Tier{scp.ReferenceHub.scp079PlayerScript._curLvl + 1}/{Mathf.RoundToInt(scp.ReferenceHub.scp079PlayerScript.Mana)}AP\n";
					}
					else
					{
						if(room.Zone == Exiled.API.Enums.ZoneType.Surface)
							scpList += $"[{Methods.TranslateZoneNameForShort(room.Zone)}]{scp.ReferenceHub.characterClassManager.CurRole.fullName}:{scp.GetHealthAmountPercent()}%{(scp.ArtificialHealth > 0 ? $"({scp.GetAHPAmountPercent()}%)" : string.Empty)}\n";
						else
							scpList += $"[{Methods.TranslateZoneNameForShort(room.Zone)}:{Methods.TranslateRoomName(room.Type)}]{scp.ReferenceHub.characterClassManager.CurRole.fullName}:{scp.GetHealthAmountPercent()}%{(scp.ArtificialHealth > 0 ? $"({scp.GetAHPAmountPercent()}%)" : string.Empty)}\n";
					}
				}
				if(scp0492counter > 0)
					scpList += $"SCP-049-2:({scp0492counter})\n";
				scpList = scpList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(scpList, 7));
			}
			else if(Player.Role.Team == Team.MTF)
			{
				string MtfList = string.Empty;
				MtfList += $"MTF Tickets:{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.NineTailedFox)}\n";
				MtfList += $"<color=#0096ff>Specialist:</color>{RoundSummary.singleton.CountRole(RoleType.NtfSpecialist)}\n";
				MtfList += $"<color=#003eca>Captain:</color>{RoundSummary.singleton.CountRole(RoleType.NtfCaptain)}\n";
				MtfList += $"<color=#0096ff>Sergeant:</color>{RoundSummary.singleton.CountRole(RoleType.NtfSergeant)}\n";
				MtfList += $"<color=#6fc3ff>Private:</color>{RoundSummary.singleton.CountRole(RoleType.NtfPrivate)}\n";
				MtfList += $"<color=#5b6370>FacilityGuard:</color>{RoundSummary.singleton.CountRole(RoleType.FacilityGuard)}\n";
				MtfList = MtfList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(MtfList, 7));
			}
			else if(Player.Role.Team == Team.CHI)
			{
				string CiList = string.Empty;
				CiList += $"CI Tickets:{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.ChaosInsurgency)}\n";
				CiList += $"<color=#008f1e>Conscript:</color>{RoundSummary.singleton.CountRole(RoleType.ChaosConscript)}\n";
				CiList += $"<color=#0a7d34>Marauder:</color>{RoundSummary.singleton.CountRole(RoleType.ChaosMarauder)}\n";
				CiList += $"<color=#006728>Repressor:</color>{RoundSummary.singleton.CountRole(RoleType.ChaosRepressor)}\n";
				CiList += $"<color=#008f1e>Rifleman:</color>{RoundSummary.singleton.CountRole(RoleType.ChaosRifleman)}\n";
				CiList = CiList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(CiList, 7));
			}
			else if(Player.Role == RoleType.Spectator)
			{
				string RespawnList = string.Empty;
				RespawnList += $"Tickets:<color=#6fc3ff>{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.NineTailedFox)}</color>:";
				RespawnList += $"<color=#008f1e>{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.ChaosInsurgency)}</color>";
				RespawnList += $"({RoundSummary.singleton.CountRole(RoleType.Spectator)} Spectators)\n";

				if(RespawnManager.Singleton.NextKnownTeam != SpawnableTeamType.None && RespawnWaveGenerator.SpawnableTeams.TryGetValue(RespawnManager.Singleton.NextKnownTeam, out var spawnableTeamHandlerBase))
				{
					if(RespawnManager.Singleton._prioritySpawn)
						if(Player.List.Where(x => x.Role == RoleType.Spectator && !x.IsOverwatchEnabled).OrderBy(x => x.ReferenceHub.characterClassManager.DeathTime).Take(spawnableTeamHandlerBase.MaxWaveSize).Contains(Player))
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
			{
				curText = curText.Replace("[CENTER_UP]", string.Empty);
			}
			else if(Player.Role == RoleType.Scp096 && Player.CurrentScp is PlayableScps.Scp096 scp096)
			{
				switch(scp096.PlayerState)
				{
					case PlayableScps.Scp096PlayerState.TryNotToCry:
					case PlayableScps.Scp096PlayerState.Docile:
						if(!scp096.CanEnrage) curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"静寂中:{Mathf.RoundToInt(scp096.RemainingEnrageCooldown)}s", 6));
						else curText = curText.Replace("[CENTER_UP]", FormatStringForHud("", 6));
						break;
					case PlayableScps.Scp096PlayerState.Enraging:
						curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"激怒中:{Mathf.RoundToInt(scp096._enrageWindupRemaining)}s", 6));
						break;
					case PlayableScps.Scp096PlayerState.PryGate:
					case PlayableScps.Scp096PlayerState.Enraged:
					case PlayableScps.Scp096PlayerState.Attacking:
					case PlayableScps.Scp096PlayerState.Charging:
						{
							var sortedTargetDistance = scp096._targets.Select(x => Vector3.Distance(scp096.Hub.playerMovementSync.RealModelPosition, x.playerMovementSync.RealModelPosition)).OrderBy(x => x);
							curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"狂乱中:{Mathf.RoundToInt(scp096.EnrageTimeLeft)}s\n最寄りの対象:{Mathf.RoundToInt(sortedTargetDistance.FirstOrDefault())}m", 6));
							break;
						}
					case PlayableScps.Scp096PlayerState.Calming:
						curText = curText.Replace("[CENTER_UP]", FormatStringForHud($"鎮静中:{Mathf.RoundToInt(scp096._calmingTime)}s", 6));
						break;
					default:
						curText = curText.Replace("[CENTER_UP]", FormatStringForHud(string.Empty, 6));
						break;
				}
			}
			else
				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(string.Empty, 6));

			//[CENTER]
			if(RoundSummary.singleton.RoundEnded && EventHandlers.sortedKills != null)
			{
				curText = curText.Replace("[CENTER]", string.Empty);
			}
			else if(AlphaWarheadController.Host.inProgress && !AlphaWarheadController.Host.detonated && !RoundSummary.singleton.RoundEnded)
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
			if(RoundSummary.singleton.RoundEnded && EventHandlers.sortedKills != null)
			{
				curText = curText.Replace("[CENTER_DOWN]", string.Empty);
			}
			else if(Player.Role.Team == Team.RIP && _respawnCounter != -1 && !RoundSummary.singleton.RoundEnded)
			{
				if(RespawnManager.Singleton.NextKnownTeam != SpawnableTeamType.None)
					curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud($"{RespawnManager.Singleton.NextKnownTeam}が突入まで{_respawnCounter}秒", 7));
				else
					curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud($"部隊到着まで{_respawnCounter}秒", 7));
			}
			else if(!string.IsNullOrEmpty(_hudCenterDownString))
			{
				curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud(_hudCenterDownString, 7));
			}
			else
				curText = curText.Replace("[CENTER_DOWN]", FormatStringForHud(string.Empty, 7));

			//[BOTTOM]
			if(RoundSummary.singleton.RoundEnded && EventHandlers.sortedKills != null)
			{
				curText = curText.Replace("[BOTTOM]", "　");
			}
			if(!string.IsNullOrEmpty(_hudBottomDownString))
			{
				curText = curText.Replace("[BOTTOM]", _hudBottomDownString);
			}
			else if(Intercom.host.speaking && Intercom.host.speaker != null)
			{
				curText = curText.Replace("[BOTTOM]", $"{Player.Get(Intercom.host.speaker)?.Nickname}が放送中...");
			}
			else
				curText = curText.Replace("[BOTTOM]", "　");

			_hudText = curText;
			Player.SendTextHintNotEffect(_hudText, 1.2f);
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
