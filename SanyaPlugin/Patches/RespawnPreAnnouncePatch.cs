using HarmonyLib;
using MEC;
using Respawning;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(RespawnEffectsController), nameof(RespawnEffectsController.ExecuteAllEffects))]
	public static class RespawnPreAnnouncePatch
	{
		public static void Postfix(RespawnEffectsController.EffectType type)
		{
			if(type == RespawnEffectsController.EffectType.Selection)
				SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.PreAnnounce(), Segment.FixedUpdate));
		}
	}
}
