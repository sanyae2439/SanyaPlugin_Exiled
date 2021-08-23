using System.Collections.Generic;
using HarmonyLib;
using InventorySystem.Items;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Attachments;
using Mirror;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(AttachmentsServerHandler), nameof(AttachmentsServerHandler.SetupProvidedWeapon))]
	public static class FixWeaponStatusDesync
	{
		public static bool Prefix(ReferenceHub ply, ItemBase item)
		{
			Firearm firearm;
			if(!NetworkServer.active || (firearm = (item as Firearm)) == null)
				return false;

			Dictionary<ItemType, uint> dictionary;
			uint num;
			if(!AttachmentsServerHandler.PlayerPreferences.TryGetValue(ply, out dictionary) || !dictionary.TryGetValue(item.ItemTypeId, out num))
			{
				num = 0U;
			}
			num = firearm.ValidateAttachmentsCode(num);
			firearm.ApplyAttachmentsCode(num, false);
			ushort num2;
			if(ply.inventory.UserInventory.ReserveAmmo.TryGetValue(firearm.AmmoType, out num2))
			{
				int num3 = Mathf.Min((int)num2, (int)firearm.AmmoManagerModule.MaxAmmo);
				ply.inventory.UserInventory.ReserveAmmo[firearm.AmmoType] = (ushort)((int)num2 - num3);
				bool flag = firearm.CombinedAttachments.AdditionalPros.HasFlagFast(AttachmentDescriptiveAdvantages.Flashlight);
				firearm.Status = new FirearmStatus((byte)num3, flag ? (FirearmStatusFlags.MagazineInserted | FirearmStatusFlags.FlashlightEnabled) : FirearmStatusFlags.MagazineInserted, num);
			}
			else
			{
				bool flag = firearm.CombinedAttachments.AdditionalPros.HasFlagFast(AttachmentDescriptiveAdvantages.Flashlight);
				firearm.Status = new FirearmStatus(0, flag ? (FirearmStatusFlags.MagazineInserted | FirearmStatusFlags.FlashlightEnabled) : FirearmStatusFlags.MagazineInserted, num);
			}
			return true;
		}
	}
}
