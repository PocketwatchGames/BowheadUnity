// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;
using Bowhead.Server.Actors;

namespace Bowhead.Actors {
	public struct ProjectileContact {
		public readonly Actor actor;
		public readonly Collider hit;
		public readonly Vector3 contact;
		public readonly Vector3 normal;
		
		public ProjectileContact (Actor actor, Collider hit, Vector3 contact, Vector3 normal) {
			this.actor = actor;
			this.hit = hit;
			this.contact = contact;
			this.normal = normal;
		}

		public ProjectileContact(Collider self, RaycastHit hit) {
			actor = null;
			this.hit = hit.collider;
			contact = hit.point;
			normal = hit.normal;
		}

		public ProjectileContact(ContactPoint contact) : this(null, contact) {}

		public ProjectileContact(Actor actor, ContactPoint contact) {
			this.actor = actor;
			hit = contact.otherCollider;
			this.contact = contact.point;
			normal = contact.normal;
		}
	}

	public abstract class ProjectileActor : DamageableActor, ColliderContactReceiver {
		const float LAYER_SWITCH_DISTANCE_SQ = 0.25f*0.25f;
		const float ALIGN_TO_ROTATION_SPEED = 35;
		const float MAX_POSITION_DELTA_SNAP = 3f;
		const float MAX_POSITION_DELTA_SNAP_SQUARED = MAX_POSITION_DELTA_SNAP * MAX_POSITION_DELTA_SNAP;
		public const float POSITION_LERP_FACTOR = 6;
		public const float VELOCITY_LERP_FACTOR = 5;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		StaticAssetRef<ProjectileClass> _projectileClass;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		PlayerState _owningPlayerState;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		Team _team;

		[Replicated(Notify = "OnRep_serverPosition")]
		QuantizedVector3Field _serverPosition = new QuantizedVector3Field(Vector3.zero, new QuantizedFloatField.FixedPoint(8, 4), new QuantizedFloatField.FixedPoint(6, 4), new QuantizedFloatField.FixedPoint(8, 4));
		[Replicated]
		QuantizedVector3Field _serverVelocity = new QuantizedVector3Field(Vector3.zero, new QuantizedFloatField.FixedPoint(6, 4));
		[Replicated(Notify = "OnRep_Layer")]
		byte _layer;
		[Replicated]
		bool _killedByIce;

		Transform _visual;

		Quaternion _align;
		Rigidbody _rb;

		ServerPlayerController _player;
		ActorWithTeam _instigator;
		HashSet<Transform> _contacts = new HashSet<Transform>();
		float _selfHitTestTimeout = 0.25f;
		float _damageScale;
		float _spellPower;
		bool _layerSwitched;
		Vector3 _launchPos;

		readonly ActorRPC<Vector3, Vector3, StaticAssetRef<PhysicalMaterialClass>> rpc_Multicast_ContactFx;


		public ProjectileActor() {
			SetReplicates(true);
			SetReplicateRate(1/10f);
			rpc_Multicast_ContactFx = BindRPC<Vector3, Vector3, StaticAssetRef<PhysicalMaterialClass>>(Multicast_ContactFx);
        }
		
		public virtual void ConstructProjectileClass(ProjectileClass projectileClass) {
			_projectileClass = projectileClass;
			ConstructDamagableActorClass(new ConstructDamagableActorClassParams(projectileClass.metaClass, projectileClass.properties, null, projectileClass.health, projectileClass.physicalMaterial, null, 1));
		}

		public void ServerFire(ServerPlayerController player, ActorWithTeam instigator, Vector3 position, Vector3 velocity, float damageScale, float spellPower) {
			_player = player;
			_instigator = instigator;
			_team = _instigator.team;
			_owningPlayerState = (player != null) ? player.playerState : null;
			_damageScale = damageScale;
			_spellPower = spellPower;

			if (GameManager.instance.clientWorld == null) {
				AttachExternalGameObject(GameObject.Instantiate(projectileClass.serverPrefab.Load()));
			} else {
				AttachExternalGameObject(GameObject.Instantiate(projectileClass.mixedPrefab.Load()));
			}

			_rb = go.GetComponent<Rigidbody>();
			if (_rb != null) {
				_rb.isKinematic = false;
				_rb.useGravity = true;
				_rb.velocity = velocity;
				_rb.transform.position = position;
				_rb.interpolation = RigidbodyInterpolation.None;
				_rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
			} else {
				go.transform.position = position;
			}

			SetTeamLayer();
			
			ServerSnapPosition();
			ServerInit();
						
			_launchPos = go.transform.position;
		}

		protected virtual void ServerInit() { }

