using System;
using Exiled.API.Features;
using HarmonyLib;
using LightContainmentZoneDecontamination;
using MapGeneration.Distributors;
using Respawning;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Intercom), nameof(Intercom.UpdateText))]
	public static class IntercomTextPatch
	{
		public static void Prefix(Intercom __instance)
		{
			if(!SanyaPlugin.Instance.Config.IntercomInformation) return;

			int leftdecont = (int)Math.Truncate((DecontaminationController.Singleton.DecontaminationPhases[DecontaminationController.Singleton.DecontaminationPhases.Length - 1].TimeTrigger) - Math.Truncate(DecontaminationController.GetServerTime));
			int leftautowarhead = AlphaWarheadController.Host != null ? (int)Mathf.Clamp(AlphaWarheadController.Host._autoDetonateTime - (AlphaWarheadController.Host._autoDetonateTime - AlphaWarheadController.Host._autoDetonateTimer), 0, AlphaWarheadController.Host._autoDetonateTimer) : -1;
			int nextRespawn = (int)Math.Truncate(RespawnManager.CurrentSequence() == RespawnManager.RespawnSequencePhase.RespawnCooldown ? RespawnManager.Singleton._timeForNextSequence - RespawnManager.Singleton._stopwatch.Elapsed.TotalSeconds : 0);
			bool isContain = PlayerManager.localPlayer.GetComponent<CharacterClassManager>()._lureSpj.allowContain;
			bool isAlreadyUsed = OneOhSixContainer.used;

			leftdecont = Mathf.Clamp(leftdecont, 0, leftdecont);

			string contentfix = string.Concat(
				$"作戦経過時間 : {RoundSummary.roundTime / 60:00}:{RoundSummary.roundTime % 60:00}\n",
				$"残存SCPオブジェクト : {RoundSummary.singleton.CountTeam(Team.SCP):00}/{RoundSummary.singleton.classlistStart.scps_except_zombies:00}\n",
				$"残存Dクラス職員 : {RoundSummary.singleton.CountTeam(Team.CDP):00}/{RoundSummary.singleton.classlistStart.class_ds:00}\n",
				$"残存研究員 : {RoundSummary.singleton.CountTeam(Team.RSC):00}/{RoundSummary.singleton.classlistStart.scientists:00}\n",
				$"AlphaWarheadのステータス : {(AlphaWarheadOutsitePanel.nukeside.enabled ? "READY" : "DISABLED")}\n",
				$"SCP-106再収容設備：{(isContain ? (isAlreadyUsed ? "使用済み" : "準備完了") : "人員不在")}\n",
				$"軽度収容区画閉鎖まで : {leftdecont / 60:00}:{leftdecont % 60:00}\n",
				$"自動施設爆破開始まで : {leftautowarhead / 60:00}:{leftautowarhead % 60:00}\n",
				$"接近中の部隊突入まで : {nextRespawn / 60:00}:{nextRespawn % 60:00}\n\n"
				);

			if(__instance.Muted)
			{
				contentfix += "アクセスが拒否されました";
			}
			else if(Intercom.AdminSpeaking)
			{
				contentfix += $"施設管理者が放送設備をオーバーライド中";
			}
			else if(__instance.remainingCooldown > 0f)
			{
				contentfix += $"放送設備再起動中 : 残り{Mathf.CeilToInt(__instance.remainingCooldown)}秒";
			}
			else if(__instance.speaker != null)
			{
				contentfix += $"{Player.Get(__instance.speaker).Nickname}が放送中... : 残り{Mathf.CeilToInt(__instance.speechRemainingTime)}秒";
			}
			else
			{
				contentfix += "放送設備準備完了";
			}

			__instance.CustomContent = contentfix;

			return;
		}
	}
}
