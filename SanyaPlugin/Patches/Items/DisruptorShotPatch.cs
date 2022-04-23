using HarmonyLib;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Modules;
using InventorySystem.Items.Usables.Scp330;
using PlayerStatsSystem;
using UnityEngine;
using Utils.Networking;

namespace SanyaPlugin.Patches.Items
{
	[HarmonyPatch(typeof(DisruptorHitreg), nameof(DisruptorHitreg.ServerPerformShot))]
	public static class DisruptorShotPatch
	{
		public static bool Prefix(DisruptorHitreg __instance, Ray ray) 
		{
			FirearmBaseStats baseStats = __instance.Firearm.BaseStats;
			Vector3 a = (new Vector3(Random.value, Random.value, Random.value) - Vector3.one / 2f).normalized * Random.value;
			float inaccuracy = baseStats.GetInaccuracy(__instance.Firearm, __instance.Firearm.AdsModule.ServerAds, __instance.Hub.playerMovementSync.PlayerVelocity.magnitude, __instance.Hub.playerMovementSync.Grounded);
			ray.direction = Quaternion.Euler(inaccuracy * a) * ray.direction;
			LayerMask mask = LayerMask.GetMask(new string[]
			{
				"Default",
				"Hitbox",
				"CCTV",
				"Door",
				"Locker",
				"Pickup"
			});
			RaycastHit raycastHit;
			if(!Physics.Raycast(ray, out raycastHit, baseStats.MaxDistance(), mask)) return false;
			if(!raycastHit.collider.TryGetComponent(out IDestructible destructible))
			{
				new DisruptorHitreg.DisruptorHitMessage
				{
					Position = raycastHit.point + raycastHit.normal * 0.1f,
					Rotation = new LowPrecisionQuaternion(Quaternion.LookRotation(-raycastHit.normal))
				}.SendToAuthenticated(0);
				new CandyPink.CandyExplosionMessage() 
				{ 
					Origin = raycastHit.point + raycastHit.normal * 0.1f
				}.SendToAuthenticated();
				__instance.CreateExplosion(raycastHit.point);
				return false;
			}
			__instance.RestorePlayerPosition();
			float damage = baseStats.DamageAtDistance(__instance.Firearm, raycastHit.distance);
			if(destructible.Damage(damage, new DisruptorDamageHandler(__instance.Firearm.Footprint, damage), raycastHit.point))
			{
				Hitmarker.SendHitmarker(__instance.Conn, 2f);
				__instance.ShowHitIndicator(destructible.NetworkId, damage, ray.origin);
			}
			new CandyPink.CandyExplosionMessage()
			{
				Origin = raycastHit.point + raycastHit.normal * 0.1f
			}.SendToAuthenticated();
			__instance.CreateExplosion(raycastHit.point);
			return false;
		}
	}
}
