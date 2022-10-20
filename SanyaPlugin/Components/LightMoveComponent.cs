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
		public float Intensity;
		public float Range;
		public static GameObject prefab;

		private void Start()
		{
			if(prefab == null)
				prefab = CustomNetworkManager.singleton.spawnPrefabs.First(x => x.name.Contains("LightSource"));

			TargetPlayer = Player.Get(gameObject);
			SourceObject = Object.Instantiate(prefab.GetComponent<LightSourceToy>());

			SourceObject.NetworkLightColor = TargetPlayer.Role.Color;
			SourceObject.NetworkLightIntensity = Intensity;
			SourceObject.NetworkLightRange = Range;
			SourceObject.transform.SetParentAndOffset(TargetPlayer.GameObject.transform, Vector3.zero);
			NetworkServer.Spawn(SourceObject.gameObject);
		}

		private void FixedUpdate()
		{
			if(TargetPlayer.Role == RoleType.None || TargetPlayer.Role == RoleType.Spectator)
				Object.Destroy(this);
		}

		private void OnDestroy()
		{
			if(SourceObject != null)
				NetworkServer.Destroy(SourceObject.gameObject);
		}
	}
}
