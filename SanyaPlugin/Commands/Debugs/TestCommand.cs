using System;
using CommandSystem;
using Exiled.Permissions.Extensions;

namespace SanyaPlugin.Commands.Debugs
{
	public class TestCommand : ICommand
	{
		public string Command { get; } = "test";

		public string[] Aliases { get; }

		public string Description { get; } = "テスト用";

		public string RequiredPermission { get; } = "sanya.debugs.test";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			response = string.Empty;
			// testing zone start



			// testing zone end
			response = response.TrimEnd('\n');
			return true;
		}
	}
}