using CustomPlayerEffects;
using HarmonyLib;
using MEC;
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
					__instance.Windup(false);			
			}
			else if(__instance.Enraging)
			{
				__instance._enrageWindupRemaining -= Time.deltaTime;
				if(__instance._enrageWindupRemaining <= 0f)
					__instance.Enrage();
			}
			else if(__instance.Enraged)
			{
				__instance.EnrageTimeLeft -= Time.deltaTime;
				__instance.Hub.characterClassManager.netIdentity.connectionToClient.Send(new Scp096ToSelfMessage(__instance.EnrageTimeLeft, __instance._chargeCooldown), 0);
				if(__instance.EnrageTimeLeft <= 0f && !__instance.PryingGate)
				{
					__instance.RemainingEnrageCooldown = 6f;
					__instance.Hub.playerEffectsController.DisableEffect<Invigorated>();
					__instance.Hub.fpc.NetworkforceStopInputs = true;
					SanyaPlugin.Instance.Handlers.roundCoroutines.Add(Timing.CallDelayed(6f, Segment.FixedUpdate, () => __instance.Hub.fpc.NetworkforceStopInputs = false));
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
