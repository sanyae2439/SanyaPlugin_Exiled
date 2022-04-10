using System.Linq;
using CustomPlayerEffects;
using HarmonyLib;
using PlayableScps.Messages;
using UnityEngine;

namespace SanyaPlugin.Patches.Scp096
{
	[HarmonyPatch(typeof(PlayableScps.Scp096), nameof(PlayableScps.Scp096.UpdateEnrage))]
	public static class ReworkEnragePatch
	{
		public static bool Prefix(PlayableScps.Scp096 __instance)
		{
			if(!SanyaPlugin.Instance.Config.Scp096Rework) return true;

			if(__instance.IsPreWindup)
			{
				__instance._preWindupTime -= Time.deltaTime;
				if(__instance._preWindupTime <= 0f)
				{
					__instance.Hub.playerEffectsController.DisableEffect<Amnesia>();
					__instance.Hub.playerEffectsController.EnableEffect<Invigorated>();
					__instance.Hub.playerEffectsController.EnableEffect<Ensnared>(6f);
					__instance.Windup(false);
				}				
			}
			else if(__instance.Enraging)
			{
				__instance._enrageWindupRemaining -= Time.deltaTime;
				if(__instance._enrageWindupRemaining <= 0f)
					__instance.Enrage();
			}
			else if(__instance.Enraged)
			{
				//__instance.EnrageTimeLeft -= Time.deltaTime;
				__instance.Hub.characterClassManager.netIdentity.connectionToClient.Send(new Scp096ToSelfMessage(__instance.EnrageTimeLeft, __instance._chargeCooldown), 0);

				var sortedTargetDistance = __instance._targets.Select(x => Vector3.Distance(__instance.Hub.playerMovementSync.RealModelPosition, x.playerMovementSync.RealModelPosition)).OrderBy(x => x);

				//__instance.EnrageTimeLeft <= 0f
				if((__instance._targets.Count == 0 || sortedTargetDistance.FirstOrDefault() > 200f) && !__instance.PryingGate)
				{
					//__instance.RemainingEnrageCooldown = 6f;
					__instance.Hub.playerEffectsController.DisableEffect<Invigorated>();
					__instance.Hub.playerEffectsController.EnableEffect<Amnesia>();
					__instance.Hub.playerEffectsController.EnableEffect<Ensnared>(6f);
					__instance.EndEnrage();
				}

				if(__instance._attacking)
				{
					__instance._attackDuration -= Time.deltaTime;
					if(__instance._attackDuration <= 0f)
						__instance.EndAttack();
				}
				else if(!__instance.CanAttack)
					__instance._attackCooldown -= Time.deltaTime;
			}
			else if(__instance.Calming)
			{
				__instance._calmingTime -= Time.deltaTime;

				if(__instance._calmingTime <= 0f)
					__instance.ResetEnrage();
			}
			else if(__instance.RemainingEnrageCooldown > 0f)
			{
				__instance.RemainingEnrageCooldown -= Time.deltaTime;
			}
			if(__instance.Charging)
			{
				__instance._chargeTimeRemaining -= Time.deltaTime;
				if(__instance._chargeTimeRemaining <= 0f)
					__instance.EndCharge();
			}
			else if(__instance._chargeCooldown > 0f)
				__instance._chargeCooldown -= Time.deltaTime;

			return false;
		}
	}
}
