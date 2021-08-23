using Footprinting;
using HarmonyLib;
using InventorySystem.Items;
using InventorySystem.Items.Armor;
using PlayableScps.Interfaces;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(HitboxIdentity), nameof(HitboxIdentity.Damage))]
	public static class ScpArmorEfficacyPatch
	{
		public static bool Prefix(HitboxIdentity __instance, ref bool __result, float damage, IDamageDealer item, Footprint attackerFootprint, Vector3 exactPos)
		{
			if(attackerFootprint.NetId != __instance.NetworkId)
			{
				Role role = __instance.TargetHub.characterClassManager.Classes.SafeGet(attackerFootprint.Role);
				Role curRole = __instance.TargetHub.characterClassManager.CurRole;
				if(!HitboxIdentity.CheckFriendlyFire(role.roleId, curRole.roleId, false))
				{
					__result = false;
					return false;
				}
				if(Misc.GetFaction(role.team) == Misc.GetFaction(curRole.team))
				{
					damage *= PlayerStats.FriendlyFireFactor;
				}
			}
			HitboxIdentity.DamagePercent damagePercent = item.UseHitboxMultipliers ? __instance._dmgMultiplier : HitboxIdentity.DamagePercent.Body;
			if(item.UseHitboxMultipliers)
			{
				damage *= (float)damagePercent / 100f;
			}
			int bulletPenetrationPercent = Mathf.RoundToInt(item.ArmorPenetration * 100f);
			BodyArmor bodyArmor;
			IArmoredScp armoredScp;
			if(__instance.TargetHub.inventory.TryGetBodyArmor(out bodyArmor))
			{
				if(damagePercent != HitboxIdentity.DamagePercent.Body)
				{
					if(damagePercent == HitboxIdentity.DamagePercent.Headshot)
					{
						damage = BodyArmorUtils.ProcessDamage(bodyArmor.HelmetEfficacy, damage, bulletPenetrationPercent);
					}
				}
				else
				{
					damage = BodyArmorUtils.ProcessDamage(bodyArmor.VestEfficacy, damage, bulletPenetrationPercent);
				}
			}
			else if(SanyaPlugin.Instance.Config.ScpArmorEfficacyParsed.TryGetValue(__instance.TargetHub.characterClassManager.CurClass, out int efficacy))
			{
				damage = BodyArmorUtils.ProcessDamage(efficacy, damage, bulletPenetrationPercent);
			}
			else if((armoredScp = (__instance.TargetHub.scpsController.CurrentScp as IArmoredScp)) != null)
			{
				damage = BodyArmorUtils.ProcessDamage(armoredScp.GetArmorEfficacy(), damage, bulletPenetrationPercent);
			}
			__instance.TargetHub.playerStats.HurtPlayer(new PlayerStats.HitInfo(damage, attackerFootprint.LoggedHubName, item.DamageType, attackerFootprint.PlayerId, false), __instance.TargetHub.gameObject, false, true);
			__result = true;
			return false;
		}
	}
}
