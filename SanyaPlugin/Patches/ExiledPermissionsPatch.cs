using System;
using System.Linq;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using HarmonyLib;
using NorthwoodLib.Pools;

namespace SanyaPlugin.Patches
{
	[HarmonyPatch(typeof(Permissions), nameof(Permissions.CheckPermission), new Type[] { typeof(Player), typeof(string) })]
	public static class ExiledPermissionPatcher
	{
		public static bool Prefix(Player player, string permission, ref bool __result)
		{
			if(string.IsNullOrEmpty(permission) || player == null || player.GameObject == null || Permissions.Groups == null || Permissions.Groups.Count == 0 || player.ReferenceHub.isDedicatedServer)
			{
				__result = false;
				return false;
			}

			Log.Debug($"UserID: {player.UserId} | PlayerId: {player.Id}", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
			Log.Debug($"Permission string: {permission}", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);

			var plyGroupKey = player.Group != null ? ServerStatic.GetPermissionsHandler()._groups.FirstOrDefault(g => g.Value == player.Group).Key : player.GroupName;
			if(string.IsNullOrEmpty(plyGroupKey))
				plyGroupKey = player.Group != null ? ServerStatic.GetPermissionsHandler()._members.FirstOrDefault(g => g.Key == player.UserId).Value : player.GroupName;

			if(string.IsNullOrEmpty(plyGroupKey))
			{
				__result = false;
				return false;
			}

			Log.Debug($"GroupKey: {plyGroupKey}", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);

			if(plyGroupKey == null || !Permissions.Groups.TryGetValue(plyGroupKey, out var group))
			{
				Log.Debug("The source group is null, the default group is used", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
				group = Permissions.DefaultGroup;
			}
				

			if(group is null)
			{
				Log.Debug("There's no default group, returning false...", Exiled.Loader.Loader.ShouldDebugBeShown || SanyaPlugin.Instance.Config.IsDebugged);
				__result = false;
				return false;
			}

			const char permSeparator = '.';
			const string allPerms = ".*";

			if(group.CombinedPermissions.Contains(allPerms))
			{
				__result = true;
				return false;
			}


			if(permission.Contains(permSeparator))
			{
				var strBuilder = StringBuilderPool.Shared.Rent();
				var seraratedPermissions = permission.Split(permSeparator);

				bool Check(string source) => group.CombinedPermissions.Contains(source, StringComparison.OrdinalIgnoreCase);

				var result = false;
				for(var z = 0; z < seraratedPermissions.Length; z++)
				{
					if(z != 0)
					{
						strBuilder.Length -= allPerms.Length;

						strBuilder.Append(permSeparator);
					}

					strBuilder.Append(seraratedPermissions[z]);

					if(z == seraratedPermissions.Length - 1)
					{
						result = Check(strBuilder.ToString());
						break;
					}

					strBuilder.Append(allPerms);
					if(Check(strBuilder.ToString()))
					{
						result = true;
						break;
					}
				}

				StringBuilderPool.Shared.Return(strBuilder);

				__result = result;
				return false;
			}

			__result = group.CombinedPermissions.Contains(permission, StringComparison.OrdinalIgnoreCase);
			return false;
		}
	}
}
