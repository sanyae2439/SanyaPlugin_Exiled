using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using InventorySystem.Items.Firearms.Modules;
using MapGeneration.Distributors;
using MEC;
using PlayerStatsSystem;
using Respawning;
using SanyaPlugin.Components;
using UnityEngine;
using Utils.Networking;

namespace SanyaPlugin
{
	public class SanyaPluginComponent : MonoBehaviour
	{

		public static readonly Dictionary<Player, SanyaPluginComponent> Instances = new();
		public static readonly HashSet<Player> scplists = new();

		public Player Player { get; private set; }
		public AhpStat.AhpProcess Shield { get; private set; }
		public bool DisableHud = false;

		private string _hudTemplate = "<line-height=95%><voffset=8.5em><align=left><size=50%><alpha=#44>さにゃぷらぐいん(SanyaPlugin) Ex-HUD [VERSION] ([STATS])<alpha=#ff></size></align>\n<align=right>[LIST]</align><align=center>[CENTER_UP][CENTER][CENTER_DOWN][BOTTOM]";
		private float _timer = 0f;
		private int _respawnCounter = -1;
		private int _prevHealth = -1;
		private int _prevAhp = -1;
		private string _hudText = string.Empty;
		private string _hudCenterDownString = string.Empty;
		private float _hudCenterDownTime = -1f;
		private float _hudCenterDownTimer = 0f;
		private string _hudBottomDownString = string.Empty;
		private float _hudBottomDownTime = -1f;
		private float _hudBottomDownTimer = 0f;
		private SinkHole sinkHoleEffect = null;

		//Scan
		private Camera079 _lastcam = null;

		//SCP-106 ExHotkey
		private bool isHiding = false;
		private bool IsTeleportMode = false;
		private float captureTimerDefault = 30f;
		private float captureTimer = 0f;
		private float trapTimerDefault = 5f;
		private float trapTimer = 0f;
		private float hideTimerDefault = 10f;
		private float hideTimer = 0f;

		//SCP-079 ExHotkey
		private float playgunTimerDefault = 3f;
		private float playgunTimer = 0f;
		private float markingTimerDefault = 30f;
		private float markingTimer = 0f;
		private float flashingTimerDefault = 60f;
		private float flashingTimer = 0f;
		private float scanningTimerDefault = 120f;
		private float scanningTimer = 0f;

		private void Start()
		{
			Player = Player.Get(gameObject);
			if(!Instances.TryGetValue(Player, out _))
				Instances.Add(Player, this);
			_hudTemplate = _hudTemplate.Replace("[VERSION]", $"Ver{SanyaPlugin.Instance.Version}/{SanyaPlugin.Instance.ExiledFullVersion}");
			sinkHoleEffect = Player.ReferenceHub.playerEffectsController.GetEffect<SinkHole>();
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
				Timing.CallDelayed(Timing.WaitForOneFrame, Segment.FixedUpdate, () => SetupShield(newRole));
			else
				ResetShield();
		}

		public void OnDamage()
		{
			if(Shield != null)
			{
				if(Player.Role == RoleType.Scp049)
					Shield.SustainTime = SanyaPlugin.Instance.Config.Scp049TimeUntilRegen;
				else if(Player.Role == RoleType.Scp106)
					Shield.SustainTime = SanyaPlugin.Instance.Config.Scp106TimeUntilRegen;
			}
		}

