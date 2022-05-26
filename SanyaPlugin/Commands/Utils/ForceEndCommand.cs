using System;
using CommandSystem;
using Exiled.Permissions.Extensions;

namespace SanyaPlugin.Commands.Utils
{
	public class ForceEndCommand : ICommand
	{
		public string Command { get; } = "forceend";

		public string[] Aliases { get; }

		public string Description { get; } = "ラウンドを強制終了します";

		public string RequiredPermission { get; } = "sanya.utils.forceend";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}
			SanyaPlugin.Instance.Handlers.nextForceEnd = true;
			response = "成功しました。";
			return true;
		}
	}
}
