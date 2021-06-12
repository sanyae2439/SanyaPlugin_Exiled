using Grenades;
using HarmonyLib;
using Interactables.Interobjects;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(FragGrenade), nameof(FragGrenade.ServersideExplosion))]
	public static class FragGrenadePryGatePatch
	{
		public static void Prefix(FragGrenade __instance)
		{
			foreach(Collider collider in Physics.OverlapSphere(__instance.transform.position, __instance.chainTriggerRadius, __instance.damageLayerMask))
			{
				PryableDoor componentInParent = collider.GetComponentInParent<PryableDoor>();
				if(componentInParent != null && !componentInParent.NetworkTargetState)
					componentInParent.TryPryGate();
			}
		}
	}
}