		protected override void OnGameObjectAttached() {
			base.OnGameObjectAttached();

			if (!hasAuthority) {
				_visual = go.transform.Find("Visual");

				if (team != null) {
					var teamColors = go.GetComponentsInAllChildren<TeamColors>();
					for (int i = 0; i < teamColors.Length; ++i) {
						var tc = teamColors[i];
						if (_owningPlayerState != null) {
							tc.SetSharedColors(_owningPlayerState);
						} else {
							tc.SetSharedColors(team);
						}
					}
				}

				if (_visual != null) {
					_visual.gameObject.SetActive(false);
				}
			}
		}

		void SetTeamLayer() {
			if (team != null) {
				ServerSetLayer(Layers.GetTeamProjectilesLayer(team.teamNumber));
			}
		}

		protected void ServerSetLayer(int layer) {
			_layer = (byte)layer;
			if (go != null) {
				go.layer = layer;
			}
		}

		void OnRep_Layer() {
			if ((go != null) && (GameManager.instance.serverWorld == null)) {
				go.layer = _layer;
			}
		}

		public override void Tick() {
			Perf.Begin("Bowhead.Actors.ProjectileActor.Tick");

			base.Tick();

			if (hasAuthority) {
				_selfHitTestTimeout -= world.deltaTime;
				if (_selfHitTestTimeout < 0f) {
					_selfHitTestTimeout = 0f;
				}

				ServerUpdate(world.deltaTime);
			}

			Perf.End();
		}

		void ServerUpdate(float dt) {
			ServerUpdatePosition();

			if (!_layerSwitched) {
				var dd = go.transform.position - _launchPos;
				if (dd.sqrMagnitude >= LAYER_SWITCH_DISTANCE_SQ) {
					_layerSwitched = true;
					_layer = Layers.NoSelfContactProjectiles;
					go.layer = _layer;
				}
			}
		}

		void ServerSnapPosition() {
			if (_rb != null) {
				_serverPosition.value = _rb.transform.position;
				_serverVelocity.value = _rb.velocity;
				if (_rb.velocity.sqrMagnitude > 0.001f) {
					_rb.transform.rotation = Quaternion.LookRotation(_rb.velocity.normalized);
				}
			} else {
				_serverPosition.value = go.transform.position;
				_serverVelocity.value = Vector3.zero;
			}
		}

		void ServerUpdatePosition() {
			if (GameManager.instance.fixedUpdateDidRun) {
				ServerSyncPosition();
				if ((_rb != null) && projectileClass.orientToVelocity && (_rb.velocity.sqrMagnitude > 0.001f)) {
					_rb.rotation = Quaternion.LookRotation(_rb.velocity.normalized);
				}
			}
		}

		protected void ServerSyncPosition() {
			if (_rb != null) {
				_serverPosition.value = _rb.position;
				_serverVelocity.value = _rb.velocity;
			} else {
				_serverPosition.value = go.transform.position;
				_serverVelocity.value = Vector3.zero;
			}
		}

		public override void ClientFixedUpdate(float dt) {
			base.ClientFixedUpdate(dt);
			if ((_rb != null) && (GameManager.instance.serverWorld == null)) {

				var pos = Vector3.Lerp(_rb.position, _serverPosition.value, POSITION_LERP_FACTOR*dt);
				var vel = Vector3.Lerp(_rb.velocity, _serverVelocity.value, VELOCITY_LERP_FACTOR*dt);

				_rb.MovePosition(pos);
				_rb.velocity = vel;

				if (projectileClass.orientToVelocity) {
					_rb.rotation = Quaternion.Lerp(_rb.rotation, _align, dt*ALIGN_TO_ROTATION_SPEED);
				}
			}
		}

		protected void ClientSnapPosition() {
			if (GameManager.instance.serverWorld == null) {
				if (_rb != null) {
					_rb.velocity = _serverVelocity.value;
					_rb.transform.position = _serverPosition.value;
					if (_serverVelocity.value.sqrMagnitude > 0.001f) {
						_rb.rotation = Quaternion.LookRotation(_serverVelocity.value.normalized);
					}
				} else {
					go.transform.position = _serverPosition.value;
				}
			}
		}
		
