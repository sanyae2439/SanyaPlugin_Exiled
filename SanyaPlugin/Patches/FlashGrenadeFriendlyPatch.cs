using System;
using CustomPlayerEffects;
using Exiled.API.Features;
using Grenades;
using HarmonyLib;
using SanyaPlugin.Functions;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(FlashGrenade), nameof(FlashGrenade.ServersideExplosion))]
	public static class FlashGrenadeFriendlyPatch
	{
		public static bool Prefix(FlashGrenade __instance, ref bool __result)
		{
			foreach(GameObject gameObject in PlayerManager.players)
			{
				Vector3 position = __instance.transform.position;
				Player thrower = Player.Get(__instance.thrower.gameObject);
				Player target = Player.Get(gameObject);
				ReferenceHub hub = ReferenceHub.GetHub(gameObject);
				Flashed effect = hub.playerEffectsController.GetEffect<Flashed>();
				if(effect != null && !(__instance.thrower == null) && effect.Flashable(ReferenceHub.GetHub(__instance.thrower.gameObject), position, __instance._ignoredLayers))
				{
					float num = __instance.powerOverDistance.Evaluate(Vector3.Distance(gameObject.transform.position, position) / ((position.y > 900f)
						? __instance.distanceMultiplierSurface
						: __instance.distanceMultiplierFacility)) * __instance.powerOverDot.Evaluate(Vector3.Dot(hub.PlayerCameraReference.forward, (hub.PlayerCameraReference.position - position).normalized));
					byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(num * 10f * __instance.maximumDuration), 1, 255);
					if(b >= effect.Intensity && num > 0f)
					{
						if(target != thrower && !thrower.IsEnemy(target.Team) && target.GameObject.TryGetComponent<SanyaPluginComponent>(out var comp))
						{
							comp.AddHudBottomText($"<color=#ffff00><size=25>味方の{thrower.Nickname}よりダメージを受けました[FlashGrenade]</size></color>", 5);
							thrower.ReferenceHub.GetComponent<SanyaPluginComponent>()?.AddHudBottomText($"味方の<color=#ff0000><size=25>{comp.player.Nickname}へダメージを与えました[FlashGrenade]</size></color>", 5);
						}

						if(hub.characterClassManager.IsAnyScp())
							hub.playerEffectsController.ChangeEffectIntensity<Flashed>(1);

						num *= 2f;
						hub.playerEffectsController.EnableEffect<Amnesia>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Deafened>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Blinded>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Concussed>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Panic>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Exhausted>(num * __instance.maximumDuration, true);
						hub.playerEffectsController.EnableEffect<Disabled>(num * __instance.maximumDuration, true);
					}
				}
			}
			__result = ((Func<bool>)Activator.CreateInstance(typeof(Func<bool>), __instance, typeof(EffectGrenade).GetMethod(nameof(EffectGrenade.ServersideExplosion)).MethodHandle.GetFunctionPointer()))();
			return false;
		}
	}
}
