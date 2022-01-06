using System;
using CommandSystem;
using Exiled.Permissions.Extensions;
using Mirror;
using NorthwoodLib.Pools;
using UnityEngine;

namespace SanyaPlugin.Commands.Debugs
{
	[CommandHandler(typeof(SanyaCommand))]
	public class CheckComponentsCommand : ICommand
	{
		public string Command { get; } = "checkcomponents";

		public string[] Aliases { get; } = new string[] { "chkcomp" };

		public string Description { get; } = "NetworkObjectの持つComponentを一覧で表示する";

		public string RequiredPermission { get; } = "sanya.debugs.checkcomponents";

		public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
		{
			if(!sender.CheckPermission(RequiredPermission))
			{
				response = $"{RequiredPermission}の権限がありません。";
				return false;
			}

			var builder = StringBuilderPool.Shared.Rent();
			foreach(var identity in UnityEngine.Object.FindObjectsOfType<NetworkIdentity>())
			{
				builder.AppendLine($"{identity.transform.name} (layer{identity.transform.gameObject.layer})");
				builder.AppendLine($"  Components:");
				foreach(var i in identity.transform.gameObject.GetComponents<Component>())
					builder.AppendLine($"    {i?.name}:{i?.GetType()}");
				builder.AppendLine($"  ComponentsInChildren:");
				foreach(var j in identity.transform.gameObject.GetComponentsInChildren<Component>())
					builder.AppendLine($"    {j?.name}:{j?.GetType()}");
				builder.AppendLine($"  ComponentsInParent:");
				foreach(var k in identity.transform.gameObject.GetComponentsInParent<Component>())
					builder.AppendLine($"    {k?.name}:{k?.GetType()}");
			}
			builder.AppendLine("---------END OF LIST----------");

			response = builder.ToString();
			StringBuilderPool.Shared.Return(builder);
			return true;
		}
	}
}
