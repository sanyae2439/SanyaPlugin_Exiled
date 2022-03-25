using System;
using CommandSystem;
using Exiled.API.Features;

namespace SanyaPlugin.Commands.Public
{
	public class ExHudCommand : ICommand
	{
		public string Command { get; } = "exhud";

		public string[] Aliases { get; }

		public string Description { get; } = "ExHUDの切り替えができます";

		public string RequiredPermission { get; } = "sanya.public.exhud";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			var player = Player.Get(sender);
			if(player == null)
			{
				response = "このコマンドはプレイヤーのみ使用できます。";
				return false;
			}

			if(player.GameObject.TryGetComponent<SanyaPluginComponent>(out var sanya))
			{
				sanya.DisableHud = !sanya.DisableHud;
				response = $"ExHUDを{(sanya.DisableHud ? "無効" : "有効")}にしました。";
				return true;
			}
			else
			{
				response = "さにゃこんぽーねんとが見つかりません。";
				return false;
			}
		}
	}
}
