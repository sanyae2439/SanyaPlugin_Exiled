using CustomPlayerEffects;
using Exiled.API.Features;
using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Pickups;
using InventorySystem.Items.Usables.Scp244;
using Mirror;
using UnityEngine;

namespace SanyaPlugin.Components
{
	public class Scp244MoveComponent : MonoBehaviour
	{
		public Scp244DeployablePickup SourceObject { get; private set; }
		public Player TargetPlayer { get; private set; }

		private void Start()
		{
			TargetPlayer = Player.Get(gameObject);

			if(!InventoryItemLoader.AvailableItems.TryGetValue(ItemType.SCP244b, out ItemBase ib))
				return;

			SourceObject = Object.Instantiate<ItemPickupBase>(ib.PickupDropModel, TargetPlayer.Position + Vector3.up, Quaternion.identity) as Scp244DeployablePickup;
			SourceObject.NetworkInfo = new PickupSyncInfo
			{
				ItemId = ib.ItemTypeId,
				Weight = ib.Weight,
				Serial = ItemSerialGenerator.GenerateNext(),
				Locked = true
			};
			SourceObject.Network_syncState = (byte)Scp244State.Active;
			SourceObject.MaxDiameter = 24.9f;
			SourceObject.Rb.useGravity = false;
			SourceObject.Rb.isKinematic = true;
			SourceObject.transform.SetParentAndOffset(TargetPlayer.GameObject.transform, Vector3.zero);
			SourceObject.transform.localScale = Vector3.zero;
			SourceObject.RefreshPositionAndRotation();
			NetworkServer.Spawn(SourceObject.gameObject);
			SourceObject.InfoReceived(default, SourceObject.Info);

			TargetPlayer.EnableEffect<Vitality>();
		}

		private void FixedUpdate()
		{
			if(TargetPlayer.Role == RoleType.None || TargetPlayer.Role == RoleType.Spectator)
				Object.Destroy(this);
		}

		private void OnDestroy()
		{
			if(SourceObject != null)
				SourceObject.State = Scp244State.Destroyed;
		}
	}
}
