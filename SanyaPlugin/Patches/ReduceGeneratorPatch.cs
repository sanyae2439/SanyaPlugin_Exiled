using HarmonyLib;
using MapGeneration.Distributors;
using Mirror;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp079Generator), nameof(Scp079Generator.ServerUpdate))]
	public static class ReduceGeneratorPatch
	{
		public static bool Prefix(Scp079Generator __instance)
		{
			if(!NetworkServer.active) 
				return false;

			bool flag = __instance._currentTime >= __instance._totalActivationTime;
			if(!flag)
			{
				int num = Mathf.FloorToInt(__instance._totalActivationTime - __instance._currentTime);
				if(num != (int)__instance._syncTime)
					__instance.Network_syncTime = (short)num;
			}

			if(__instance.ActivationReady)
			{
				if(flag && !__instance.Engaged)
				{
					__instance.Engaged = true;
					__instance.Activating = false;
					return false;
				}
				__instance._currentTime += Time.deltaTime;
			}
			else if(__instance._currentTime == 0f || flag)
				return false;

			__instance._currentTime = Mathf.Clamp(__instance._currentTime, 0f, __instance._totalActivationTime);
			return false;
		}
	}
}
