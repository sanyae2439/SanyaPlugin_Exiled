using Exiled.API.Features;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(AnimationController), nameof(AnimationController.CallCmdChangeSpeedState))]
	public static class JumpAttackPatch
	{
		public static void Prefix(AnimationController __instance, byte newState)
		{
			if(!SanyaPlugin.Instance.Config.JumpingKickAttack || __instance.curAnim != 2 || __instance._curMoveState == newState || newState != 1 || !__instance.ccm.IsHuman())
				return;

			Log.Debug($"Attack:{__instance.ccm._hub.nicknameSync.MyNick}", SanyaPlugin.Instance.Config.IsDebugged);
			var player = Player.Get(__instance.gameObject);
			if(player.SessionVariables.TryGetValue("sanya_kickattack_lasttime", out object time) && (NetworkTime.time - (double)time) < 0.2) return;
			foreach(var hits in Physics.RaycastAll(__instance.ccm._hub.PlayerCameraReference.position, __instance.ccm._hub.PlayerCameraReference.forward, 1.5f, 4))
			{
				var target = Player.Get(hits.transform.gameObject);
				if(target?.Id == player?.Id) continue;

				if(player.ReferenceHub.weaponManager.GetShootPermission(target.Team) || ServerConsole.FriendlyFire)
				{
					Log.Debug($"Attack:{player.Nickname} -> {target.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);
					target.ReferenceHub.falldamage.RpcDoSound();
					target.Hurt(2f, player, DamageTypes.Scp0492);
					if(!player.SessionVariables.ContainsKey("sanya_kickattack_lasttime"))
						player.SessionVariables.Add("sanya_kickattack_lasttime", NetworkTime.time);
					else
						player.SessionVariables["sanya_kickattack_lasttime"] = NetworkTime.time;
				}
			}
		}
	}
}
