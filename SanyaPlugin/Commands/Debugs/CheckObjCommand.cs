﻿using System;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using NorthwoodLib.Pools;
using UnityEngine;

namespace SanyaPlugin.Commands.Debugs
{
	[CommandHandler(typeof(SanyaCommand))]
	public class CheckObjCommand : ICommand
	{
		public string Command { get; } = "checkobj";

		public string[] Aliases { get; }

		public string Description { get; } = "プレイヤーが見ているオブジェクトの持つコンポーネントを表示する";

		public string RequiredPermission { get; } = "sanya.debugs.checkobj";

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

			if(Physics.Raycast(player.Position + player.CameraTransform.forward, player.CameraTransform.forward, out var casy))
			{
				Log.Warn($"{casy.transform.name} (layer{casy.transform.gameObject.layer})");
				Log.Warn($"  Components:");
				foreach(var i in casy.transform.gameObject.GetComponents<Component>())
					Log.Warn($"    {i.name}:{i.GetType()}");
				Log.Warn($"  ComponentsInChildren:");
				foreach(var i in casy.transform.gameObject.GetComponentsInChildren<Component>())
					Log.Warn($"    {i.name}:{i.GetType()}");
				Log.Warn($"  ComponentsInParent:");
				foreach(var i in casy.transform.gameObject.GetComponentsInParent<Component>())
					Log.Warn($"    {i.name}:{i.GetType()}");
				response = "サーバーログに出力しました。";
				return true;
			}
			else
			{
				response = "ターゲットが見つかりません。";
				return false;
			}
		}
	}
}