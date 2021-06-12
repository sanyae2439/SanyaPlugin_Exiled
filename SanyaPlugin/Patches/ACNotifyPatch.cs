using Exiled.API.Features;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(PlayerMovementSync), nameof(PlayerMovementSync.AntiCheatKillPlayer))]
	public static class ACNotifyPatch
	{
		public static void Prefix(PlayerMovementSync __instance, string message, string code) => Log.Warn($"[SanyaPlugin] AntiCheatKill Detect:{Player.Get(__instance._hub)?.Nickname} [{message}({code})]");
	}
}
