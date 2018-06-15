// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using Bowhead.Server.Actors;
using Bowhead.Client.Actors;

namespace Bowhead.Actors.Spells {

	public abstract class AreaOfEffectActor<T> : AreaOfEffectActor where T : AreaOfEffectActor<T>, new() {

		public override Type serverType {
			get {
				return typeof(T);
			}
		}

		public override Type clientType {
			get {
				return typeof(T);
			}
		}

	}

	public class AreaOfEffectActor : DamageableActor, ColliderTriggerReceiver, TargetableActor {

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		Team _team;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		PlayerState _owningPlayer;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		StaticAssetRef<AreaOfEffectClass> _effectClass;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		DamageableActor _parent;
		[Replicated]
		Vector4 _position;
		[Replicated]
		Vector3 _normal;
		[Replicated(Notify = "OnRep_Placed")]
		bool _placed;

		TotemPlacement _placement;
		Transform _visual;
		CapsuleCollider _damageCollider;
		Transform _selectionVisual;
		Collider _selectionHitTest;
		DecalComponent _selectionVisualDecal;
		DecalComponent _borderDecal;
		Transform _healthBarRoot;
		Image _healthBarImage;
		Image _healthBarFrame;
		RectTransform _healthBarImageRect;
		Color _highlightOutlineColor;
		Color _selectedOutlineColor;
		Color _highlightCircleColor;
		Color _selectedCircleColor;
		Color _healthBarUnselectedColor;
		Color _healthBarSelectedColor;
		Color _healthBarHighlightedColor;
		bool _highlighted;
		float _highlightTime;
		//HighlightingSystem.Highlighter _outline;
		bool _initLocalPlayer;
		Actor _instigator;
		ServerPlayerController _serverOwningPlayer;
		List<DamageableActor> _touching = new List<DamageableActor>();
		Dictionary<DamageableActor, List<Spell>> _tracked;
		float _health;
		List<GameObject> _attachments = new List<GameObject>();
		List<SpellEvents> _events = new List<SpellEvents>();
		float _nextApply;
		int _wasJustPlaced;
		float _spellPower;

		readonly ActorRPC<Vector4, Vector3, bool> rpc_Multicast_Placed;
		readonly ActorRPC rpc_Multicast_SpawnCastFx;

		public AreaOfEffectActor() {
			SetReplicates(true);
			rpc_Multicast_Placed = BindRPC<Vector4, Vector3, bool>(Multicast_Placed);
			rpc_Multicast_SpawnCastFx = BindRPC(Multicast_SpawnCastFx);
		}

		public virtual void ServerConstruct(int level, float spellPower, ServerPlayerController player, Actor instigator, DamageableActor parent, Team team, AreaOfEffectClass effectClass) {
			this.effectClass = effectClass;
			_serverOwningPlayer = player;
			_team = team;
			_owningPlayer = (player != null) ? player.playerState : null;
			_instigator = instigator ?? this;
			_parent = parent;
			_nextApply = 0f;
			_spellPower = spellPower;

			if (effectClass.removeEffectWhenOutOfArea) {
				_tracked = new Dictionary<DamageableActor, List<Spell>>();
			}

			ConstructDamagableActorClass(new ConstructDamagableActorClassParams(effectClass.metaClass, effectClass.properties, effectClass.resistances, effectClass.health, effectClass.physicalMaterial, null, effectClass.powerScale));

			if (GameManager.instance.clientWorld == null) {
				AttachExternalGameObject(GameObject.Instantiate(effectClass.prefabs.serverPrefab.Load().gameObject));
			} else {
				AttachExternalGameObject(GameObject.Instantiate(effectClass.prefabs.mixedPrefab.Load().gameObject));
			}

			ServerInitActorLevel(level);
			ServerInit();

			if (_parent != null) {
				_placed = true;
				_wasJustPlaced = 2;
			} else {
				SetLifetime(effectClass.lifetime);
			}
		}

		protected virtual void ServerInit() {
			InitFogOfWar();
		}

