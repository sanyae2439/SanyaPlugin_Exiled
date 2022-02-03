using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
using MapGeneration.Distributors;
using MEC;
using Mirror.LiteNetLib4Mirror;
using PlayerStatsSystem;
using Respawning;
using UnityEngine;

namespace SanyaPlugin
{
	public class SanyaPluginComponent : MonoBehaviour
	{

		public static readonly Dictionary<Player, SanyaPluginComponent> Instances = new Dictionary<Player, SanyaPluginComponent>();
		public static readonly HashSet<Player> scplists = new HashSet<Player>();

		public Player player { get; private set; }
		public AhpStat.AhpProcess Shield { get; private set; }
		public bool DisableHud = false;

		private string _hudTemplate = "<line-height=95%><voffset=8.5em><align=left><size=50%><alpha=#44>さにゃぷらぐいん(SanyaPlugin) Ex-HUD [VERSION] ([STATS])<alpha=#ff></size></align>\n<align=right>[LIST]</align><align=center>[CENTER_UP][CENTER][CENTER_DOWN][BOTTOM]";
		private float _timer = 0f;
		private int _respawnCounter = -1;
		private int _prevHealth = -1;
		private string _hudText = string.Empty;
		private string _hudCenterDownString = string.Empty;
		private float _hudCenterDownTime = -1f;
		private float _hudCenterDownTimer = 0f;
		private string _hudBottomDownString = string.Empty;
		private float _hudBottomDownTime = -1f;
		private float _hudBottomDownTimer = 0f;

		//Scan
		private Camera079 _lastcam = null;

		private void Start()
		{
			player = Player.Get(gameObject);
			if(!Instances.TryGetValue(player, out _))
				Instances.Add(player, this);
			_hudTemplate = _hudTemplate.Replace("[VERSION]", $"Ver{SanyaPlugin.Instance.Version}");
		}

		private void OnDestroy()
		{
			if(scplists.Contains(player))
				scplists.Remove(player);
			if(Instances.TryGetValue(player, out _))
				Instances.Remove(player);
		}

