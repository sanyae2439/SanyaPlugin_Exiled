using System;
using System.Linq;
using AdminToys;
using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SanyaPlugin.Commands.Debugs
{
	public class TestCommand : ICommand
	{
		public string Command { get; } = "test";

		public string[] Aliases { get; }

		public string Description { get; } = "テスト用";

		public string RequiredPermission { get; } = "sanya.debugs.test";

		PrimitiveObjectToy targetPrimitive;

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			Player player = Player.Get(sender);
			response = $"args:{arguments.Join(null, " ")}\n";
			// testing zone start



			// testing zone end
			response = response.TrimEnd('\n');
			return true;
		}
	}
}