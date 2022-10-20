using HarmonyLib;
using InventorySystem.Items.Usables.Scp244;
using Mirror;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp244DeployablePickup), nameof(Scp244DeployablePickup.UpdateRange))]
	public static class Limit244RangePatch
	{
		public static bool Prefix(Scp244DeployablePickup __instance)
		{
			if(__instance.ModelDestroyed && __instance._visibleModel.activeSelf)
			{
				__instance.Rb.constraints = RigidbodyConstraints.FreezeAll;
				__instance._visibleModel.SetActive(false);
			}
			if(__instance.State == Scp244State.Idle && Vector3.Dot(__instance.transform.up, Vector3.up) < __instance._activationDot)
			{
				__instance.State = Scp244State.Active;
				__instance._lifeTime.Restart();
			}
			float num = (__instance.State == Scp244State.Active) ? __instance.TimeToGrow : (-__instance._timeToDecay);
			float curpercent = Mathf.Clamp01(__instance.CurrentSizePercent + Time.deltaTime / num);
			if(__instance.MaxDiameter != 25f)
				curpercent = Mathf.Clamp(curpercent, 0f, 0.33f);
			__instance.CurrentSizePercent = curpercent;
			__instance.Network_syncSizePercent = (byte)Mathf.RoundToInt(__instance.CurrentSizePercent * 255f);
			if(!__instance.ModelDestroyed || __instance.CurrentSizePercent > 0f)
				return false;
			__instance._timeToDecay -= Time.deltaTime;
			if(__instance._timeToDecay <= 0f)
				NetworkServer.Destroy(__instance.gameObject);
			return false;
		}
	}
}
