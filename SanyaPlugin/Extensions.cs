using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Exiled.API.Features;
using Hints;
using UnityEngine;

namespace SanyaPlugin
{
	public static class Extensions
	{
		public static Task StartSender(this Task task) => task.ContinueWith((x) => { Log.Error($"[Sender] {x}"); }, TaskContinuationOptions.OnlyOnFaulted);

		public static bool IsEnemy(this Player player, Team target)
		{
			if(player.Role == RoleType.Spectator || player.Role == RoleType.None || player.Role.Team == target)
				return false;

			return target == Team.SCP ||
				((player.Role.Team != Team.MTF && player.Role.Team != Team.RSC) || (target != Team.MTF && target != Team.RSC))
				&&
				((player.Role.Team != Team.CDP && player.Role.Team != Team.CHI) || (target != Team.CDP && target != Team.CHI))
			;
		}

		public static int GetHealthAmountPercent(this Player player) => (int)(100f - (Mathf.Clamp01(1f - player.Health / player.MaxHealth) * 100f));

		public static int GetAHPAmountPercent(this Player player) => (int)(100f - (Mathf.Clamp01(1f - player.ArtificialHealth / (float)player.MaxArtificialHealth) * 100f));

		public static void SendTextHintNotEffect(this Player player, string text, float time) => player.ReferenceHub.hints.Show(new TextHint(text, new HintParameter[] { new StringHintParameter(string.Empty) }, null, time));

		public static void SetParentAndOffset(this Transform target, Transform parent, Vector3 local)
		{
			target.SetParent(parent);
			target.position = parent.position;
			target.transform.localPosition = local;
			var localoffset = parent.transform.TransformVector(target.localPosition);
			target.localPosition = Vector3.zero;
			target.position += localoffset;
		}

		public static IEnumerable<Camera079> GetNearCams(this Player player)
		{
			foreach(var cam in Scp079PlayerScript.allCameras)
			{
				var dis = Vector3.Distance(player.Position, cam.transform.position);
				if(dis <= 15f)
				{
					yield return cam;
				}
			}
		}

		public static void SendHitmarker(this Player player, float size = 1f) => Hitmarker.SendHitmarker(player.Connection, size);

		public static void SendReportText(this Player player, string text) => player.SendConsoleMessage($"[REPORTING] {text}", "white");

		public static T CallBaseMethod<T>(this object instance, Type targetType, string methodName) => (T)Activator.CreateInstance(
				typeof(T),
				instance,
				targetType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).MethodHandle.GetFunctionPointer());

		public static T GetRandomOne<T>(this List<T> list) => list[UnityEngine.Random.Range(0, list.Count)];

		public static T Random<T>(this IEnumerable<T> ie)
		{
			if(!ie.Any()) return default;
			return ie.ElementAt(UnityEngine.Random.Range(0, ie.Count()));
		}

		public static bool IsList(this Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

		public static bool IsDictionary(this Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>);

		public static Type GetListArgs(this Type type) => type.GetGenericArguments()[0];
	}
}
