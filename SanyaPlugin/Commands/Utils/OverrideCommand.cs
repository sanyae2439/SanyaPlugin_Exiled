using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace SanyaPlugin.Commands.Utils
{
	public class OverrideCommand : ICommand
	{
		public string Command { get; } = "override";

		public string[] Aliases { get; }

		public string Description { get; } = "オーバーライド";

		public string RequiredPermission { get; } = "sanya.utils.override";

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

			if(SanyaPlugin.Instance.Handlers.Overrided != null)
				SanyaPlugin.Instance.Handlers.Overrided = null;
			else
				SanyaPlugin.Instance.Handlers.Overrided = player;

			response = $"{SanyaPlugin.Instance.Handlers.Overrided?.Nickname}をセットしました。";
			return true;
		}
	}
}
