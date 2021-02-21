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

		public static readonly HashSet<Player> scplists = new HashSet<Player>();
		private static Vector3 _espaceArea = new Vector3(177.5f, 985.0f, 29.0f);

		public Player player { get; private set; }
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
		private byte _prevPreset = 0;
		

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

			CheckHighPing();
			CheckTraitor();
			CheckVoiceChatting();
			CheckRadioRader();
			CheckFake939();
			CheckSinkholeDistance();
			
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

			if(player.Team != Team.MTF && player.Team != Team.CHI) return;
			if(!player.IsCuffed) return;
			if(Vector3.Distance(player.Position, _espaceArea) > Escape.radius) return;

			if(UnityEngine.Random.Range(0, 100) >= _plugin.Config.TraitorChancePercent)
			{
				switch(player.Team)
				{
					case Team.MTF:
						player.SetRole(RoleType.ChaosInsurgency);
						break;
					case Team.CHI:
						player.SetRole(RoleType.NtfCadet);
						break;
				}
			}
			else
				player.SetRole(RoleType.Spectator);
		}

		private void CheckHighPing()
		{
			if(_detectHighPing) return;
			if(_plugin.Config.PingLimit <= 0) return;

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
				player.ReferenceHub.footstepSync._visionController.MakeNoise(25f);
		}

		private void CheckFake939()
		{
			if(SanyaPlugin.Instance.Config.Scp939FakeHumansRange < 0) return;

			foreach(var scp939 in Scp939PlayerScript.instances)
			{
				bool isNear = false;
				if(Vector3.Distance(scp939._hub.playerMovementSync.RealModelPosition, player.Position) < SanyaPlugin.Instance.Config.Scp939FakeHumansRange) isNear = true;

				if(!Faked939s.Contains(scp939))
				{
					if(!isNear && player.IsHuman()) 
					{
						Faked939s.Add(scp939);
						SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp939SetFake(player.ReferenceHub, scp939._hub, player.Role, (ItemType)UnityEngine.Random.Range((int)ItemType.KeycardJanitor, (int)ItemType.Coin))));
					}
				}
				else
				{
					if(isNear || !player.IsHuman())
					{
						Faked939s.Remove(scp939);
						SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.Scp939SetFake(player.ReferenceHub, scp939._hub, scp939._hub.characterClassManager.CurClass, ItemType.None)));
					}
				}
			}
		}

		private void CheckRadioRader()
		{
			if(!(_timer > 1f) || !player.IsAlive || !player.IsHuman) return;

			if(player.CurrentItem != null && player.CurrentItem.id == ItemType.Radio && player.ReferenceHub.TryGetComponent<Radio>(out var radio) && radio.CheckRadio())
			{
				if(radio.curPreset != _prevPreset)
				{
					_prevPreset = radio.curPreset;
					return;
				}

				switch(radio.curPreset)
				{
					case 2:
						{
							AddHudCenterDownText($"<color=#bbee00>Detected {player.CurrentRoom?.Zone}Zone radiowave:{Player.List.Count(x => x.IsAlive && x.CurrentRoom?.Zone == player.CurrentRoom?.Zone && x.Inventory.items.Any(y => y.id == ItemType.Radio))}</color>", 5);
							player.ReferenceHub.inventory.items.ModifyDuration(radio.myRadio, Mathf.Clamp(player.ReferenceHub.inventory.items[radio.myRadio].durability - 5f, 0, 100));
							break;
						}
					case 3:
						{
							AddHudCenterDownText($"<color=#bbee00>Detected {player.CurrentRoom?.Zone}Zone bio-signal:{Player.List.Count(x => x.IsAlive && x.CurrentRoom?.Zone == player.CurrentRoom?.Zone)}</color>", 5);
							player.ReferenceHub.inventory.items.ModifyDuration(radio.myRadio, Mathf.Clamp(player.ReferenceHub.inventory.items[radio.myRadio].durability - 10f, 0, 100));
							break;
						}
					case 4:
						{
							AddHudCenterDownText($"<color=#bbee00>Detected facility's bio-signal:{Player.List.Count(x => x.IsAlive)}</color>", 5);
							player.ReferenceHub.inventory.items.ModifyDuration(radio.myRadio, Mathf.Clamp(player.ReferenceHub.inventory.items[radio.myRadio].durability - 20f, 0, 100));
							break;
						}
				}
			}
		}

		private void CheckSinkholeDistance()
		{
			if(!(_timer > 1f)) return;

			bool inRange = false;
			foreach(var sinkhole in UnityEngine.Object.FindObjectsOfType<SinkholeEnvironmentalHazard>())
				if(Vector3.Distance(player.Position, sinkhole.transform.position) <= 7f)
					inRange = true;

			if(!inRange && player.ReferenceHub.playerEffectsController.GetEffect<SinkHole>().Enabled)
				player.DisableEffect<SinkHole>();
		}

		private void UpdateMyCustomText()
		{
			if(!(_timer > 1f) || !player.IsAlive || !SanyaPlugin.Instance.Config.PlayersInfoShowHp) return;
			if(_prevHealth != player.Health) 
			{
				_prevHealth = (int)player.Health;
				player.ReferenceHub.nicknameSync.Network_customPlayerInfoString = $"{_prevHealth} HP";
			}
		}

		private void UpdateRespawnCounter()
		{
			if(!RoundSummary.RoundInProgress() || Warhead.IsDetonated || player.Role != RoleType.Spectator || _timer < 1f) return;

			_respawnCounter = (int)Math.Truncate(RespawnManager.CurrentSequence() == RespawnManager.RespawnSequencePhase.RespawnCooldown ? RespawnManager.Singleton._timeForNextSequence - RespawnManager.Singleton._stopwatch.Elapsed.TotalSeconds : 0);
		}

		private void UpdateScpLists()
		{
			if((player.Team != Team.SCP || player.Role == RoleType.Scp0492) && scplists.Contains(player))
			{
				scplists.Remove(player);
				return;
			}

			if(player.Team == Team.SCP && player.Role != RoleType.Scp0492 && !scplists.Contains(player))
			{
				scplists.Add(player);
				return;
			}

		}

		private void UpdateExHud()
		{
			if(DisableHud || !_plugin.Config.ExHudEnabled) return;
			if(!(_timer > 1f)) return;

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
				string damageList = string.Empty;
				damageList += "Round Damage Ranking:\n";
				foreach(var stats in EventHandlers.sortedDamages)
				{
					if(stats.Value == 0) continue;
					damageList += $"[{rankcounter}]{stats.Key}({stats.Value}Damage)\n";
					rankcounter++;
					if(rankcounter > 5) break;
				}
				damageList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(damageList, 6));
			}
			else if(player.Team == Team.SCP)
			{
				string scpList = string.Empty;
				foreach(var scp in scplists)
					if(scp.Role == RoleType.Scp079)
						scpList += $"{scp.ReferenceHub.characterClassManager.CurRole.fullName}:Tier{scp.ReferenceHub.scp079PlayerScript.curLvl + 1}\n";
					else
						scpList += $"{scp.ReferenceHub.characterClassManager.CurRole.fullName}:{scp.GetHealthAmountPercent()}%\n";
				scpList.TrimEnd('\n');

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
				MtfList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(MtfList, 6));
			}
			else if(player.Team == Team.CHI)
			{
				string CiList = string.Empty;
				CiList += $"<color=#008f1e>ChaosInsurgency:{RoundSummary.singleton.CountRole(RoleType.ChaosInsurgency)}</color>\n";
				CiList += $"<color=#ff8e00>ClassD:{RoundSummary.singleton.CountRole(RoleType.ClassD)}</color>\n";
				CiList.TrimEnd('\n');

				curText = curText.Replace("[LIST]", FormatStringForHud(CiList, 6));
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
			if(player.Team == Team.RIP && _respawnCounter != -1 && !Warhead.IsDetonated)
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
