using HarmonyLib;
using MEC;
using Respawning;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(RespawnEffectsController), nameof(RespawnEffectsController.ExecuteAllEffects))]
	public static class RespawnPreAnnouncePatch
	{
		public static void Postfix(RespawnEffectsController.EffectType type, SpawnableTeamType team)
		{
			if(type == RespawnEffectsController.EffectType.Selection)
				SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.RunCoroutine(Coroutines.PreAnnounce(team), Segment.FixedUpdate));
		}
	}
}
