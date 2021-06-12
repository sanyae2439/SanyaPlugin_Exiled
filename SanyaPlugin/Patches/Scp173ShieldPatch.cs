using System.Collections.Generic;
using Exiled.API.Features;
using HarmonyLib;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Scp173PlayerScript), nameof(Scp173PlayerScript.FixedUpdate))]
	public static class Scp173ShieldPatch
	{
		private static readonly HashSet<Player> seeingHumans = new HashSet<Player>();

		public static void Postfix(Scp173PlayerScript __instance)
		{
			if(SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount <= 0 || !__instance.iAm173) return;

			foreach(var ply in Player.List)
			{
				if(!ply.ReferenceHub.characterClassManager.Scp173.SameClass
					&& ply.ReferenceHub.characterClassManager.Scp173.LookFor173(__instance.gameObject, true)
					&& __instance.LookFor173(ply.GameObject, false))
				{
					if(!seeingHumans.Contains(ply))
					{
						Log.Debug($"[Scp173ShieldPatch:Add] {ply.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);
						seeingHumans.Add(ply);
						__instance._ps.NetworkmaxArtificialHealth += SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount;
						__instance._ps.unsyncedArtificialHealth = Mathf.Clamp(__instance._ps.unsyncedArtificialHealth + SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount, 0, __instance._ps.NetworkmaxArtificialHealth);
					}
				}
				else if(seeingHumans.Contains(ply))
				{
					Log.Debug($"[Scp173ShieldPatch:Remove] {ply.Nickname}", SanyaPlugin.Instance.Config.IsDebugged);
					seeingHumans.Remove(ply);
					__instance._ps.NetworkmaxArtificialHealth = Mathf.Clamp(__instance._ps.NetworkmaxArtificialHealth - SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount, 0, __instance._ps.NetworkmaxArtificialHealth - SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount);
					__instance._ps.unsyncedArtificialHealth = Mathf.Clamp(__instance._ps.unsyncedArtificialHealth - SanyaPlugin.Instance.Config.Scp173SeeingByHumansAhpAmount, 0, __instance._ps.NetworkmaxArtificialHealth);
				}
			}
		}
	}
}