		public void ServerPlace(Vector3 pos, float rotation) {
			RaycastHit hitInfo;
			Vector3 n = Vector3.up;
			if (effectClass.orientToGround && Physics.Raycast(new Ray(pos + Vector3.up, Vector3.down), out hitInfo, Mathf.Infinity, Layers.TerrainMask|Layers.WaterMask, QueryTriggerInteraction.Collide)) {
				n = hitInfo.normal;
			}

			_placement.SetPosition(pos, n, rotation, true);
			_placed = true;
			_position = new Vector4(pos.x, pos.y, pos.z, rotation);
			_normal = n;
			_nextApply = 0f;
			_wasJustPlaced = 2;
            go.SetActive(true);

			NetFlush();
			rpc_Multicast_Placed.Invoke(_position, _normal, true);
		}

		public void ServerPickup() {
			_placed = false;

			Uncast();
			_touching.Clear();

			go.SetActive(false);
			rpc_Multicast_Placed.Invoke(Vector3.zero, Vector3.zero, false);
		}

		void OnRep_Placed() {
			if (GameManager.instance.serverWorld == null) {
				go.SetActive(_placed);
			}

			if (_parent != null) {
				return;
			}

			if (_placed && (_placement != null)) { // may be null if we are hosting a match and the server GO is destroyed
				_placement.SetPosition(_position, _normal, _position.w, true);
			}
		}

		[RPC(ERPCDomain.Multicast, CheckRelevancy = true)]
		void Multicast_Placed(Vector4 position, Vector3 normal, bool placed) {
			if (GameManager.instance.serverWorld == null) {
				go.SetActive(placed);
			}
			if (placed) {
				_placement.SetPosition(position, normal, position.w, true);
				ClientSpawnPlacedFx();
			} else {
				ClientSpawnPickedUpFx();
			}
		}

		public override void Tick() {
			base.Tick();

			if (hasAuthority) {
				if (_placed) {

					// destroyed actors won't get removed via events.
					// do it here.

					for (int i = _touching.Count-1; i >= 0; --i) {
						var target = _touching[i];
						if (target.dead || target.pendingKill) {
							_touching.RemoveAtSwap(i);
							ServerOnTriggerExit(target);
						}
					}

					if (effectClass.reapplyRate > 0f) {
						if (_wasJustPlaced <= 0) {
							_nextApply -= world.deltaTime;
							if (_nextApply <= 0f) {
								rpc_Multicast_SpawnCastFx.Invoke();

								_nextApply = effectClass.reapplyRate;

								for (int i = 0; i < _touching.Count; ++i) {
									var target = _touching[i];

									if (!(target.dead || target.pendingKill) && TargetIsInFOV(target)) {
										if (_tracked != null) {
											List<Spell> spells = _tracked[target];
											Cast(target, spells);
										} else {
											Cast(target, null);
										}
									}
								}
							}
						} else {
							--_wasJustPlaced;
						}
					}
				}
			} else {
				if (_highlightTime > 0f) {
					_highlightTime -= world.deltaTime;
					if (_highlightTime <= 0f) {
						SetHighlighted(false, 0f);
					}
				}
			}
		}

		public override void LateTick() {
			base.LateTick();

			if (_parent != null) {
				if (hasAuthority || (GameManager.instance.serverWorld == null)) {
					if ((_parent.go != null) && (go != null)) {
						go.transform.position = _parent.go.transform.position;
						if (effectClass.trackParentRotation) {
							go.transform.rotation = _parent.go.transform.rotation;
						}
					}
				}
			}
		}

		public override bool IsNetRelevantFor(ActorReplicationChannel channel) {
			return _placed && base.IsNetRelevantFor(channel);
		}

		protected override void OnRepActorProperty(ActorProperty property, ImmutableActorPropertyInstance instance) {
			base.OnRepActorProperty(property, instance);

			if (instance == health) {
				if (instance.value < _health) {
					if (!dead) {
						_placement.Damaged();
					}
				}

				_health = instance.value;

				if (_healthBarImage != null) {
					_healthBarImage.fillAmount = instance.value / instance.max;
					if ((ClientPlayerController.localPlayer != null) && (ClientPlayerController.localPlayer.playerState == _owningPlayer)) {
						//_healthBarImage.material.color = Unit.EvaluateHealthColor(instance.value / instance.max);
					}
				}
			}
		}

