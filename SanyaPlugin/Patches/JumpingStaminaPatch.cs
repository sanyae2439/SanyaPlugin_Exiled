using Exiled.API.Features;
using HarmonyLib;
using Mirror;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(PlayerMovementSync), nameof(PlayerMovementSync.ReceivePosition2DJump))]
	public static class JumpingStaminaPatch
	{
		public static void Postfix(NetworkConnection connection)
		{
			if(CustomLiteNetLib4MirrorTransport.DelayConnections) return;
			var player = Player.Get(connection.identity.gameObject);

			//ジャンプ時スタミナ消費
			if(SanyaPlugin.Instance.Config.StaminaCostJump > 0
				&& player.ReferenceHub.characterClassManager.IsHuman()
				&& !player.ReferenceHub.fpc.staminaController._invigorated.IsEnabled
				&& !player.ReferenceHub.fpc.staminaController._scp207.IsEnabled
			)
				player.ReferenceHub.fpc.staminaController.RemainingStamina -= SanyaPlugin.Instance.Config.StaminaCostJump;
				player.ReferenceHub.fpc.staminaController._regenerationTimer = 0f;
		}
	}
}