		private void FixedUpdate()
		{
			if(!SanyaPlugin.Instance.Config.IsEnabled) return;
			if(!SanyaPlugin.Instance.Config.ExHudEnabled) return;

			_timer += Time.deltaTime;

			UpdateTimers();
			CheckVoiceChatting();

			//EverySeconds
			if(_timer > 1f)
			{
				CheckSinkholeDistance();
				Check079Spot();

				UpdateScpLists();
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

		public void OnChangingRole(RoleType newRole, RoleType prevRole)
		{
			if(newRole == RoleType.Scp049 || newRole == RoleType.Scp106)
				Timing.CallDelayed(Timing.WaitForOneFrame, () => SetupShield(newRole));
			else
				ResetShield();
		}

		public void OnDamage()
		{
			if(Shield != null)
			{
				if(player.Role == RoleType.Scp049)
					Shield.SustainTime = SanyaPlugin.Instance.Config.Scp049TimeUntilRegen;
				else if(player.Role == RoleType.Scp106)
					Shield.SustainTime = SanyaPlugin.Instance.Config.Scp106TimeUntilRegen;
			}
		}

		private void SetupShield(RoleType roleType)
		{
			if(Shield != null)
				ResetShield();

			Shield = player.ReferenceHub.playerStats.GetModule<AhpStat>().ServerAddProcess(0f, 0f, 0f, 1f, 0f, true);

			if(roleType == RoleType.Scp049)
			{
				Shield.CurrentAmount = SanyaPlugin.Instance.Config.Scp049MaxAhp;
				Shield.DecayRate = -SanyaPlugin.Instance.Config.Scp049RegenRate;
				Shield.Limit = SanyaPlugin.Instance.Config.Scp049MaxAhp;
			}
			else if(roleType == RoleType.Scp106)
			{
				Shield.CurrentAmount = SanyaPlugin.Instance.Config.Scp106MaxAhp;
				Shield.DecayRate = -SanyaPlugin.Instance.Config.Scp106RegenRate;
				Shield.Limit = SanyaPlugin.Instance.Config.Scp106MaxAhp;
			}
		}

		private void ResetShield()
		{
			if(Shield != null)
				player.ReferenceHub.playerStats.GetModule<AhpStat>().ServerKillProcess(Shield.KillCode);

			Shield = null;
		}

		private void UpdateTimers()
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
			if(!SanyaPlugin.Instance.Config.Scp939CanSeeVoiceChatting) return;

			if(player.IsHuman()
				&& player.GameObject.TryGetComponent(out Radio radio)
				&& (radio._syncPrimaryVoicechatButton || radio._syncAltVoicechatButton))
				player.ReferenceHub.footstepSync._visionController.MakeNoise(35f);
		}

		private void CheckSinkholeDistance()
		{
			if(SanyaPlugin.Instance.Handlers.Sinkhole == null) return;

			if(!(Vector3.Distance(player.Position, SanyaPlugin.Instance.Handlers.Sinkhole.transform.position) <= 7f)
				&& player.ReferenceHub.playerEffectsController.GetEffect<SinkHole>().IsEnabled
				)
				player.DisableEffect<SinkHole>();
		}

		private void Check079Spot()
		{
			if(!SanyaPlugin.Instance.Config.Scp079ScanRoom || player.Role != RoleType.Scp079 || player.CurrentRoom == null || player.Camera == _lastcam) return;

			string message = string.Empty;
			if(player.CurrentRoom.GetComponentsInChildren<Scp079Generator>().Any(x => x.Activating))
				message = $"<color=#bbee00><size=25>発電機が起動を開始している\n場所：{player.CurrentRoom.Type}</color></size>\n";
			else
			{
				var target = player.CurrentRoom.Players.FirstOrDefault(x => x.Team != Team.SCP && x.Team != Team.RIP && x.Team != Team.CHI);
				if(target != null)
				{
					if(player.CurrentRoom.Zone == Exiled.API.Enums.ZoneType.Surface)
						message = $"<color=#bbee00><size=25>SCP-079が{target.ReferenceHub.characterClassManager.CurRole.fullName}を発見した\n場所：{Methods.TranslateZoneName(player.CurrentRoom.Zone)}</color></size>\n";
					else
						message = $"<color=#bbee00><size=25>SCP-079が{target.ReferenceHub.characterClassManager.CurRole.fullName}を発見した\n場所：{Methods.TranslateZoneName(player.CurrentRoom.Zone)}の{Methods.TranslateRoomName(player.CurrentRoom.Type)}</color></size>\n";
				}
			}

			if(!string.IsNullOrEmpty(message))
				foreach(var scp in Player.Get(Team.SCP))
					scp.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(message, 5);

			_lastcam = player.Camera;
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
			if(DisableHud || !SanyaPlugin.Instance.Config.ExHudEnabled) return;

			string curText = _hudTemplate.Replace("[STATS]",
				$"ServerTime:{DateTime.Now:HH:mm:ss}");

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
			else if(player.Team == Team.SCP)
			{
				string scpList = string.Empty;
				int scp0492counter = 0;
				foreach(var scp in scplists)
					if(scp.Role == RoleType.Scp0492)
						scp0492counter++;
					else if(scp.Role == RoleType.Scp079)
						scpList += $"[{scp.CurrentRoom?.Type}]{scp.ReferenceHub.characterClassManager.CurRole.fullName}:Tier{scp.ReferenceHub.scp079PlayerScript._curLvl + 1}/{Mathf.RoundToInt(scp.ReferenceHub.scp079PlayerScript.Mana)}AP\n";
					else
						scpList += $"[{scp.CurrentRoom?.Type}]{scp.ReferenceHub.characterClassManager.CurRole.fullName}:{scp.GetHealthAmountPercent()}%{(scp.ArtificialHealth > 0 ? $"({scp.GetAHPAmountPercent()}%)" : string.Empty)}\n";
				if(scp0492counter > 0)
					scpList += $"SCP-049-2:({scp0492counter})\n";
				scpList = scpList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(scpList, 7));
			}
			else if(player.Team == Team.MTF)
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
			else if(player.Team == Team.CHI)
			{
				string CiList = string.Empty;
				CiList += $"CI Tickets:{RespawnTickets.Singleton.GetAvailableTickets(SpawnableTeamType.ChaosInsurgency)}\n";
				CiList += $"<color=#008f1e>Conscript:</color>{RoundSummary.singleton.CountRole(RoleType.ChaosConscript)}\n";
				CiList += $"<color=#0a7d34>Repressor:</color>{RoundSummary.singleton.CountRole(RoleType.ChaosRepressor)}\n";
				CiList += $"<color=#006728>Marauder:</color>{RoundSummary.singleton.CountRole(RoleType.ChaosMarauder)}\n";	
				CiList += $"<color=#008f1e>Rifleman:</color>{RoundSummary.singleton.CountRole(RoleType.ChaosRifleman)}\n";
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
			{
				curText = curText.Replace("[CENTER_UP]", string.Empty);
			}
			else if(player.Role == RoleType.Scp0492)
			{
				string text = string.Empty;

				text += $"Adrenaline Level:{(player.GetEffectIntensity<MovementBoost>() / 10) - 1}/4";

				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(text, 6));
			}
			else if(player.Role == RoleType.Scp079)
			{
				string text = string.Empty;

				if(player.Level > 0)
					text += player.ReferenceHub.scp079PlayerScript.CurrentLDCooldown <= 0f ? "LockDown:Ready" : $"LockDown:Cooldown({Mathf.RoundToInt(player.ReferenceHub.scp079PlayerScript.CurrentLDCooldown)})";

				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(text, 6));
			}
			else if(player.Role == RoleType.Scp096 && player.CurrentScp is PlayableScps.Scp096 scp096)
			{
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
			else if(player.Team == Team.RIP && _respawnCounter != -1 && !RoundSummary.singleton.RoundEnded)
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