		protected virtual void OnRep_serverPosition() {
			
			// predict ahead based on ping-time/2
			var dtPing = (Client.Actors.ClientPlayerController.localPlayerPingSeconds / 2f);
			_serverVelocity.value += Physics.gravity * dtPing;
			_serverPosition.value += _serverVelocity.value * dtPing;
			
			if (projectileClass.orientToVelocity && (_serverVelocity.value.sqrMagnitude > 0.001f)) {
				_align = Quaternion.LookRotation(_serverVelocity.value.normalized);
			}
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();

			ConstructProjectileClass(projectileClass);

			if (GameManager.instance.serverWorld == null) {
				if (!GameManager.instance.serverPerfTest) {
					AttachExternalGameObject(GameObject.Instantiate(projectileClass.clientPrefab.Load()));
				}
			} else {
				// on client we share GO with local server if there is one
				var svActor = (ProjectileActor)GameManager.instance.serverWorld.GetObjectByNetID(netID);
				if ((svActor != null) && !svActor.pendingKill) {
					AttachExternalGameObject(svActor.go);
				}
			}

			if (go != null) {
				_rb = go.GetComponent<Rigidbody>();
				if (GameManager.instance.serverWorld == null) {
					if (_rb != null) {
						_rb.isKinematic = false;
						_rb.useGravity = true;
						_rb.interpolation = RigidbodyInterpolation.Interpolate;
					}

					ClientSnapPosition();
				}
			}
		}

		protected override void OnFogOfWarLocalVisibilityChanged() {
			if (_visual != null) {
				_visual.gameObject.SetActive(fogOfWarLocalVisibility);
			}
		}

		protected abstract void OnContact(ProjectileContact contact);

		public void HitIceBarrier() {
			_killedByIce = true;
			NetFlush();
			Destroy();
		}

		public void OnContactBegin(Collision collision) {
			if (collision.contacts.Length > 0) {
				var c = collision.contacts[0];

				var actor = collision.transform.FindServerActorUpwards();
				if (actor != null) {
					if (_contacts.Add(actor.go.transform)) {
						if (!ReferenceEquals(actor, instigatingActor) || (_selfHitTestTimeout <= 0f)) {
							OnContact(new ProjectileContact(actor, c));
						}
					}
				} else if (_contacts.Add(collision.transform)) {
					OnContact(new ProjectileContact(c));
				}
			}
		}

		public void OnContactEnd(Collision collision) {
			var actor = collision.transform.FindServerActorUpwards();
			if (actor != null) {
				_contacts.Remove(actor.go.transform);
			} else {
				_contacts.Remove(collision.transform);
			}
		}

		protected void ContactFx(ProjectileContact contact) {
			ContactFx(contact.contact, contact.normal, GetContactMaterial(contact));
		}

		protected void ContactFx(Vector3 pos, Vector3 normal, StaticAssetRef<PhysicalMaterialClass> matRef) {
			if (!hasAuthority) {
				((Client.ClientWorld)world).SpawnContactFx(projectileClass.physicalMaterial, matRef, pos, normal);
			}
		}

		[RPC(ERPCDomain.Multicast)]
		protected void Multicast_ContactFx(Vector3 pos, Vector3 normal, StaticAssetRef<PhysicalMaterialClass> matRef) {
			if (hasAuthority) {
				if ((projectileClass.physicalMaterial != null) && (matRef.obj != null)) {
					rpc_Multicast_ContactFx.Invoke(pos, normal, matRef);
				}
			} else {
				ContactFx(pos, normal, matRef);
			}
		}

		protected PhysicalMaterialClass GetContactMaterial(ProjectileContact contact) {
			var q = contact.hit.GetComponent<PhysicalMaterialQuery>();
			if (q != null) {
				return q.GetMaterialAtPoint(contact.contact);
			}
			return null;
		}

		public sealed override void EnableSelectionHitTest(bool enable) { }

		public sealed override bool RaycastSpawnBloodSpray(Vector3 contactLocation, Vector3 direction, bool bidrectionalHitTest, out RaycastHit hitInfo) {
			hitInfo = new RaycastHit();
			return false;
		}

		public sealed override GameObject SpawnBloodSpray(Transform t, Vector3 pos, Quaternion rot) { return null; }

		public override bool CapsuleCast(Vector3 p1, Vector3 p2, float radius, Vector3 direction, float maxDistance, out RaycastHit hitInfo) {
			hitInfo = new RaycastHit();
			return false;
		}

		public override bool SphereCast(Vector3 p, float radius, Vector3 direction, float maxDistance, out RaycastHit hitInfo) {
			hitInfo = new RaycastHit();
			return false;
		}

		public override bool Raycast(Vector3 p, Vector3 direction, float maxDistance, out RaycastHit hitInfo) {
			hitInfo = new RaycastHit();
			return false;
		}

		protected override void ClientFlashHudOnDamage(float amount) {}
		protected override void ClientFlashHudOnDeath() {}

