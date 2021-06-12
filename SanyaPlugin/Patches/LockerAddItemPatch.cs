using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using HarmonyLib;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(LockerManager), nameof(LockerManager.Generate))]
	public static class LockerAddItemPatch
	{
		public static void Prefix(LockerManager __instance)
		{
			if(!SanyaPlugin.Instance.Config.SpawnAddItems) return;

			Log.Debug($"Item adding...", SanyaPlugin.Instance.Config.IsDebugged);
			var list = new List<SpawnableItem>(__instance.items);

			foreach(var i in list.Where(x => x.itemTag == "medkit"))
				i.inventoryId = ItemType.Flashlight;

			var glocker556_1 = list.First(x => x.itemTag == "glocker556" && x.inventoryId == ItemType.Ammo556);
			glocker556_1.inventoryId = ItemType.Radio;

			var glocker556_2 = list.First(x => x.itemTag == "glocker556" && x.inventoryId == ItemType.Ammo556);
			glocker556_2.inventoryId = ItemType.KeycardNTFLieutenant;

			var glocker556_3 = list.First(x => x.itemTag == "glocker556" && x.inventoryId == ItemType.Ammo556);
			glocker556_3.inventoryId = ItemType.Adrenaline;

			var glocker556_4 = list.First(x => x.itemTag == "glocker556" && x.inventoryId == ItemType.Ammo556);
			glocker556_4.inventoryId = ItemType.GrenadeFrag;

			var glocker_b_small_1 = list.First(x => x.itemTag == "glocker-b-small" && x.inventoryId == ItemType.Ammo556);
			glocker_b_small_1.inventoryId = ItemType.Radio;
			glocker_b_small_1.chanceOfSpawn = 100;
			glocker_b_small_1.copies = 0;

			var misclocker_1 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Ammo762);
			misclocker_1.inventoryId = ItemType.GunCOM15;
			misclocker_1.copies = 0;

			var misclocker_2 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Ammo556);
			misclocker_2.inventoryId = ItemType.Coin;
			misclocker_2.copies = 3;

			var misclocker_3 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Ammo9mm);
			misclocker_3.inventoryId = ItemType.Radio;
			misclocker_3.copies = 0;

			var misclocker_4 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.KeycardScientist);
			misclocker_4.inventoryId = ItemType.KeycardGuard;
			misclocker_4.copies = 0;

			var misclocker_5 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.KeycardScientist);
			misclocker_5.inventoryId = ItemType.KeycardSeniorGuard;
			misclocker_5.copies = 0;
			misclocker_5.chanceOfSpawn = 50;

			var misclocker_6 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.KeycardZoneManager);
			misclocker_6.inventoryId = ItemType.KeycardScientistMajor;
			misclocker_6.copies = 0;

			var misclocker_7 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Painkillers);
			misclocker_7.inventoryId = ItemType.SCP500;
			misclocker_7.copies = 0;
			misclocker_7.chanceOfSpawn = 50;

			var misclocker_8 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Flashlight);
			misclocker_8.inventoryId = ItemType.GrenadeFlash;
			misclocker_8.copies = 0;

			var misclocker_9 = list.First(x => x.itemTag == "misclocker" && x.inventoryId == ItemType.Flashlight);
			misclocker_9.inventoryId = ItemType.SCP207;
			misclocker_9.copies = 0;
			misclocker_9.chanceOfSpawn = 50;

			__instance.items = list.ToArray();
			Log.Debug($"Item add completed.", SanyaPlugin.Instance.Config.IsDebugged);
		}
	}
}