		public void OnProcessingHotkey(HotkeyButton hotkeyButton)
		{
			if(Player.Role == RoleType.Scp106)
			{
				switch(hotkeyButton)
				{
					case HotkeyButton.PrimaryFirearm:
						{
							if(trapTimer <= 0f)
							{
								if(!IsTeleportMode)
								{
									if(SanyaPlugin.Instance.Handlers.Sinkholes.Count > 1 && Player.ReferenceHub.playerMovementSync.Grounded && !Player.ReferenceHub.scp106PlayerScript.goingViaThePortal)
										Methods.MoveNetworkIdentityObject(SanyaPlugin.Instance.Handlers.Sinkholes[1].netIdentity, Get106PortalDiff());
								}
								else
								{
									if(SanyaPlugin.Instance.Handlers.Sinkholes.Count > 1)
										SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp106CustomTeleport(Player, SanyaPlugin.Instance.Handlers.Sinkholes[1].transform.position + Vector3.up * 2f), Segment.FixedUpdate));
								}
								trapTimer = trapTimerDefault;
							}
							break;
						}
					case HotkeyButton.SecondaryFirearm:
						{
							if(trapTimer <= 0f)
							{
								if(!IsTeleportMode)
								{
									if(SanyaPlugin.Instance.Handlers.Sinkholes.Count > 2 && Player.ReferenceHub.playerMovementSync.Grounded && !Player.ReferenceHub.scp106PlayerScript.goingViaThePortal)
										Methods.MoveNetworkIdentityObject(SanyaPlugin.Instance.Handlers.Sinkholes[2].netIdentity, Get106PortalDiff());
								}
								else
								{
									if(SanyaPlugin.Instance.Handlers.Sinkholes.Count > 2)
										SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp106CustomTeleport(Player, SanyaPlugin.Instance.Handlers.Sinkholes[2].transform.position + Vector3.up * 2f), Segment.FixedUpdate));
								}
								trapTimer = trapTimerDefault;
							}
							break;
						}
					case HotkeyButton.Keycard:
						{
							if(captureTimer <= 0f)
							{
								HashSet<Player> targets = new HashSet<Player>();
								foreach(var i in Player.List.Where(x => x.IsHuman))
									foreach(var sinkhole in SanyaPlugin.Instance.Handlers.Sinkholes)
										if(Vector3.Distance(i.Position, sinkhole.transform.position) <= 5f)
											targets.Add(i);
								foreach(var target in targets)
								{
									Player.ReferenceHub.characterClassManager.RpcPlaceBlood(target.Position, 1, 2f);
									Player.ReferenceHub.scp106PlayerScript.TargetHitMarker(Player.Connection, Player.ReferenceHub.scp106PlayerScript.captureCooldown);
									Player.ReferenceHub.scp106PlayerScript._currentServerCooldown = Player.ReferenceHub.scp106PlayerScript.captureCooldown;
									if(Scp106PlayerScript._blastDoor.isClosed)
									{
										Player.ReferenceHub.characterClassManager.RpcPlaceBlood(target.Position, 1, 2f);
										target.ReferenceHub.playerStats.DealDamage(new ScpDamageHandler(Player.ReferenceHub, DeathTranslations.PocketDecay));
									}
									else
									{
										foreach(Scp079PlayerScript scp079PlayerScript in Scp079PlayerScript.instances)
										{
											scp079PlayerScript.ServerProcessKillAssist(target.ReferenceHub, ExpGainType.PocketAssist);
										}
										target.ReferenceHub.scp106PlayerScript.GrabbedPosition = target.ReferenceHub.playerMovementSync.RealModelPosition;
										target.ReferenceHub.playerStats.DealDamage(new ScpDamageHandler(Player.ReferenceHub, 40f, DeathTranslations.PocketDecay));
									}
									target.ReferenceHub.playerEffectsController.EnableEffect<Corroding>();
								}
								captureTimer = captureTimerDefault;
							}
							break;
						}
					case HotkeyButton.Grenade:
						{
							if(!isHiding)
							{
								if(Player.ReferenceHub.playerMovementSync.Grounded && !Player.ReferenceHub.scp106PlayerScript.goingViaThePortal && hideTimer <= 0f)
								{
									bool canHide = false;
									foreach(var sinkhole in SanyaPlugin.Instance.Handlers.Sinkholes)
										if(Vector3.Distance(Player.Position, sinkhole.transform.position) <= 5f)
											canHide = true;

									if(canHide)
									{
										Player.Scale = Vector3.one / 5f;
										Player.Position += Vector3.up;
										Player.ReferenceHub.fpc.NetworkforceStopInputs = true;
										Player.EnableEffect<Invisible>();
										Player.EnableEffect<Amnesia>();
										Player.EnableEffect<Deafened>();
										isHiding = true;
									}
								}
							}
							else
							{
								Player.Scale = Vector3.one;
								Player.Position += Vector3.up;
								Player.ReferenceHub.fpc.NetworkforceStopInputs = false;
								Player.DisableEffect<Invisible>();
								Player.DisableEffect<Amnesia>();
								Player.DisableEffect<Deafened>();
								isHiding = false;
								hideTimer = hideTimerDefault;
							}
							break;
						}
					case HotkeyButton.Medical:
						{
							IsTeleportMode = !IsTeleportMode;
							break;
						}
				}
			}
			else if(Player.Role == RoleType.Scp079)
			{
				switch(hotkeyButton)
				{
					case HotkeyButton.PrimaryFirearm:
					case HotkeyButton.SecondaryFirearm:
						{
							if(playgunTimer <= 0f && Methods.GetCurrentRoomsSpeaker(Player.CurrentRoom) != null)
							{
								SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp079PlayDummySound(Player), Segment.FixedUpdate));
								playgunTimer = playgunTimerDefault;
							}
							break;
						}
					case HotkeyButton.Keycard:
						{
							if(markingTimer <= 0f && Player.ReferenceHub.scp079PlayerScript.Lvl >= 1)
							{
								if(Physics.Raycast(Player.ReferenceHub.scp079PlayerScript.currentCamera.transform.position,
									Player.ReferenceHub.scp079PlayerScript.currentCamera.targetPosition.forward,
									out var raycastHit,
									StandardHitregBase.HitregMask))
								{
									new DisruptorHitreg.DisruptorHitMessage()
									{
										Position = raycastHit.point + raycastHit.normal * 0.1f,
										Rotation = new LowPrecisionQuaternion(Quaternion.LookRotation(-raycastHit.normal))
									}.SendToAuthenticated();

									var hub = raycastHit.collider.GetComponentInParent<ReferenceHub>();
									if(hub != null)
									{
										var target = Player.Get(hub);
										if(Player.IsEnemy(target.Role.Team))
										{
											if(target.GameObject.TryGetComponent<LightMoveComponent>(out var lightMove))
												lightMove.Timer = 60f;
											else
												target.GameObject.AddComponent<LightMoveComponent>().Timer = 60f;

											target.EnableEffect<Concussed>(5f);
											target.EnableEffect<Disabled>(5f);

											target.ReferenceHub.playerStats.DealDamage(new DisruptorDamageHandler(new Footprinting.Footprint(Player.ReferenceHub), 20f));

											Player.SendHitmarker();
										}
									}
								}
								markingTimer = markingTimerDefault;
							}
							break;
						}
					case HotkeyButton.Grenade:
						{
							if(flashingTimer <= 0f && Player.ReferenceHub.scp079PlayerScript.Lvl >= 2)
							{
								SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp079RoomFlashing(Player), Segment.FixedUpdate));
								flashingTimer = flashingTimerDefault;
							}
							break;
						}
					case HotkeyButton.Medical:
						{
							if(scanningTimer <= 0f && Player.ReferenceHub.scp079PlayerScript.Lvl >= 3)
							{
								SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp079ScanningHumans(Player), Segment.FixedUpdate));
								scanningTimer = scanningTimerDefault;
							}
							break;
						}
				}
			}
		}

		private Vector3 Get106PortalDiff()
		{
			if(Physics.Raycast(new Ray(Player.Position, -Player.GameObject.transform.up), out var raycastHit, 10f, Player.ReferenceHub.scp106PlayerScript.teleportPlacementMask))
				return raycastHit.point - Vector3.up;
			return Vector3.zero;
		}

		private void SetupShield(RoleType roleType)
		{
			if(Shield != null)
				ResetShield();

			Shield = Player.ReferenceHub.playerStats.GetModule<AhpStat>().ServerAddProcess(0f, 0f, 0f, 1f, 0f, true);

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
				Player.ReferenceHub.playerStats.GetModule<AhpStat>().ServerKillProcess(Shield.KillCode);

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

			if(captureTimer > 0)
				captureTimer -= Time.deltaTime;
			if(trapTimer > 0)
				trapTimer -= Time.deltaTime;
			if(hideTimer > 0)
				hideTimer -= Time.deltaTime;

			if(playgunTimer > 0)
				playgunTimer -= Time.deltaTime;
			if(markingTimer > 0)
				markingTimer -= Time.deltaTime;
			if(flashingTimer > 0)
				flashingTimer -= Time.deltaTime;
			if(scanningTimer > 0)
				scanningTimer -= Time.deltaTime;
		}

		private void CheckVoiceChatting()
		{
			if(!SanyaPlugin.Instance.Config.Scp939CanSeeVoiceChatting) return;

			if(Player.IsHuman() && (Player.Radio._syncPrimaryVoicechatButton || Player.Radio._syncAltVoicechatButton))
				Player.ReferenceHub.footstepSync._visionController.MakeNoise(35f);
		}

		private void CheckSinkholeDistance()
		{
			foreach(var sinkhole in SanyaPlugin.Instance.Handlers.Sinkholes)
				if(Vector3.Distance(Player.Position, sinkhole.transform.position) > 7f && sinkHoleEffect.IsEnabled)
					Player.DisableEffect<SinkHole>();
		}

		private void Check079Spot()
		{
			if(!SanyaPlugin.Instance.Config.Scp079ScanRoom || Player.Role != RoleType.Scp079 || Player.CurrentRoom == null || Player.ReferenceHub.scp079PlayerScript.currentCamera == _lastcam) return;

			string message = string.Empty;
			if(Player.CurrentRoom.GetComponentsInChildren<Scp079Generator>().Any(x => x.Activating))
				message = $"<color=#bbee00><size=25>発電機が起動を開始している\n場所：{Methods.TranslateRoomName(Player.CurrentRoom.Type)}</color></size>\n";
			else
			{
				var target = Player.CurrentRoom.Players.FirstOrDefault(x => x.Role.Team != Team.SCP && x.Role.Team != Team.RIP);
				if(target != null)
				{
					if(Player.CurrentRoom.Zone == Exiled.API.Enums.ZoneType.Surface)
						message = $"<color=#bbee00><size=25>SCP-079が{target.ReferenceHub.characterClassManager.CurRole.fullName}を発見した\n場所：{Methods.TranslateZoneName(Player.CurrentRoom.Zone)}</color></size>\n";
					else
						message = $"<color=#bbee00><size=25>SCP-079が{target.ReferenceHub.characterClassManager.CurRole.fullName}を発見した\n場所：{Methods.TranslateZoneName(Player.CurrentRoom.Zone)}の{Methods.TranslateRoomName(Player.CurrentRoom.Type)}</color></size>\n";
				}
			}

			if(!string.IsNullOrEmpty(message))
				foreach(var scp in Player.Get(Team.SCP))
					scp.ReferenceHub.GetComponent<SanyaPluginComponent>().AddHudCenterDownText(message, 5);

			_lastcam = Player.ReferenceHub.scp079PlayerScript.currentCamera;
		}

		private void UpdateMyCustomText()
		{
			if(!Player.IsAlive || !SanyaPlugin.Instance.Config.PlayersInfoShowHp) return;
			if(_prevHealth != Player.Health || _prevAhp != Player.ArtificialHealth)
			{
				_prevHealth = (int)Player.Health;
				_prevAhp = (int)Player.ArtificialHealth;
				Player.ReferenceHub.nicknameSync.Network_customPlayerInfoString = $"{_prevHealth} HP{(_prevAhp != 0 ? $"\n{_prevAhp} AHP" : "")}";
			}
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
			else if(Player.Role == RoleType.Scp0492)
			{
				string text = string.Empty;

				text += $"Adrenaline Level:{(Player.GetEffectIntensity<MovementBoost>() / 10) - 1}/4";

				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(text, 6));
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
			else if(Player.Role == RoleType.Scp079 && SanyaPlugin.Instance.Config.ExHudEnabled && SanyaPlugin.Instance.Config.Scp079ExHotkey)
			{
				string text = string.Empty;

				text += "<align=left><alpha=#44>　ExHotkey:\n";
				text += $"　　　[武器]:銃声再生({(playgunTimer <= 0f ? "使用可能" : $"あと{Mathf.FloorToInt(playgunTimer)}秒")})\n";
				text += $"[キーカード]:マーキングビーム({(markingTimer <= 0f ? (Player.ReferenceHub.scp079PlayerScript.Lvl >= 1 ? "使用可能" : "Tier不足") : $"あと{Mathf.FloorToInt(markingTimer)}秒")})\n";
				text += $"[グレネード]:ライトフラッシュ({(flashingTimer <= 0f ? (Player.ReferenceHub.scp079PlayerScript.Lvl >= 2 ? "使用可能" : "Tier不足") : $"あと{Mathf.FloorToInt(flashingTimer)}秒")})\n";
				text += $"　　　[回復]:レーダースキャン({(scanningTimer <= 0f ? (Player.ReferenceHub.scp079PlayerScript.Lvl >= 3 ? "使用可能" : "Tier不足") : $"あと{Mathf.FloorToInt(scanningTimer)}秒")})";
				text += "</align>\n<alpha=#FF><align=center>";

				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(text, 6));
			}
			else if(Player.Role == RoleType.Scp106 && SanyaPlugin.Instance.Config.ExHudEnabled && SanyaPlugin.Instance.Config.Scp106ExHotkey)
			{
				string text = string.Empty;

				text += "<align=left><alpha=#44>　ExHotkey:\n";
				text += $"　　　[武器]:{(IsTeleportMode ? "トラップへテレポート" : "トラップの設置")}({(trapTimer <= 0f ? "使用可能" : $"あと{Mathf.FloorToInt(trapTimer)}秒")})\n";
				text += $"[キーカード]:トラップで捕獲({(captureTimer <= 0f ? "使用可能" : $"あと{Mathf.FloorToInt(captureTimer)}秒")})\n";
				text += $"[グレネード]:トラップに隠れる({(hideTimer <= 0f ? "使用可能" : $"あと{Mathf.FloorToInt(hideTimer)}秒")})\n";
				text += $"　　　[回復]:テレポートの切替";
				text += "</align>\n<alpha=#FF><align=center>";

				curText = curText.Replace("[CENTER_UP]", FormatStringForHud(text, 6));
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
