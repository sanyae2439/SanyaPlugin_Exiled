using System.Collections.Generic;
using Exiled.API.Features;
using HarmonyLib;
using SanyaPlugin.Functions;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp079PlayerScript), nameof(Scp079PlayerScript.CallCmdSwitchCamera))]
	public static class Scp079CameraPatch
	{
		[HarmonyPriority(Priority.HigherThanNormal)]
		public static bool Prefix(Scp079PlayerScript __instance, ref ushort cameraId, bool lookatRotation)
		{
			if(!SanyaPlugin.Instance.Config.Scp079ExtendEnabled) return true;

			Log.Debug($"[Scp079CameraPatch] {cameraId}:{lookatRotation}", SanyaPlugin.Instance.Config.IsDebugged);

			if(__instance.GetComponent<AnimationController>().curAnim != 1) return true;

			if(SanyaPlugin.Instance.Config.Scp079ExtendLevelFindscp > 0 && __instance.curLvl + 1 >= SanyaPlugin.Instance.Config.Scp079ExtendLevelFindscp)
			{
				List<Camera079> cams = new List<Camera079>();
				foreach(var ply in Player.List)
					if(ply.Team == Team.SCP && ply.Role != RoleType.Scp079)
						cams.AddRange(ply.GetNearCams());

				Camera079 target;
				if(cams.Count > 0)
					target = cams.GetRandomOne();
				else return true;

				if(target != null)
				{
					if(SanyaPlugin.Instance.Config.Scp079ExtendCostFindscp > __instance.curMana)
					{
						__instance.RpcNotEnoughMana(SanyaPlugin.Instance.Config.Scp079ExtendCostFindscp, __instance.curMana);
						return false;
					}

					__instance.RpcSwitchCamera(target.cameraId, lookatRotation);
					__instance.Mana -= SanyaPlugin.Instance.Config.Scp079ExtendCostFindscp;
					__instance.currentCamera = target;
					return false;
				}
			}
			return true;
		}
	}
}
