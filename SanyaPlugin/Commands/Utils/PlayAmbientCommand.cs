using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;

namespace SanyaPlugin.Commands.Utils
{
	public class PlayAmbientCommand : ICommand
	{
		public string Command { get; } = "amb";

		public string[] Aliases { get; }

		public string Description { get; } = "アンビエントを再生します";

		public string RequiredPermission { get; } = "sanya.utils.amb";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			Map.PlayAmbientSound(int.Parse(arguments.At(0)));

			response = $"{arguments.At(0)}を再生しました。";
			return true;
		}
	}
}
