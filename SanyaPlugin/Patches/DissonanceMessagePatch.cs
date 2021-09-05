using Dissonance.Integrations.MirrorIgnorance;
using Dissonance.Networking;
using HarmonyLib;
using Mirror;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(MirrorIgnoranceServer), nameof(MirrorIgnoranceServer.OnMessageReceived))]
	public static class DissonanceMessagePatch
	{
		public static bool Prefix(MirrorIgnoranceServer __instance, NetworkConnection source, DissonanceNetworkMessage msg)
		{
			PacketReader newreader = new PacketReader(msg.Data);
			if(newreader.ReadPacketHeader(out var messageTypes) && (messageTypes == MessageTypes.ServerRelayReliable || messageTypes == MessageTypes.ServerRelayUnreliable))
			{
				if(!EventHandlers.connIdToUserIds.TryGetValue(source.connectionId, out var userid)) return true;
				if(SanyaPlugin.Instance.Config.DisableChatBypassWhitelist && WhiteList.Users != null && WhiteList.IsOnWhitelist(userid)) return true;
				if(SanyaPlugin.Instance.Config.DisableAllChat) return false;
			}
			return true;
		}
	}
}
