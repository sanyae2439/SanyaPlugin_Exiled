using HarmonyLib;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(CustomPlayerEffects.Flashed), nameof(CustomPlayerEffects.Flashed.Flashable))]
	public static class FixFlashablePatch
	{
		public static bool Prefix(CustomPlayerEffects.Flashed __instance, ref bool __result, ReferenceHub throwerPlayerHub, Vector3 sourcePosition, int ignoreMask)
		{
			__result = ((__instance.Hub != throwerPlayerHub && throwerPlayerHub.weaponManager.GetShootPermission(__instance.Hub.characterClassManager.CurRole.team, false)) || SanyaPlugin.Instance.Handlers.FriendlyFlashEnabled)
				&& !Physics.Linecast(sourcePosition, __instance.Hub.PlayerCameraReference.position, ignoreMask);
			return false;
		}
	}
}