		protected override void Dispose(bool disposing) {
			if (disposing && !hasAuthority && _killedByIce && (go != null)) { // nref check go in-case this hit ice the same frame it was spawned.
				if ((projectileClass.killedByIcePrefab != null) && (projectileClass.killedByIcePrefab.Load() != null)) {
					GameObject.Instantiate(projectileClass.killedByIcePrefab.Load(), go.transform.position, go.transform.rotation);
				}
			}

			base.Dispose(disposing);
		}

		public ProjectileClass projectileClass {
			get {
				return _projectileClass.obj;
			}
		}

		public Transform visual {
			get {
				return _visual;
			}
		}

		public override ServerPlayerController serverOwningPlayer {
			get {
				return _player;
			}
		}

		public ActorWithTeam instigatingActor {
			get {
				return _instigator;
			}
		}

		public override Vector3 projectileTargetPos {
			get {
				return go.transform.position;
			}
		}

		public override float meleeAttackRadius {
			get {
				return 0;
			}
		}

		public override Team team {
			get {
				return _team;
			}
		}

		public override AttachmentLocations attachmentLocations {
			get {
				return new AttachmentLocations();
			}
		}

		protected Rigidbody rb {
			get {
				return _rb;
			}
		}

		protected Vector3 serverVelocity {
			get {
				return _serverVelocity.value;
			}
		}

		protected override float defaultFogOfWarObjectRadius {
			get {
				return 0f;
			}
		}

		protected override float defaultFogOfWarSightRadius {
			get {
				return 0f;
			}
		}

		protected override bool defaultFogOfWarCanSeeUnderwater {
			get {
				return false;
			}
		}

		protected override EFogOfWarTest defaultFogOfWarTest {
			get {
				return EFogOfWarTest.SightCheck;
			}
		}
		
		protected override void ServerAddActorToFogOfWar() {
			// projectiles don't reveal fog of war
		}

		protected override bool fogOfWarVisibleWhenDead {
			get {
				return false;
			}
		}

		public float projectileDamageScale {
			get {
				return _damageScale;
			}
		}

		public float projectileSpellPower {
			get {
				return _spellPower;
			}
		}

		public Vector3 launchPos {
			get {
				return _launchPos;
			}
		}
	}

	public abstract class DirectProjectileActor : ProjectileActor {
		const float SINK_TIME = 4f;

		readonly ActorRPC<DamageableActor, Vector3, Vector3> rpc_Multicast_HitActor;
		readonly ActorRPC<Vector3, Vector3, bool> rpc_Multicast_HitGround;

		bool _hitActor;
		bool _destroyOnTearOff;
		bool _forceRelevancy;
		DamageableActor _attached;
		Debris _debris;
		BreakIntoPieces _break;
		
		public DirectProjectileActor() {
			rpc_Multicast_HitActor = BindRPC<DamageableActor, Vector3, Vector3>(Multicast_HitActor);
			rpc_Multicast_HitGround = BindRPC<Vector3, Vector3, bool>(Multicast_HitGround);
		}
				
		public override void Tick() {
			base.Tick();

			if (!(hasAuthority || pendingKill) && netTornOff) {
				if (_attached != null) {
					if (_attached.pendingKill) {
						Destroy();
					} else if (_attached.dead) {
						DetachGameObject();
						Destroy();
					}
				}
			}
        }

		protected override void OnContact(ProjectileContact contact) {

			if (hasAuthority && !pendingKill) {
				_forceRelevancy = true;

				var actor = contact.actor as DamageableActor;

				if ((actor != null) && actor.replicates && actor.projectileIceBarrier) {
					var center = rb.GetComponent<Collider>().GetWorldSpaceCenter();
					DealDamage(actor, center, serverVelocity);
					HitIceBarrier();
					return;
				}

				var bounce = !projectileClass.embed || contact.hit.CompareTag(Tags.ProjectileBounce);
				var willTearOff = projectileClass.embed;

				NetFlush();

				if ((actor != null) && actor.replicates) {

					willTearOff = willTearOff || ((projectileClass._break == EProjectileBreak.BreakOnActor) || (projectileClass._break == EProjectileBreak.Always));

					var center = rb.GetComponent<Collider>().GetWorldSpaceCenter();
					if (willTearOff) {
						Multicast_HitActor(actor, center, serverVelocity);
					}
					DealDamage(actor, center, serverVelocity);
				} else {

					willTearOff = willTearOff || ((projectileClass._break == EProjectileBreak.BreakOnDefaultTerrainBlocker) || (projectileClass._break == EProjectileBreak.Always));

					if (willTearOff) {
						Multicast_ContactFx(contact.contact, contact.normal, GetContactMaterial(contact));
						// bounce or embed inside whatever object this is.

						if (bounce) {
							rb.transform.position = contact.contact;
						} else {
							var n = serverVelocity.normalized * Mathf.Lerp(projectileClass.penetration.x, projectileClass.penetration.y, GameManager.instance.randomNumber);
							rb.transform.position += n;
						}

						Multicast_HitGround(rb.transform.position, serverVelocity, bounce);
					}

					if (!bounce && (projectileClass.groundHitAOE != null) && (projectileClass.canProcGroundAOEInWater || !inWater)) {
						var instigator = instigatingActor as Unit;
						//if ((instigator != null) && (instigator.dead || instigator.pendingKill)) {
						//	instigator = null;
						//}

						//var aoe = projectileClass.groundHitAOE.Spawn<Spells.AreaOfEffectActor>(level, projectileDamageScale, (Server.ServerWorld)world, serverOwningPlayer, instigator, null, team);
						//aoe.ServerPlace(rb.transform.position, 0);
					}
				}

				if (willTearOff) {
					NetTearOff();
				}
										
				Destroy();
			}
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();

			if ((rb != null) && (GameManager.instance.serverWorld == null)) {
				rb.detectCollisions = false;
			}

			if (go != null) { // can happen during player-hosted game
				_debris = go.GetComponent<Debris>();
				_break = go.GetComponent<BreakIntoPieces>();
			}
		}

