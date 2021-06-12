using CustomPlayerEffects;
using HarmonyLib;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Lift), nameof(Lift.CheckMeltPlayer))]
	public static class CheckDecontLiftPatch
	{
		public static bool Prefix(GameObject ply)
		{
			if(!ReferenceHub.TryGetHub(ply, out var referenceHub)
				|| referenceHub.playerMovementSync.RealModelPosition.y >= 200f
				|| referenceHub.playerMovementSync.RealModelPosition.y <= -200f)
				return false;
			referenceHub.playerEffectsController.EnableEffect<Decontaminating>(0f, false);
			return false;
		}
	}
}
