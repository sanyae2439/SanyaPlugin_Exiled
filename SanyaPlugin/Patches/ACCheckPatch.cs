using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(PlayerMovementSync), nameof(PlayerMovementSync.ForceLastSafePosition))]
	public static class AntiCheatCheckPatch
	{
		public static bool Prefix(PlayerMovementSync __instance)
		{
			if(__instance._hub.characterClassManager.CurClass == RoleType.Scp173
				&& Scp173PlayerScript._blinkTimeRemaining > 0f)
			{
				__instance.RealModelPosition = __instance._receivedPosition;
				__instance._lastSafePosition = __instance._receivedPosition;
				__instance._lastSafePosition2 = __instance._receivedPosition;
				__instance._lastSafePosition3 = __instance._receivedPosition;
				__instance._lastSafePositionDistance = 0f;
				return false;
			}
			return true;
		}
	}
}