		public override bool IsNetRelevantFor(ActorReplicationChannel channel) {
			return _forceRelevancy || base.IsNetRelevantFor(channel);
		}

		public override void UpdateLocalFogOfWarVisibility() {
			if (_attached == null) {
				base.UpdateLocalFogOfWarVisibility();
			} else {
				if (fogOfWarUpdateFrame != Time.frameCount) {
					fogOfWarUpdateFrame = Time.frameCount;
					_attached.UpdateLocalFogOfWarVisibility();
					var vis = _attached.fogOfWarLocalVisibility;
					if (vis != fogOfWarLocalVisibility) {
						fogOfWarLocalVisibility = vis;
						OnFogOfWarLocalVisibilityChanged();
					}
				}
			}
		}

		void DealDamage(DamageableActor actor, Vector3 location, Vector3 velocity) {
			if (projectileClass.damage.baseDamageAmount != 0f) {
				var dist = (location-launchPos).magnitude;

				DamageEvent damage = new DamageEvent();
				damage.pain = EUnitActionCueSlotExplosion.MidCenter;
				damage.blockParry = 0;
				damage.damageLevel = level;
				damage.damageSpellPower = projectileSpellPower;
				damage.gibForce = (projectileClass.damage.baseDamageAmount+(dist*projectileClass.damageBonusPerMeterTraveled));
				damage.damage = damage.gibForce*projectileDamageScale;
				damage.damageClass = projectileClass.damage.damageClass;
				damage.distance = dist;
				damage.effectingActor = this;
				damage.instigatingPlayer = serverOwningPlayer;
				damage.instigatingActor = (Actor)instigatingActor;
				damage.instigatingTeam = team;
				damage.targetActor = actor;
				damage.targetPlayer = actor.serverOwningPlayer;

				var force = Mathf.Lerp(projectileClass.impactForce.x, projectileClass.impactForce.y, GameManager.instance.randomNumber);
				if (force > 0f) {
					ImpactForce impactForce = new ImpactForce();
					impactForce.force = velocity.normalized * force * 100;
					impactForce.location = location;
					damage.killInfo = new SimulatedKillInfo(new PhysicalDamageForces(impactForce));
				}

				ServerExecuteDamage((Server.ServerWorld)world, damage);
			}
		}

		protected sealed override void ClientSimulateKill(SimulatedKillInfo ki) {}
		protected sealed override void ServerSimulateKill(SimulatedKillInfo ki) {}

		protected override void OnRep_serverPosition() {
			if (!_hitActor) {
				base.OnRep_serverPosition();
			}
		}

		public override void ClientFixedUpdate(float dt) {
			if (!_hitActor) {
				base.ClientFixedUpdate(dt);
			}
		}

		[RPC(ERPCDomain.Multicast, Reliable = true)]
		void Multicast_HitActor(DamageableActor actor, Vector3 location, Vector3 velocity) {
			if (hasAuthority) {
				rpc_Multicast_HitActor.Invoke(actor, location, velocity);
			} else {
				if (projectileClass.embed) {
					if (actor != null) {
						_hitActor = CollisionTrace(actor, location, velocity);
						_destroyOnTearOff = !_hitActor;

						if (_hitActor) {
							if (((projectileClass._break == EProjectileBreak.BreakOnActor) || (projectileClass._break == EProjectileBreak.Always)) && (_break != null)) {
								_break.Break(SINK_TIME);
							}
							_attached = actor;
							UpdateLocalFogOfWarVisibility();
							SetLifetime(30f);
							DestroyComponents(false, true);
						}
					} else {
						_destroyOnTearOff = true;
					}
				} else {
					_destroyOnTearOff = true;
					if (_break != null) {
						_break.Break(SINK_TIME);
					}
					if (_debris != null) {
						_debris.Throw(SINK_TIME, rb.velocity);
						DetachGameObject();
					}
				}
			}
		}

