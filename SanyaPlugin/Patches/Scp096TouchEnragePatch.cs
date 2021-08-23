using System.Collections.Generic;
using HarmonyLib;
using PlayableScps;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp096), nameof(Scp096.UpdateVision))]
	public static class Scp096TouchEnragePatch
	{
		[HarmonyPriority(Priority.HigherThanNormal)]
		public static bool Prefix(Scp096 __instance)
		{
			if(__instance._flash.IsEnabled) return false;

			Vector3 vector = __instance.Hub.transform.TransformPoint(Scp096._headOffset);
			foreach(KeyValuePair<GameObject, ReferenceHub> keyValuePair in ReferenceHub.GetAllHubs())
			{
				ReferenceHub value = keyValuePair.Value;
				CharacterClassManager characterClassManager = value.characterClassManager;
				if(characterClassManager.CurClass != RoleType.Spectator
					&& !value.isDedicatedServer
					&& !(value == __instance.Hub) 
					&& !characterClassManager.IsAnyScp() 
					&& Vector3.Dot((value.PlayerCameraReference.position - vector).normalized, __instance.Hub.PlayerCameraReference.forward) >= 0.1f)
				{
					VisionInformation visionInformation = VisionInformation.GetVisionInformation(value, vector, -0.1f, 60f, true, true, __instance.Hub.localCurrentRoomEffects, 0);
					bool toEnrage = visionInformation.IsLooking;

					//Add touch check
					if(!toEnrage && SanyaPlugin.Instance.Config.Scp096TouchEnrageDistance > visionInformation.Distance)
					{
						toEnrage = !Physics.Linecast(value.PlayerCameraReference.position, vector, VisionInformation.VisionLayerMask);
					}

					if(toEnrage)
					{
						float delay = visionInformation.LookingAmount / 0.25f * (visionInformation.Distance * 0.1f);
						if(!__instance.Calming) __instance.AddTarget(value.gameObject);
						if(__instance.CanEnrage && value.gameObject != null) __instance.PreWindup(delay);
					}
				}
			}

			return false;
		}
	}
}
