using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using UnityEngine;

namespace SanyaPlugin.Commands.Items
{
	public class DropCommand : ICommand
	{
		public string Command { get; } = "drop";

		public string[] Aliases { get; }

		public string Description { get; } = "アイテムを投下する";

		public string RequiredPermission { get; } = "sanya.items.drop";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			if(arguments.Count == 2)
			{
				var target = Player.Get(arguments.At(0));
				if(target == null)
				{
					response = "ターゲットが見つかりません。";
					return false;
				}
				ItemType itemtype = (ItemType)Enum.Parse(typeof(ItemType), arguments.At(1));
				Methods.SpawnItem(itemtype, target.Position + Vector3.up * 3);
				response = $"{target.Nickname}に{itemtype}を投下しました。";
				return true;
			}
			else
			{
				response = "引数:[player] [itemId]";
				return false;
			}
		}
	}
}