		[RPC(ERPCDomain.Multicast, Reliable = true)]
		void Multicast_HitGround(Vector3 location, Vector3 velocity, bool bounce) {
			if (hasAuthority) {
				rpc_Multicast_HitGround.Invoke(location, velocity, bounce);
			} else {
				_hitActor = true;
				_destroyOnTearOff = true;
				if (rb != null) {
					if (((projectileClass._break == EProjectileBreak.BreakOnDefaultTerrainBlocker) || (projectileClass._break == EProjectileBreak.Always)) && (_break != null)) {
						_break.Break(SINK_TIME);
					}
					
					if (bounce) {
						if (_debris != null) {
							_debris.Throw(SINK_TIME, rb.velocity);
						}
					} else {
						rb.isKinematic = true;
						rb.detectCollisions = false;
						rb.velocity = Vector3.zero;
						rb.transform.position = location;
						if (velocity.sqrMagnitude > 0.001f) {
							rb.transform.rotation = Quaternion.LookRotation(velocity.normalized);
						}
					}

					DestroyComponents(bounce, false);
					DetachGameObject();
				}
			}
		}

		bool CollisionTrace(DamageableActor actor, Vector3 location, Vector3 velocity) {

			if (rb == null) {
				return false;
			}

			rb.velocity = Vector3.zero;
			rb.isKinematic = true;
			rb.detectCollisions = false;

			float radius = rb.GetComponent<Collider>().GetWorldSpaceRadius();

			RaycastHit hitInfo;
			var didContactFX = false;
			var vn = velocity.normalized;

			var spawnBloodFX = projectileClass.damage.damageClass.spawnBloodFX && actor.isNetRelevant;

			if (spawnBloodFX ? actor.RaycastSpawnBloodSpray(location, vn, false, out hitInfo) : actor.Raycast(location, vn, Mathf.Infinity, out hitInfo)) {
				if (actor.isNetRelevant) {
					ContactFx(hitInfo.point, -vn, actor.physicalMaterial);
					didContactFX = true;
				}
				hitInfo.point += vn * Mathf.Lerp(projectileClass.penetration.x, projectileClass.penetration.y, GameManager.instance.randomNumber);
				rb.transform.parent = hitInfo.collider.transform;
				rb.transform.position = hitInfo.point - vn*radius;
				rb.transform.rotation = Quaternion.LookRotation(vn);
				return true;
			} else {
				var locations = actor.attachmentLocations;
				if (locations.chest != null) { // veer towards chest for contact trace
					var vx = (locations.chest.position - location).normalized;
					if (spawnBloodFX ? actor.RaycastSpawnBloodSpray(location, vx, false, out hitInfo) : actor.Raycast(location, vx, Mathf.Infinity, out hitInfo)) {
						if (actor.isNetRelevant) {
							ContactFx(hitInfo.point, -vn, actor.physicalMaterial);
							didContactFX = true;
						}
						hitInfo.point += vn * Mathf.Lerp(projectileClass.penetration.x, projectileClass.penetration.y, GameManager.instance.randomNumber);
						rb.transform.parent = locations.chest.transform;
						rb.transform.position = hitInfo.point - vn*radius;
						rb.transform.rotation = Quaternion.LookRotation(vn);
						return true;
					}
				}
			}

			if (actor.isNetRelevant && !didContactFX) {
				ContactFx(location, -vn, actor.physicalMaterial);
			}

			return false;
		}

		void DestroyComponents(bool bounce, bool hitActor) {
			Utils.DestroyComponent(go.GetComponent<ColliderContactCallback>());

			var sound = go.GetComponent<SoundEntity>();
			if (sound != null) {
				Utils.DestroyComponent(sound);
			}
			
			go.DestroyComponentsInChildren<ParticleSystem>(1f);
			go.DestroyComponentsInChildren<TrailRenderer>(1f);
			//go.DestroyComponentsInChildren<BetterRotationScript>(1f);

			// extinguish fire arrows...
			if (bounce) {
				ExtinguishFlames();
			} else {
				go.DestroyChild("Flames");
			}

			if (!bounce) {
				if (_break != null) {
					Utils.DestroyComponent(_break);
					_break = null;
				}

				if (_debris != null) {
					Utils.DestroyComponent(_debris);
					_debris = null;
				}

				Utils.DestroyComponent(go.GetComponent<WaterSplashes>());
				Utils.DestroyComponent(go.GetComponent<Rigidbody>());
				Utils.DestroyComponent(go.GetComponent<Collider>());

				var water = go.FindChild("WaterInteraction");
				if (water != null) {
					if (hitActor) {
						Utils.DestroyGameObject(water); 
					} else {
						Utils.DestroyGameObject(water, 1f);
					}
				}
			}
		}

