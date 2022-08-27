using HarmonyLib;
using Interactables.Interobjects.DoorUtils;
using PlayableScps;
using UnityEngine;

namespace SanyaPlugin.Patches.Scp096
{
	[HarmonyPatch(typeof(PlayableScps.Scp096), nameof(PlayableScps.Scp096.TryNotToCry))]
	public static class TryingNotToCryPatch
	{
		public static bool Prefix(PlayableScps.Scp096 __instance)
		{
			if(!SanyaPlugin.Instance.Config.Scp096Rework) return true;

			if(!Physics.Raycast(__instance.Hub.PlayerCameraReference.position, __instance.Hub.PlayerCameraReference.forward, out var raycastHit, 1f, LayerMask.GetMask("Door", "Default")))
				return false;

			__instance.PlayerState = Scp096PlayerState.TryNotToCry;

			DoorVariant componentInParent = raycastHit.collider.gameObject.GetComponentInParent<DoorVariant>();
			if(componentInParent != null && componentInParent.GetExactState() <= 0f && !PlayableScps.Scp096._takenDoors.ContainsKey(__instance.Hub))
				PlayableScps.Scp096._takenDoors.Add(__instance.Hub, componentInParent);

			return false;
		}
	}
}
