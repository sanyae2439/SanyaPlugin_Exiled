using Grenades;
using HarmonyLib;
using Mirror;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Grenade), nameof(Grenade.OnCollisionEnter))]
	public static class FlashGrenadeFusePatch
	{
		public static void Prefix(Grenade __instance)
		{
			if(!SanyaPlugin.Instance.Config.FlashbangFuseWithCollision) return;
			if(__instance is FlashGrenade flashGrenade && flashGrenade.DisableGameObject)
			{
				__instance.NetworkfuseTime = NetworkTime.time + 1.0;
				flashGrenade.DisableGameObject = false;
			}
		}
	}
}
