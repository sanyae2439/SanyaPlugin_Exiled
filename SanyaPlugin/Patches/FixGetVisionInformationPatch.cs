using HarmonyLib;
using PlayableScps;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(PlayableScps.VisionInformation), nameof(PlayableScps.VisionInformation.GetVisionInformation))]
	public static class FixGetVisionInformationPatch
	{
		public static bool Prefix(
			ref VisionInformation __result,
			ReferenceHub source, 
			Vector3 target, float 
			targetRadius = 0f, 
			float visionTriggerDistance = 0f, 
			bool checkFog = true,
			bool checkLineOfSight = true,
			LocalCurrentRoomEffects targetLightCheck = null, 
			int MaskLayer = 0)
		{
			Transform playerCameraReference = source.PlayerCameraReference;
			bool isOnSameFloor = false;
			bool isLooking = false;
			if(Mathf.Abs(target.y - playerCameraReference.position.y) < 100f)
			{
				isOnSameFloor = true;
				isLooking = true;
			}
			bool IsInDistance = visionTriggerDistance == 0f;
			float num = 0f;
			Vector3 vector = target - playerCameraReference.position;
			if(isLooking && visionTriggerDistance > 0f)
			{
				float num2 = checkFog ? ((target.y > 980f) ? visionTriggerDistance : (visionTriggerDistance / 2f)) : visionTriggerDistance;
				num = vector.magnitude;
				if(num <= num2)
				{
					IsInDistance = true;
				}
				isLooking = IsInDistance;
			}
			float lookingAmount = 1f;
			if(isLooking)
			{
				isLooking = false;
				if(num < Mathf.Abs(targetRadius))
				{
					if(Vector3.Dot(source.transform.forward, (target - source.transform.position).normalized) > 0f)
					{
						isLooking = true;
						lookingAmount = 1f;
					}
				}
				else
				{
					Vector3 vector2 = playerCameraReference.InverseTransformPoint(target);
					if(targetRadius != 0f)
					{
						Vector2 vector3 = vector2.normalized * targetRadius;
						vector2 = new Vector3(vector2.x + vector3.x, vector2.y + vector3.y, vector2.z);
					}
					AspectRatioSync aspectRatioSync = source.aspectRatioSync;
					float num3 = Vector2.Angle(Vector2.up, new Vector2(vector2.x, vector2.z));
					if(num3 < aspectRatioSync.XScreenEdge)
					{
						float num4 = Vector2.Angle(Vector2.up, new Vector2(vector2.y, vector2.z));
						if(num4 < AspectRatioSync.YScreenEdge)
						{
							lookingAmount = (num3 + num4) / aspectRatioSync.XplusY;
							isLooking = true;
						}
					}
				}
			}
			bool IsInLineOfSight = !checkLineOfSight;
			if(isLooking && checkLineOfSight)
			{
				if(MaskLayer == 0)
				{
					MaskLayer = VisionInformation.VisionLayerMask;
				}
				IsInLineOfSight = (Physics.RaycastNonAlloc(new Ray(playerCameraReference.position, vector.normalized), VisionInformation._raycastResult, IsInDistance ? num : vector.magnitude, MaskLayer) == 0);
				isLooking = IsInLineOfSight;
			}
			bool IsInDarkness = targetLightCheck == null;
			if(isLooking && !IsInDarkness)
			{
				IsInDarkness = targetLightCheck.syncFlicker;
				if(IsInDarkness)
				{
					IsInDarkness = !VisionInformation.CheckAttachments(source);
				}
				isLooking = !IsInDarkness;
			}

			__result = new VisionInformation(source, target, isLooking, isOnSameFloor, lookingAmount, num, IsInLineOfSight, IsInDarkness, IsInDistance);
			return false;
		}
	}
}
