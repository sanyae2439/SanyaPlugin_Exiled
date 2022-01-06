using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace SanyaPlugin.Commands.Utils.Scp914
{
	public class UseCommand : ICommand
	{
		public string Command { get; } = "use";

		public string[] Aliases { get; }

		public string Description { get; } = "SCP-914を起動させる";

		public string RequiredPermission { get; } = "sanya.utils.914.use";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			if(Exiled.API.Features.Scp914.Scp914Controller._remainingCooldown > 0f)
			{
				response = "SCP-914がクールタイム中です。";
				return false;
			}

			Exiled.API.Features.Scp914.Scp914Controller.ServerInteract(Server.Host.ReferenceHub, 1);
			response = "使用しました。";
			return true;
		}
	}
}
