using System.Linq;
using HarmonyLib;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Firearms.Attachments.Components;
using InventorySystem.Items.Firearms.Modules;
using UnityEngine;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(StandardHitregBase), nameof(StandardHitregBase.ShowHitIndicator))]
	public static class PreventSuppressorIndicatorPatch
	{
		public static bool Prefix(StandardHitregBase __instance, uint netId, ref Vector3 origin)
		{
			if(!ReferenceHub.TryGetHubNetID(netId, out var hub))
				return false;

			Attachment supp = __instance.Firearm.Attachments.FirstOrDefault(x => x.Name == AttachmentName.SoundSuppressor);
			bool isSuppressor = false;
			if(supp != null)
				isSuppressor = supp.IsEnabled;

			return !isSuppressor;
		}
	}
}
