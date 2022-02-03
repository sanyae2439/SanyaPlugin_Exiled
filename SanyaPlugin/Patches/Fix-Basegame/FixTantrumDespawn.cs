using HarmonyLib;
using MEC;
using Mirror;

namespace SanyaPlugin.Patches.Fix_Basegame
{
	[HarmonyPatch(typeof(TantrumAnimation), nameof(TantrumAnimation.UserCode_RpcDespawn))]
	public static class FixTantrumDespawn
	{
		public static void Postfix(TantrumAnimation __instance) => Timing.RunCoroutine(__instance.ServerRemovePuddle(), Segment.FixedUpdate);
	}
}
