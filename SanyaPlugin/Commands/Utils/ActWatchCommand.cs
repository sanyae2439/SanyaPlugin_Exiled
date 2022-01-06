using System;
using CommandSystem;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using Mirror;

namespace SanyaPlugin.Commands.Utils
{
	public class ActWatchCommand : ICommand
	{
		public string Command { get; } = "actwatch";

		public string[] Aliases { get; }

		public string Description { get; } = "監視用";

		public string RequiredPermission { get; } = "sanya.utils.actwatch";

		private bool _isEnabled = false;

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			var player = Player.Get(sender);
			if(player == null)
			{
				response = "このコマンドはプレイヤーのみ使用できます。";
				return false;
			}

			if(!_isEnabled)
			{
				MirrorExtensions.SendFakeSyncObject(player, player.ReferenceHub.networkIdentity, typeof(PlayerEffectsController), (writer) =>
				{
					writer.WriteUInt64(1ul);
					writer.WriteUInt32(1);
					writer.WriteByte(4);
					writer.WriteUInt32(18);
					writer.WriteByte(1);
				});
				_isEnabled = true;
			}
			else
			{
				MirrorExtensions.SendFakeSyncObject(player, player.ReferenceHub.networkIdentity, typeof(PlayerEffectsController), (writer) =>
				{
					writer.WriteUInt64(1ul);
					writer.WriteUInt32(1);
					writer.WriteByte(4);
					writer.WriteUInt32(18);
					writer.WriteByte(0);
				});
				_isEnabled = false;
			}

			response = $"{!_isEnabled} -> {_isEnabled}";
			return true;
		}
	}
}
