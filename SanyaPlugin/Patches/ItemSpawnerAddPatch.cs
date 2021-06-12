using System.Linq;
using Exiled.API.Features;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(HostItemSpawner), nameof(HostItemSpawner.Spawn))]
	public static class ItemSpawnerAddPatch
	{
		public static void Prefix()
		{
			if(!SanyaPlugin.Instance.Config.SpawnAddItems) return;
			Log.Debug($"SpawnerItem adding...", SanyaPlugin.Instance.Config.IsDebugged);
			var list = RandomItemSpawner.singleton.pickups;

			var lcarmory_ammo_762 = list.First(x => x.posID == "LC_Armory_Ammo" && x.itemID == ItemType.Ammo762);
			lcarmory_ammo_762.itemID = ItemType.Radio;

			var lcarmory_ammo_9mm = list.First(x => x.posID == "LC_Armory_Ammo" && x.itemID == ItemType.Ammo9mm);
			lcarmory_ammo_9mm.itemID = ItemType.Disarmer;

			var lcarmory_com15 = list.First(x => x.posID == "LC_Armory_Pistol" && x.itemID == ItemType.GunCOM15);
			lcarmory_com15.itemID = ItemType.Medkit;

			var lcarmory_mp7 = list.First(x => x.posID == "LC_Armory" && x.itemID == ItemType.GunMP7);
			lcarmory_mp7.itemID = ItemType.GunLogicer;

			var room012_keycard = list.First(x => x.posID == "012_mScientist_keycard" && x.itemID == ItemType.KeycardZoneManager);
			room012_keycard.itemID = ItemType.KeycardScientistMajor;

			var cafe = list.First(x => x.posID == "Cafe_Scientist_keycard" && x.itemID == ItemType.KeycardScientist);
			cafe.itemID = ItemType.KeycardScientistMajor;

			var servers = list.First(x => x.posID == "Servers" && x.itemID == ItemType.KeycardScientist);
			servers.itemID = ItemType.KeycardSeniorGuard;

			var nuke = list.First(x => x.posID == "Nuke" && x.itemID == ItemType.KeycardGuard);
			nuke.itemID = (ItemType)UnityEngine.Random.Range((int)ItemType.KeycardJanitor, (int)ItemType.KeycardO5 + 1);

			RandomItemSpawner.singleton.pickups = list.ToArray();
			Log.Debug($"SpawnerItem added.", SanyaPlugin.Instance.Config.IsDebugged);
		}
	}
}