		void ExtinguishFlames() {
			go.DestroyChild("Flames");
			{
				var extinguish = go.FindChild("Extinguish");
				if (extinguish != null) {
					extinguish.SetActive(true);
				}
			}
		}

		protected override void OnEnterWaterVolume() {
			base.OnEnterWaterVolume();
			if (!hasAuthority) {
				ExtinguishFlames();
			}
		}

		protected override void OnNetTornOff() {
			base.OnNetTornOff();
			if (!hasAuthority) {
				if (_destroyOnTearOff) {
					Destroy();
				}
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);

			if ((_attached == null) && (visual != null)) {
				visual.gameObject.SetActive(true);
			}
		}

		new public DirectProjectileClass projectileClass {
			get {
				return (DirectProjectileClass)base.projectileClass;
			}
		}
	}

	public class BasicDirectProjectileActor : DirectProjectileActor {
		public override Type clientType {
			get {
				return typeof(BasicDirectProjectileActor);
			}
		}

		public override Type serverType {
			get {
				return typeof(BasicDirectProjectileActor);
			}
		}
	}

	public abstract class ExplosionProjectileActor : ProjectileActor {
		const int MAX_WATER_DUDS = 25;
		const float SINK_TIME = 2;
		const float SINK_SPEED = 1f;// Unit.RAGDOLL_FADEOUT_MOVE_SPEED;

		static List<ExplosionProjectileActor> _waterDuds = new List<ExplosionProjectileActor>();

		[Replicated(Notify = "OnRep_lit")]
		bool _lit;

		float _fuseTime;
		float _contactChance;
		float _originalDrag;
		float _sinkTime;
		bool _exploded;
		bool _contactExplode;
		bool _didContact;
		
		public static void ClearWaterDuds() {
			_waterDuds.Clear();
		}

		protected override void ServerInit() {
			base.ServerInit();
			lit = true;
			_fuseTime = Mathf.Lerp(projectileClass.fuseTime.x, projectileClass.fuseTime.y, GameManager.instance.randomNumber);
			_contactChance = projectileClass.explodeOnContactChance;

			if (rb != null) {
				_originalDrag = rb.drag;
			}
		}

		public override void Tick() {
			base.Tick();

			if (hasAuthority) {
				if (lit) {
					if (_fuseTime > 0f) {
						_fuseTime -= world.deltaTime;
						if (_fuseTime < 0f) {
							_fuseTime = 0f;
						}
					}

					if (inWater && projectileClass.dudInWater) {
						AddWaterDud();
					}
				}

				if (lit && _contactExplode && GameManager.instance.fixedUpdateDidRun) {
					ServerKill();
				}

				if (_fuseTime <= 0f) {
					if (projectileClass.fuseTime.y > 0f) {
						ServerKill();
					}
				}
			} else if (netTornOff) {
				if (_sinkTime > 0f) {
					_sinkTime -= world.deltaTime;
					if (go != null) {
						go.transform.position = go.transform.position - Vector3.up*SINK_SPEED*world.deltaTime;
					}
				} else {
					Destroy();
				}
			}
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();

			if (rb != null) {
				_originalDrag = rb.drag;
			}
		}

		protected override void OnContact(ProjectileContact contact) {
			if (hasAuthority) {
				_didContact = true;
				if (!dead) {
					if (dud) {
						Dud();
					} else if (lit) {
						if ((_contactChance > 0f) && ((GameManager.instance.randomNumber*100) <= _contactChance)) {
							_contactExplode = true;
						} else {
							_contactChance = Mathf.Clamp(_contactChance + projectileClass.additionalContactChance, 0, 100);
						}
					}
				}
			} else if (!dead) {
				ContactFx(contact);
			}
		}

		protected override void ClientSimulateKill(SimulatedKillInfo ki) {
			if ((go != null) && (projectileClass.explosionPrefab != null) && (projectileClass.explosionPrefab.Load() != null) && !GameManager.instance.serverPerfTest) {
				GameObject.Instantiate(projectileClass.explosionPrefab.Load(), go.transform.position, Quaternion.identity);
			}
		}