		protected override void OnGameObjectAttached() {
			base.OnGameObjectAttached();

			_placement = go.GetComponent<TotemPlacement>();

			_damageCollider = go.transform.GetChildComponent<CapsuleCollider>("Damageable");

			if (hasAuthority) {
				_placement.ServerInit();
				go.SetActive(_parent != null);

				if (GameManager.instance.clientWorld != null) {

					var visual = go.transform.Find("Visual");
					if (visual != null) {
						visual.gameObject.SetActive(false);
					}

					var selectionVisual = go.transform.Find("SelectionVisual");
					if (selectionVisual != null) {
						selectionVisual.gameObject.SetActive(false);
					}

					var healthBarRoot = go.transform.Find("HealthBar");
					if (healthBarRoot != null) {
						healthBarRoot.gameObject.SetActive(false);
					}
				}
			} else {
				_placement.ClientInit(_team, _owningPlayer);
				_visual = go.transform.Find("Visual");

				if (_visual != null) {
					_borderDecal = _visual.GetChildComponent<DecalComponent>("Decal");
					if (_borderDecal != null) {
						_borderDecal.material = AddGC(GameObject.Instantiate<Material>(_borderDecal.material));
					}
				}

                _selectionVisual = go.transform.Find("SelectionVisual");
				_healthBarRoot = go.transform.Find("HealthBar");

				{
					var t = go.transform.Find("SelectionHitTest");
					if (t != null) {
						_selectionHitTest = t.GetComponent<Collider>();
					}
				}

				if (_healthBarRoot != null) {
					var imageHealth = _healthBarRoot.Find("Health");
					var imageFrame = _healthBarRoot.Find("Frame");
					_healthBarImage = imageHealth.GetComponent<Image>();
					_healthBarFrame = imageFrame.GetComponent<Image>();
					_healthBarImageRect = imageHealth.GetComponent<RectTransform>();
					_healthBarFrame.material = AddGC(GameObject.Instantiate(_healthBarFrame.material));
					_healthBarImage.material = AddGC(GameObject.Instantiate(_healthBarImage.material));
					_healthBarImage.fillAmount = 1.0f;
					_healthBarRoot.gameObject.SetActive(false);
				}

				if (_selectionVisual != null) {
					_selectionVisualDecal = _selectionVisual.GetComponent<DecalComponent>();
					_selectionVisualDecal.material = AddGC(GameObject.Instantiate<Material>(_selectionVisualDecal.material));
				}
				_events.AddRange(go.GetComponentsInChildren<SpellEvents>());
			}
		}

		public virtual void InitLocalPlayer() {

			if ((ClientPlayerController.localPlayer != null) && (team != null) && !_initLocalPlayer) {
				_initLocalPlayer = true;
				LocalPlayerReceived();
			}
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();

			ConstructDamagableActorClass(new ConstructDamagableActorClassParams(effectClass.metaClass, effectClass.properties, effectClass.resistances, effectClass.health, effectClass.physicalMaterial, null, effectClass.powerScale));

			if (GameManager.instance.serverWorld == null) {
				AttachExternalGameObject(GameObject.Instantiate(effectClass.prefabs.clientPrefab.Load().gameObject));
			} else {
				// on client we share GO with local server if there is one
				var svActor = (AreaOfEffectActor)GameManager.instance.serverWorld.GetObjectByNetID(netID);
				if ((svActor != null) && !svActor.pendingKill) {
					AttachExternalGameObject(svActor.go);
				}
			}

			InitLocalPlayer();
			ApplyVisibility();
		}

		protected override void OnFogOfWarLocalVisibilityChanged() {
			ApplyVisibility();
		}

