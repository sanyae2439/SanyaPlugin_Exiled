using System;
using System.Linq;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using InventorySystem.Items.Usables.Scp330;
using Utils.Networking;

namespace SanyaPlugin.Commands.Items
{
	public class ExplosionCommand : ICommand
	{
		public string Command { get; } = "explosion";

		public string[] Aliases { get; }

		public string Description { get; } = "爆発エフェクトを起こします";

		public string RequiredPermission { get; } = "sanya.items.explosion";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			if(arguments.Count == 1)
			{
				var target = Player.Get(arguments.At(0));
				if(target == null)
				{
					response = "ターゲットが見つかりません。";
					return false;
				}
				new CandyPink.CandyExplosionMessage() { Origin = target.Position }.SendToAuthenticated();
				response = $"{target.Nickname}に起こしました。";
				return true;
			}
			else
			{
				response = "引数:[player]";
				return false;
			}
		}
	}
}
