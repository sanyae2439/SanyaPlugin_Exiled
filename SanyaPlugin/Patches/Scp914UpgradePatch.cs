using CustomPlayerEffects;
using Exiled.API.Features;
using HarmonyLib;
using Scp914;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp914Upgrader), nameof(Scp914Upgrader.Upgrade))]
	public static class Scp914UpgradePatch
	{
		public static void Postfix()
		{
			Log.Debug($"[Scp914UpgradePatch]", SanyaPlugin.Instance.Config.IsDebugged);
			var coliders = Physics.OverlapBox(Exiled.API.Features.Scp914.Scp914Controller._outputChamber.position, Exiled.API.Features.Scp914.Scp914Controller.IntakeChamberSize);
			foreach(var colider in coliders)
			{
				if(colider.TryGetComponent(out CharacterClassManager ccm))
				{
					ccm._hub.playerEffectsController.EnableEffect<Disabled>();
					ccm._hub.playerEffectsController.EnableEffect<Poisoned>();
					ccm._hub.playerEffectsController.EnableEffect<Concussed>();
					ccm._hub.playerEffectsController.EnableEffect<Exhausted>();
				}
			}
			return;
		}
	}
}