		protected virtual void LocalPlayerReceived() {
			InitFogOfWar();
			ApplyVisibility();

			if (ClientPlayerController.localPlayer.playerState == _owningPlayer) {
				_highlightOutlineColor = new Color(1.0F, 1.0F, 0.0F, 0.35F);
				_selectedOutlineColor = new Color(1.0F, 1.0F, 0.0F, 1.0F);
				_highlightCircleColor = new Color(1.0F, 1.0F, 0.0F, 0.1F);
				_selectedCircleColor = new Color(1.0F, 1.0F, 0.0F, 1.0F);
				_healthBarSelectedColor = new Color(1.0F, 1.0F, 0.0F, 1.0F);
				_healthBarUnselectedColor = new Color(1.0F, 1.0F, 0.0F, 0.0F);
				_healthBarHighlightedColor = new Color(1.0F, 1.0F, 0.0F, 0.35F);

				if (_healthBarImage != null) {
					//_healthBarImage.material.color = (health != null) ? Unit.EvaluateHealthColor(health.value / health.max) : new Color(0.0f, 0.99f, 0.0f, 1.0f);
				}

				if (_selectionVisualDecal != null) {
					_selectionVisualDecal.material.color = new Color(1.0F, 1.0F, 0.0F, 1.0F);
				}

				if (_borderDecal != null) {
					_borderDecal.material.color = Color.green;
				}
			} else if (IsFriendly(ClientPlayerController.localPlayer.team)) {
				_highlightOutlineColor = new Color(0.0F, 0.44F, 1.0F, 0.35F);
				_selectedOutlineColor = new Color(0.0F, 0.44F, 1.0F, 1.0F);
				_highlightCircleColor = new Color(0.0F, 0.44F, 1.0F, 0.1F);
				_selectedCircleColor = new Color(0.0F, 0.44F, 1.0F, 1.0F);
				_healthBarSelectedColor = new Color(0.0F, 0.44F, 1.0F, 1.0F);
				_healthBarUnselectedColor = new Color(0.0F, 0.44F, 1.0F, 0.0F);
				_healthBarHighlightedColor = new Color(0.0F, 0.44F, 1.0F, 0.35F);

				if (_healthBarImage != null) {
					_healthBarImage.material.color = new Color(0.0f, 0.44f, 1.0f, 1.0f);
				}

				if (_selectionVisualDecal != null) {
					_selectionVisualDecal.material.color = new Color(0.0F, 0.44F, 1.0F, 1.0F);
				}

				if (_borderDecal != null) {
					_borderDecal.material.color = new Color(0.0F, 0.44F, 1.0F, 1.0F);
				}
			} else {
				_highlightOutlineColor = new Color(1.0F, 0.0F, 0.0F, 0.35F);
				_selectedOutlineColor = new Color(1.0F, 0.0F, 0.0F, 1.0F);
				_highlightCircleColor = new Color(1.0F, 0.0F, 0.0F, 0.1F);
				_selectedCircleColor = new Color(1.0F, 0.0F, 0.0F, 1.0F);
				_healthBarSelectedColor = new Color(1.0F, 0.0F, 0.0F, 1.0F);
				_healthBarUnselectedColor = new Color(1.0F, 0.0F, 0.0F, 0.0F);
				_healthBarHighlightedColor = new Color(1.0F, 0.0F, 0.0F, 0.35F);

				if (_healthBarImage != null) {
					_healthBarImage.material.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
				}

				if (_selectionVisualDecal != null) {
					_selectionVisualDecal.material.color = new Color(1.0F, 0.0F, 0.0F, 1.0F);
				}

				if (_borderDecal != null) {
					_borderDecal.material.color = Color.red;
				}				
			}
		}
		
		protected virtual void ApplyVisibility() {
			if (_visual != null) {
				var wasActive = _visual.gameObject.activeSelf;
				_visual.gameObject.SetActive(fogOfWarLocalVisibility);

				if (_visual.gameObject.activeSelf && (wasActive != go.activeSelf)) {
					var cloth = _visual.gameObject.GetComponentsInChildren<Cloth>();
					foreach (var c in cloth) {
						c.enabled = false;
					}
					foreach (var c in cloth) {
						c.enabled = true;
					}
				}

				//if (_outline == null) {
				//	_outline = _visual.gameObject.AddComponent<HighlightingSystem.Highlighter>();
				//}
			}

			if (_selectionHitTest != null) {
				_selectionHitTest.gameObject.SetActive((team != null) && fogOfWarLocalVisibility);
			}

			for (int i = 0; i < _attachments.Count; ++i) {
				var go = _attachments[i];
				go.SetActive(fogOfWarLocalVisibility);
			}

			UpdateSelectionState();
		}

