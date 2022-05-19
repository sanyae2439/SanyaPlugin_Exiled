using System.Linq;
using AdminToys;
using Exiled.API.Features;
using Mirror;
using UnityEngine;

namespace SanyaPlugin.Components
{
	public class LightMoveComponent : MonoBehaviour
	{
		public LightSourceToy SourceObject { get; private set; }
		public Player TargetPlayer { get; private set; }
		public static GameObject prefab;
		public float Timer { get; set; } = 10f;

		private void Start()
		{
			if(prefab == null)
				prefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("LightSource"));

			TargetPlayer = Player.Get(gameObject);
			SourceObject = Object.Instantiate(prefab.GetComponent<LightSourceToy>());

			SourceObject.NetworkLightColor = TargetPlayer.Role.Color;
			SourceObject.NetworkLightIntensity = 1f;
			SourceObject.NetworkLightRange = 25f;
			NetworkServer.Spawn(SourceObject.gameObject);
		}

		private void FixedUpdate()
		{
			if(TargetPlayer.Role == RoleType.None || TargetPlayer.Role == RoleType.Spectator || Timer <= 0f)
				Object.Destroy(this);

			if(TargetPlayer.Role.Color != SourceObject.LightColor)
				SourceObject.NetworkLightColor = TargetPlayer.Role.Color;

			SourceObject.transform.position = TargetPlayer.Position + Vector3.up;
			Timer -= Time.deltaTime;
		}

		private void OnDestroy()
		{
			if(SourceObject != null)
				NetworkServer.Destroy(SourceObject.gameObject);
		}
	}
}
