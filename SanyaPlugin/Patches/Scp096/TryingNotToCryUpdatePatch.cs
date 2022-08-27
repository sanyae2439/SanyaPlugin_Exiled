using HarmonyLib;
using UnityEngine;

namespace SanyaPlugin.Patches.Scp096
{
	[HarmonyPatch(typeof(PlayableScps.Scp096), nameof(PlayableScps.Scp096.UpdateTryNotToCry))]
	public static class TryingNotToCryUpdatePatch
	{
		public static bool Prefix(PlayableScps.Scp096 __instance)
		{
			if(!SanyaPlugin.Instance.Config.Scp096Rework) return true;

			if(!__instance.TryingNotToCry)
				return false;

			if(!Physics.Raycast(__instance.Hub.PlayerCameraReference.position, __instance.Hub.PlayerCameraReference.forward, out var raycastHit, 1f, LayerMask.GetMask("Door", "Default")))
				__instance.ResetState();

			return false;
		}
	}
}