		void UpdateSelectionState() {
			//if ((_selectionVisual != null) && (_outline != null)) {

			//	_selectionVisual.gameObject.SetActive((team != null) && fogOfWarLocalVisibility && _highlighted);

			//	if (_highlighted) {
			//		if (_healthBarFrame != null) {
			//			_healthBarFrame.material.SetColor("_Color", _healthBarHighlightedColor);
			//		}
			//		if (_selectionVisualDecal != null) {
			//			_selectionVisualDecal.material.SetColor("_Color", _highlightCircleColor);
			//		}
			//		_outline.ConstantOnImmediate(_highlightOutlineColor);
			//	} else {
			//		if (_healthBarFrame != null) {
			//			_healthBarFrame.material.SetColor("_Color", _healthBarUnselectedColor);
			//		}
			//		_outline.ConstantOffImmediate();
			//	}

			//	if (_healthBarRoot != null) {
			//		if (fogOfWarLocalVisibility && (team != null)) {
			//			_healthBarRoot.gameObject.SetActive(true);
			//		} else {
			//			_healthBarRoot.gameObject.SetActive(false);
			//		}
			//	}
			//}
		}

		void ClientSpawnPlacedFx() {
			SpawnAttachments(effectClass.aoePlacedFx);

			for (int i = _events.Count-1; i >= 0; --i) {
				var e = _events[i];
				if (e != null) {
					e.EffectStart();
				} else {
					_events.RemoveAt(i);
				}
			}

			if (effectClass.sounds != null) {
				GameManager.instance.Play(go.transform.position, effectClass.sounds.spawned);
			}
		}

		void ClientSpawnPickedUpFx() {
			SpawnAttachments(effectClass.aoePickedUpFx);

			for (int i = _events.Count-1; i >= 0; --i) {
				var e = _events[i];
				if (e != null) {
					e.EffectStop();
				} else {
					_events.RemoveAt(i);
				}
			}
		}

		void ClientSpawnDestroyedFx() {
			SpawnAttachments(effectClass.aoeDestroyedFx);

			for (int i = _events.Count-1; i >= 0; --i) {
				var e = _events[i];
				if (e != null) {
					e.EffectStop();
				} else {
					_events.RemoveAt(i);
				}
			}

			if (effectClass.sounds != null) {
				GameManager.instance.Play(go.transform.position, effectClass.sounds.destroyed);
			}
		}

		void ClientSpawnExpiredFx() {
			SpawnAttachments(effectClass.aoeExpiredFx);

			for (int i = _events.Count-1; i >= 0; --i) {
				var e = _events[i];
				if (e != null) {
					e.EffectStop();
				} else {
					_events.RemoveAt(i);
				}
			}

			if (effectClass.sounds != null) {
				GameManager.instance.Play(go.transform.position, effectClass.sounds.expired);
			}
		}


		[RPC(ERPCDomain.Multicast, CheckRelevancy = true)]
		void Multicast_SpawnCastFx() {
			SpawnAttachments(effectClass.aoeCastFx);

			for (int i = _events.Count-1; i >= 0; --i) {
				var e = _events[i];
				if (e != null) {
					e.EffectCast();
				} else {
					_events.RemoveAt(i);
				}
			}

			if (effectClass.sounds != null) {
				GameManager.instance.Play(go.transform.position, effectClass.sounds.cast);
			}
		}

