using CustomPlayerEffects;
using HarmonyLib;

namespace SanyaPlugin.Patches.Scp096
{
	[HarmonyPatch(typeof(PlayableScps.Scp096), nameof(PlayableScps.Scp096.Windup))]
	public static class WindupPatch
	{
		public static void Prefix(PlayableScps.Scp096 __instance, bool force)
		{
			if(!force && (__instance.IsPreWindup || !__instance.CanEnrage))
				return;

			__instance.Hub.playerEffectsController.DisableEffect<Amnesia>();
			__instance.Hub.playerEffectsController.DisableEffect<Deafened>();
			__instance.Hub.playerEffectsController.EnableEffect<Invigorated>();
			__instance.Hub.playerEffectsController.EnableEffect<Disabled>(6f);
		}
	}
}
