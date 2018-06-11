// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead.Actors {
	public abstract class ItemPickupActor : Actor {

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		PlayerState _owner;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		StaticAssetRef<MetaGame.DropItemClass> _itemClass;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		ushort _count;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		ushort _ilvl;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		int _id;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		bool _mousePickup;
		[Replicated(Notify = "OnRep_serverPosition")]
		protected QuantizedVector3Field _serverPosition = new QuantizedVector3Field(Vector3.zero, new QuantizedFloatField.FixedPoint(8, 4), new QuantizedFloatField.FixedPoint(6, 4), new QuantizedFloatField.FixedPoint(8, 4));
		[Replicated]
		protected QuantizedVector3Field _serverVelocity = new QuantizedVector3Field(Vector3.zero, new QuantizedFloatField.FixedPoint(6, 4));

		[RPC(ERPCDomain.Multicast, Reliable = true)]
		protected virtual void Multicast_SpawnFX() {}

		[RPC(ERPCDomain.Multicast, Reliable = true)]
		protected virtual void Multicast_PickupFX() {}

		[RPC(ERPCDomain.Multicast, Reliable = true)]
		protected virtual void Multicast_Contact() {}

		protected virtual void OnRep_serverPosition() {}

		protected MetaGame.DropItemClass itemClass {
			get {
				return _itemClass;
			}
			set {
				_itemClass = value;
			}
		}

		protected PlayerState owner {
			get {
				return _owner;
			}
			set {
				_owner = value;
			}
		}

		protected int count {
			get {
				return _count;
			}
			set {
				_count = (ushort)value;
			}
		}

		protected int ilvl {
			get {
				return _ilvl;
			}
			set {
				_ilvl = (ushort)value;
			}
		}

		protected int id {
			get {
				return _id;
			}
			set {
				_id = value;
			}
		}

		public bool mousePickup {
			get {
				return _mousePickup;
			}
			protected set {
				_mousePickup = value;
			}
		}

		public override Type serverType {
			get {
				return typeof(ItemPickupActorServer);
			}
		}

		public override Type clientType {
			get {
				return typeof(ItemPickupActorClient);
			}
		}
	}

	public sealed class ItemPickupActorServer : ItemPickupActor, ColliderTriggerReceiver {
		const float SEARCH_RADIUS = 2;

		Server.Actors.ServerPlayerController _player;
		Rigidbody _rb;
		Collider _collider;
		bool _contact;
		float _flightTime;

		readonly ActorRPC rpc_Mutlicast_SpawnFX;
		readonly ActorRPC rpc_Mutlicast_PickupFX;
		readonly ActorRPC rpc_Multicast_Contact;

		public ItemPickupActorServer() {
			SetReplicates(true);
			SetReplicateRate(1/10f);
			rpc_Mutlicast_SpawnFX = BindRPC(Multicast_SpawnFX);
			rpc_Mutlicast_PickupFX = BindRPC(Multicast_PickupFX);
			rpc_Multicast_Contact = BindRPC(Multicast_Contact);
		}

		public void ServerInit(Vector3 pos, Server.Actors.ServerPlayerController player, MetaGame.DropItemClass itemClass, int ilvl, int id, int count) {
			_player = player;
			owner = player.playerState;
			this.id = id;
			this.ilvl = ilvl;
			this.count = count;
			this.itemClass = itemClass;

			pos += Vector3.up;

			var prefab = GameObject.Instantiate(itemClass.serverPrefab);
			mousePickup = prefab.GetComponent<ColliderContactCallback>() == null;

			AttachExternalGameObject(prefab.gameObject);
			_rb = go.GetComponent<Rigidbody>();
			_rb.transform.position = pos;
			_collider = go.GetComponent<Collider>();

			//for (var rad = 4.0f; rad <= 30; rad += SEARCH_RADIUS) {
			//	Vector3 target;
			//	var rand = GameManager.instance.randomNumber*360;
			//	if (Scripting.UnitSquad.FindClosestWalkablePointOnBoundary(pos, new Vector2(rad, 0), rand, 0, out target)) {
			//		var vel = UnitRangedAction.ComputeVelocityToHit(pos, target, Vector3.zero, Physics.gravity.y, rad*5, out target);
			//		_rb.velocity = vel;
			//		_flightTime = vel.w;
			//		break;
			//	}
			//}

			_serverPosition.value = _rb.transform.position;
			_serverVelocity.value = _rb.velocity;

			NetFlush();
			rpc_Mutlicast_SpawnFX.Invoke();
		}

		public override void Tick() {
			base.Tick();

			if (!_contact) {

				_flightTime -= world.deltaTime;

				if (_flightTime <= 0f) {
					// contacted ground
					_rb.position = Utils.PutPositionOnGroundOrWater(_rb.position);
					_rb.velocity = Vector3.zero;
					_rb.isKinematic = true;
					_collider.enabled = true;
					_contact = true;
				}

				_serverPosition.value = _rb.position;
				_serverVelocity.value = _rb.velocity;

				if (_contact) {
					NetFlush();
					rpc_Multicast_Contact.Invoke();
				}
			}
		}

		public void ServerPickup(Server.Actors.ServerPlayerController player) {
			if (player.playerState == owner) {
				if (itemClass.GrantItem(null, _player, id, ilvl, count)) {
					itemClass.ProcOnPickup(this, null, player, ilvl);
					NetFlush();
					rpc_Mutlicast_PickupFX.Invoke();
					Destroy();
				}
			}
		}

		//void ServerPickup(Unit unit) {
		//	if (!(unit.dead || unit.pendingKill) && (unit.serverOwningPlayer != null) && (unit.serverOwningPlayer.playerState == owner)) {
		//		if (itemClass.GrantItem(unit, _player, id, ilvl, count)) {
		//			itemClass.ProcOnPickup(this, unit, unit.serverOwningPlayer, ilvl);
		//			NetFlush();
		//			rpc_Mutlicast_PickupFX.Invoke();
		//			Destroy();
		//		}
		//	}
		//}

		public void OnTriggerEnter(Collider other) {
			//var unit = other.transform.FindServerActorUpwards() as Unit;
			//if (unit != null) {
			//	ServerPickup(unit);
			//}
		}

		public void OnTriggerExit(Collider other) {}

		public override bool IsNetRelevantFor(ActorReplicationChannel channel) {
			if (itemClass.hasValidTeamPrefab || (_player == channel.owningPlayer)) {
				return base.IsNetRelevantFor(channel);
			}
			return false;
		}

	}

	public sealed class ItemPickupActorClient : ItemPickupActor, TargetableActor {

		ItemPickupClientPrefab _prefab;
		Rigidbody _rb;

		public override void PostNetConstruct() {
			base.PostNetConstruct();
			ItemPickupClientPrefab prefab;

			if (Client.Actors.ClientPlayerController.IsLocalPlayer(owner)) {
				prefab = itemClass.LoadLocalPrefab();
			} else {
				prefab = itemClass.LoadTeamPrefab();
			}

			_prefab = GameObject.Instantiate(prefab);
			AttachExternalGameObject(_prefab.gameObject);
			_prefab.ClientInit(owner, itemClass, ilvl);
			_rb = go.GetComponent<Rigidbody>();
			ClientSnapPosition();
		}

		protected override void Multicast_PickupFX() {
			_prefab.PickupFX();
		}

		protected override void Multicast_SpawnFX() {
			_prefab.SpawnFX();
		}

		public bool ProjectedBoundsTouchScreenRect(Camera camera, Rect rect) {
			return false;
		}

		public void SetHighlighted(bool highlight, float time) {
			_prefab.highlighted = highlight;
		}

		public override void ClientFixedUpdate(float dt) {
			base.ClientFixedUpdate(dt);
			var pos = Vector3.Lerp(_rb.position, _serverPosition.value, ProjectileActor.POSITION_LERP_FACTOR*dt);
			var vel = Vector3.Lerp(_rb.velocity, _serverVelocity.value, ProjectileActor.VELOCITY_LERP_FACTOR*dt);

			_rb.MovePosition(pos);
			_rb.velocity = vel;
		}

		void ClientSnapPosition() {
			_rb.velocity = _serverVelocity.value;
			_rb.transform.position = _serverPosition.value;
		}

		protected override void OnRep_serverPosition() {
			// predict ahead based on ping-time/2
			var dtPing = (Client.Actors.ClientPlayerController.localPlayerPingSeconds / 2f);
			_serverVelocity.value += Physics.gravity * dtPing;
			_serverPosition.value += _serverVelocity.value * dtPing;
		}

		protected override void Multicast_Contact() {
			_rb.isKinematic = true;
			ClientSnapPosition();
			_prefab.ContactFX();
		}

		public bool highlighted {
			get {
				return _prefab.highlighted;
			}
		}

		public Team team {
			get {
				return owner.team;
			}
		}

		public bool clientPickedUp;
	}
}