		void SpawnAttachments(AreaOfEffectClass.Attachment[] attachments) {
			if (attachments != null) {
				for (int i = 0; i < attachments.Length; ++i) {
					var attachment = attachments[i];
					if (attachment.prefab != null) {
						var gos = go.FindTagsInHierarchy(attachment.tag);
						for (int k = 0; k < gos.Length; ++k) {
							var parent = gos[k].transform;
							var newGO = (GameObject)GameObject.Instantiate(attachment.prefab.Load(), Vector3.zero, Quaternion.identity);
							if (newGO != null) {
								if (attachment.type != AreaOfEffectClass.EAttachmentType.Attached) {
									newGO.transform.position = parent.transform.position;
									if (attachment.type != AreaOfEffectClass.EAttachmentType.UnattachedDontOrient) {
										newGO.transform.rotation = parent.transform.rotation;
									}
								} else {
									newGO.transform.SetParent(parent, false);
								}
								_attachments.Add(newGO);
								_events.AddRange(newGO.GetComponentsInChildren<SpellEvents>());
							}
						}
					}
				}
			}
		}

		public override Team team {
			get {
				return _team;
			}
		}

		public override ServerPlayerController serverOwningPlayer {
			get {
				return _serverOwningPlayer;
			}
		}

		public override AttachmentLocations attachmentLocations {
			get {
				return new AttachmentLocations();
			}
		}

		protected override float defaultFogOfWarSightRadius {
			get {
				return effectClass.fogOfWarSightRadius;
			}
		}

		protected override float defaultFogOfWarObjectRadius {
			get {
				return effectClass.fogOfWarObjectRadius;
			}
		}

		protected override float defaultFogOfWarMaxVisRadius {
			get {
				return effectClass.fogOfWarMaxVisRadius;
			}
		}

		protected override bool defaultFogOfWarCanSeeUnderwater {
			get {
				return false;
			}
		}

		protected override EFogOfWarTest defaultFogOfWarTest {
			get {
				return effectClass.fogOfWarTest;
			}
		}

		protected override bool fogOfWarVisibleWhenDead {
			get {
				return true;
			}
		}

		public override float meleeAttackRadius {
			get {
				return (_damageCollider != null) ? _damageCollider.radius : 0f;
			}
		}

		public override Vector3 projectileTargetPos {
			get {
				if (_damageCollider != null) {
					return _damageCollider.GetWorldSpaceCenter();
				}
				return go.transform.position;
			}
		}

		public override bool targetable {
			get {
				return _placed;
			}
		}

		public override void EnableSelectionHitTest(bool enable) {
			if (_selectionHitTest != null) {
				_selectionHitTest.gameObject.SetActive(enable);
			}
		}

		public override bool RaycastSpawnBloodSpray(Vector3 contactLocation, Vector3 direction, bool bidrectionalHitTest, out RaycastHit hitInfo) {
			hitInfo = new RaycastHit();
			return false;
		}

		public override GameObject SpawnBloodSpray(Transform t, Vector3 pos, Quaternion rot) {
			return null;
		}

		public override bool CapsuleCast(Vector3 p1, Vector3 p2, float radius, Vector3 direction, float maxDistance, out RaycastHit hitInfo) {
			hitInfo = new RaycastHit();
			return false;
		}

		public override bool SphereCast(Vector3 p, float radius, Vector3 direction, float maxDistance, out RaycastHit hitInfo) {
			hitInfo = new RaycastHit();
			return false;
		}

		public override bool Raycast(Vector3 p, Vector3 direction, float maxDistance, out RaycastHit hitInfo) {
			if (_damageCollider != null) {
				var oldLayer = _damageCollider.gameObject.layer;
				_damageCollider.gameObject.layer = Layers.HitTest;
				var r = Physics.Raycast(p, direction, out hitInfo, maxDistance, Layers.HitTestMask);
				_damageCollider.gameObject.layer = oldLayer;
				return r;
			}

			hitInfo = new RaycastHit();
			return false;
		}

		protected override void OnDestroy() {
			if (hasAuthority) {
				Uncast();
				if (effectClass.timeToLiveAfterDestroy > 0f) {
					NetTearOff();
				}
			}
			base.OnDestroy();
		}

		protected override void ServerSimulateKill(SimulatedKillInfo ki) {
			NetFlush();
			Multicast_ClientSimulateKill(ki);
			Destroy();
		}

		protected override void ClientSimulateKill(SimulatedKillInfo ki) {
			ClientSpawnDestroyedFx();
		}

