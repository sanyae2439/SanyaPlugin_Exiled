using HarmonyLib;
using SanyaPlugin.Functions;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Lift), nameof(Lift.MovePlayers))]
	public static class LiftMovingSinkholePatch
	{
		public static void Postfix(Lift __instance, Transform target)
		{
			if(SanyaPlugin.Instance.Handlers.Sinkhole != null
				&& __instance.InRange(SanyaPlugin.Instance.Handlers.Sinkhole.transform.position, out var gameObject, 1f, 2f, 1f)
				&& gameObject.transform != target)
				Methods.MoveNetworkIdentityObject(SanyaPlugin.Instance.Handlers.Sinkhole, target.TransformPoint(gameObject.transform.InverseTransformPoint(SanyaPlugin.Instance.Handlers.Sinkhole.transform.position)));
		}
	}
}