		protected override DamageResult ServerApplyDamage(DamageEvent damage, ActorPropertyInstance property, float amount, out float damageDone, float basePainChance, float scaledPainChance, DamageClass.Channel channel, DamageResult result) {
			if (!_didContact || (inWater && !projectileClass.canExplodeInWater)) {
				damageDone = 0f;
				return new DamageResult();
			}

			return base.ServerApplyDamage(damage, property, amount, out damageDone, basePainChance, scaledPainChance, channel, result);
		}

		protected override void ServerSimulateKill(SimulatedKillInfo ki) {
			NetFlush();
			ServerExplode();
			Multicast_ClientSimulateKill(ki);
			if (GameManager.instance.clientWorld != null) {
				SetLifetime(0.1f);
			} else {
				Destroy();
			}
		}

		protected override void OnEnterWaterVolume() {
			base.OnEnterWaterVolume();
			if ((rb != null) && (hasAuthority || (GameManager.instance.serverWorld == null))) {
				GameManager.instance.StartCoroutine(UpdateRBWaterPhysics());
			}
		}

		protected override void OnExitWaterVolume() {
			base.OnExitWaterVolume();
			
			if ((rb != null) && (hasAuthority || (GameManager.instance.serverWorld == null))) {
				rb.drag = _originalDrag;
			}

			if (hasAuthority) {
				RemoveWaterDud();
			}
		}

		System.Collections.IEnumerator UpdateRBWaterPhysics() {
			yield return new WaitForFixedUpdate();
			if (!pendingKill && (rb != null) && inWater) {
				rb.drag = projectileClass.dragInWater;
				rb.velocity = rb.velocity * projectileClass.velocityChangeInWater;
			}
		}

		void Dud() {
			lit = false;
		}

		void AddWaterDud() {
			Dud();

			_waterDuds.Add(this);

			if (_waterDuds.Count > MAX_WATER_DUDS) {
				var dud = _waterDuds[0];
				_waterDuds.RemoveAt(0);
				dud.Sink();
			}
		}

		void RemoveWaterDud() {
			_waterDuds.Remove(this);
		}

		void Sink() {
			NetTearOff();
			Destroy();
		}

		protected override void OnNetTornOff() {
			base.OnNetTornOff();
			if (rb != null) {
				rb.isKinematic = true;
			}
			_sinkTime = SINK_TIME;
		}

		void ServerExplode() {
			if (!_exploded) {
				_exploded = true;
				DamageEvent damage = new DamageEvent();
				damage.pain = EUnitActionCueSlotExplosion.MidCenter;
				damage.gibForce = projectileClass.damage.baseDamageAmount;
				damage.damage = projectileClass.damage.baseDamageAmount*projectileDamageScale;
				damage.damageClass = projectileClass.damage.damageClass;
				damage.damageClass = projectileClass.damage.damageClass;
				damage.ignoredActors = new List<Actor>();
				damage.ignoredActors.Add(this);
				damage.effectingActor = this;
				damage.instigatingActor = (Actor)instigatingActor;
				damage.instigatingPlayer = serverOwningPlayer;
				damage.instigatingTeam = team;
				damage.hitLocation = go.transform.position;
				ServerExecuteDamage(damage);
			}
		}

		new public ExplosionProjectileClass projectileClass {
			get {
				return (ExplosionProjectileClass)base.projectileClass;
			}
		}

		protected virtual void OnRep_lit() {
			if (go != null) {
				var fuse = go.FindChild("Fuse");
				var dud = go.FindChild("Dud");

				if (fuse != null) {
					fuse.SetActive(lit);
				}
				if (dud != null) {
					dud.SetActive(!lit);
				}
			}
		}

		protected bool lit {
			get {
				return _lit;
			}

			set {
				_lit = value;
			}
		}

		public bool dud {
			get;
			set;
		}

		protected override bool fogOfWarVisibleWhenDead {
			get {
				return true;
			}
		}
	}

	public class BasicExplosionProjectileActor : ExplosionProjectileActor {
		public override Type clientType {
			get {
				return typeof(BasicExplosionProjectileActor);
			}
		}

		public override Type serverType {
			get {
				return typeof(BasicExplosionProjectileActor);
			}
		}
	}

	public class InstantExplodeProjectileActor : ExplosionProjectileActor {

		public override void Tick() {
			base.Tick();

			if (hasAuthority) {
				ServerKill();
			}
		}

		public override Type clientType {
			get {
				return typeof(InstantExplodeProjectileActor);
			}
		}

		public override Type serverType {
			get {
				return typeof(InstantExplodeProjectileActor);
			}
		}
	}
}