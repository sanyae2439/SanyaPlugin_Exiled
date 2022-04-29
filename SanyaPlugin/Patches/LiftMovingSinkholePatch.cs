using HarmonyLib;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Lift), nameof(Lift.MovePlayers))]
	public static class LiftMovingSinkholePatch
	{
		public static void Postfix(Lift __instance, Transform target)
		{
			foreach(var sinkhole in SanyaPlugin.Instance.Handlers.Sinkholes)
				if(__instance.InRange(sinkhole.transform.position, out var gameObject, 1f, 2f, 1f) && gameObject.transform != target)
					Methods.MoveNetworkIdentityObject(sinkhole.netIdentity, target.TransformPoint(gameObject.transform.InverseTransformPoint(sinkhole.transform.position)));
		}
	}
}