		protected override void OnNetTornOff() {
			base.OnNetTornOff();
			SetLifetime(effectClass.timeToLiveAfterDestroy);
		}

		protected override void Dispose(bool disposing) {
			if (disposing) {
				if (!(hasAuthority || dead)) {
					ClientSpawnExpiredFx();
				}
			}

			base.Dispose(disposing);
		}

		public void SetHighlighted(bool highlighted, float time) {
			if (time > -1f) {
				_highlightTime = time;
			}

			if ((_highlighted != highlighted) && ((time > -1f) || (_highlightTime <= 0f))) {
				_highlighted = highlighted;
				UpdateSelectionState();
			}
		}

		public bool highlighted {
			get {
				return _highlighted;
			}
		}

		public bool ProjectedBoundsTouchScreenRect(Camera camera, Rect rect) {
			return false;
		}

		bool TargetIsInFOV(DamageableActor target) {
			var fov = effectClass.fov;
			if (fov <= 0) {
				return true;
			}

			fov = fov/2f;
			if (fov >= 180f) {
				return true;
			}

			var pos = target.go.transform.position;
			var myPos = (_parent != null) ? _parent.go.transform.position : go.transform.position;

			Vector3 dir = (pos - myPos);
			dir.y = 0;
			dir = dir.normalized;

			var fwd = (_parent != null) ? _parent.go.transform.forward : go.transform.forward;
			var dot = Vector3.Dot(dir, fwd);

			var t = Mathf.Cos(fov*Mathf.Deg2Rad);
			Perf.End();
			return (dot >= t);
		}

		public void OnTriggerEnter(Collider other) {
			if (hasAuthority) {
				var target = other.transform.FindServerActorUpwards() as DamageableActor;
				if ((target != null) && (target != _parent)) {
					if (!(target.pendingKill || target.dead) && !_touching.Contains(target)) {
						ServerOnTriggerEnter(target);
					}
				}
			}
		}

		protected virtual void ServerOnTriggerEnter(DamageableActor actor) {
			_touching.Add(actor);

			if (_placed && TargetIsInFOV(actor)) {
				if (_tracked != null) {
					List<Spell> spells = new List<Spell>();
					_tracked.Add(actor, spells);
					if (!effectClass.unifiedUpdate) {
						Cast(actor, spells);
					}
				} else if (!effectClass.unifiedUpdate) {
					Cast(actor, null);
				}
			}
		}

		public void OnTriggerExit(Collider other) {
			if (hasAuthority) {
				var target = other.transform.FindServerActorUpwards() as DamageableActor;
				if (target != null) {
					if (_touching.Remove(target)) {
						ServerOnTriggerExit(target);
					}
				}
			}
		}

		protected virtual void ServerOnTriggerExit(DamageableActor actor) {
			if (_tracked != null) {
				List<Spell> spells;
				if (_tracked.TryGetValue(actor, out spells)) {
					Uncast(spells);
					_tracked.Remove(actor);
				}
			}
		}

		void Cast(DamageableActor target, List<Spell> spells) {
			SpellCastRule rule;
			if (SpellCastRule.GetBestRule(effectClass.spells, _team, target, out rule)) {
				rule.Execute(level, _spellPower, GameManager.instance.randomNumber, _team, _instigator, _serverOwningPlayer, target, spells);
			}
		}

		void Uncast(List<Spell> spells) {
			for (int i = 0; i < spells.Count; ++i) {
				var s = spells[i];
				if (!s.disposed) {
					s.OnProcEnd(EExpiryReason.Cleansed, null, this, _serverOwningPlayer);
				}
			}
		}

		void Uncast() {
			if (_tracked != null) {
				foreach (var spells in _tracked.Values) {
					Uncast(spells);
				}
				_tracked.Clear();
			}
		}

		public bool placed {
			get {
				return _placed;
			}
		}

		public AreaOfEffectClass effectClass {
			get {
				return _effectClass;
			}
			private set {
				_effectClass = value;
			}
		}

		public override Type serverType {
			get {
				return typeof(AreaOfEffectActor);
			}
		}

		public override Type clientType {
			get {
				return typeof(AreaOfEffectActor);
			}
		}
	}
}