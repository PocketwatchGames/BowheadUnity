// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Bowhead.Actors.Spells;

namespace Bowhead.Actors {
	public enum EImmobilizeEffect {
		None,
		Immobilize,
		Stun,
		Freeze
	}

	[Flags]
	public enum EUnitActionCueSlot {
		HighLeft = 0x1,
		HighRight = 0x2,
		HighCenter = 0x4,
		MidLeft = 0x8,
		MidRight = 0x10,
		MidCenter = 0x20,
		LowLeft = 0x40,
		LowRight = 0x80,
		LowCenter = 0x100
	}

	[Flags]
	public enum EUnitActionCueSlotExplosion {
		HighLeft = 0x1,
		HighRight = 0x2,
		HighCenter = 0x4,
		MidLeft = 0x8,
		MidRight = 0x10,
		MidCenter = 0x20,
		LowLeft = 0x40,
		LowRight = 0x80,
		LowCenter = 0x100,
		ExplosionFront = 0x200,
		ExplosionBack = 0x400
	}

	[Flags]
	public enum EBlockParryDodge {
		Block = 0x1,
		Parry = 0x2,
		Dodge = 0x4
	}

	[Flags]
	public enum EBlockParry {
		Block = 0x1,
		Parry = 0x2
	}

	public struct AttachmentLocations {
		public Transform head;
		public Transform chest;
		public Transform waist;
		public Transform leftHand;
		public Transform rightHand;
		public Transform leftFoot;
		public Transform rightFoot;
		public Transform feet;
	}

	public struct ConstructDamagableActorClassParams {
		public readonly ActorMetaClass metaClass;
		public readonly ActorProperty[] properties;
		public readonly DamageClass.Resistance[] resistances;
		public readonly ActorProperty health;
		public readonly PhysicalMaterialClass physicalMaterial;
		public readonly XPCurve damageCurve;
		public readonly int powerScale;

		public ConstructDamagableActorClassParams(ActorMetaClass metaClass, ActorProperty[] properties, DamageClass.Resistance[] resistances, ActorProperty health, PhysicalMaterialClass physicalMaterial, XPCurve damageCurve, int powerScale) {
			this.metaClass = metaClass;
			this.properties = properties;
			this.resistances = resistances;
			this.health = health;
			this.physicalMaterial = physicalMaterial;
			this.damageCurve = damageCurve;
			this.powerScale = powerScale;
		}

		public void CheckValid() {
			if (metaClass == null) {
				throw new Exception("Invalid actor meta class constructor parameters!");
			}
		}
    }

#if UNITY_EDITOR
	[HideInInspector] // Hide from FlowCanvas inspector
#endif
	public abstract class DamageableActor : Actor, FogOfWarActor, RagdollController {
		public const float FOGOFWAR_FADE_TIME = 1f;

		List<Spell> _activeSpells = new List<Spell>();
		List<Spell> _workingSpells = new List<Spell>();
		List<SpellEffectActor> _spellEffects = new List<SpellEffectActor>();
		ReadOnlyCollection<SpellEffectActor> _roSpellEffects;
		ReadOnlyCollection<Spell> _roSpells;
		ReadOnlyCollection<StaticAssetRef<SpellClass>> _roReplSpells;
		ReadOnlyCollection<float> _roReplSpellPower;
		ActorMetaClass _metaClass;
		ActorProperty[] _properties;
		ActorPropertyInstance[] _propertyInstances;
		ImmutableActorPropertyInstance[] _immutablePropertyInstances;
		ActorPropertyInstance _mutableHealth;
		ImmutableActorPropertyInstance _immutableHealth;
		ActorProperty _health;
		ReadOnlyCollection<ActorPropertyInstance> _roProperties;
		ReadOnlyCollection<ImmutableActorPropertyInstance> _roImmutableProperties;
		Stack<Actor> _damageInstigatorStack = new Stack<Actor>();
		DictionaryList<DamageMetaClass, Resistance> _resistances;
		PhysicalMaterialClass _physicalMaterial;
		XPCurve _damageCurve;
		float _fogOfWarLocalAlpha;
		float _prevHealth;
		float _damageScale;
		float _accuracyBonus;
		float _dudChanceModifier;
		int _waterCount;
		HashSet<SpellClass> _spellStack = new HashSet<SpellClass>();

		protected struct DamageResult {
			public bool damaged;
			public bool healed;
			public bool hit;
			public bool pain;
			public bool fatal;

			public void Append(DamageResult other) {
				damaged |= other.damaged;
				healed |= other.healed;
				fatal |= other.fatal;
				hit |= other.hit;
			}
		}

		public class Resistance {
			public DamageClass.Resistance original;
			public float parryDamageScale;
			public float parryMaxDamageReduction;
			public float resistChance;
			public float resistDamageScale;
			public float resistMaxDamageReduction;
			public float blockDamageScale;
			public float blockMaxDamageReduction;

			public bool transient {
				get;
				private set;
			}

			public bool valid {
				get;
				private set;
			}

			public Resistance(DamageClass.Resistance original) {
				this.original = original;
				transient = original == null;
				valid = !transient;
				Reset();
			}

			void Reset() {
				if (original != null) {
					parryDamageScale = original.parryDamageScale;
					parryMaxDamageReduction = original.parryMaxDamageReduction;
					resistChance = original.resistChance;
					resistDamageScale = original.resistDamageScale;
					resistMaxDamageReduction = original.resistMaxDamageReduction;
					blockDamageScale = original.blockDamageScale;
					blockMaxDamageReduction = original.blockMaxDamageReduction;
				} else {
					parryDamageScale = 1f;
					parryMaxDamageReduction = 0f;
					resistChance = 0f;
					resistDamageScale = 1f;
					resistMaxDamageReduction = 0f;
					blockDamageScale = 1f;
					blockMaxDamageReduction = 0f;
				}
			}

			public void Stack(DamageClass.Resistance other) {
				if (transient && (original == null)) {
					original = other;
					Reset();
					valid = true;
				} else {
					Max(other);
				}
			}

			void Max(DamageClass.Resistance original) {
				parryDamageScale = Mathf.Min(parryDamageScale, original.parryDamageScale);
				if (parryMaxDamageReduction != 0f) {
					if (original.parryMaxDamageReduction > 0f) {
						parryMaxDamageReduction = Mathf.Max(parryMaxDamageReduction, original.parryMaxDamageReduction);
					} else {
						parryMaxDamageReduction = original.parryMaxDamageReduction;
					}
				}

				resistChance = Mathf.Max(resistChance, original.resistChance);
				resistDamageScale = Mathf.Min(resistDamageScale, original.resistDamageScale);
				if (resistMaxDamageReduction != 0f) {
					if (original.resistMaxDamageReduction > 0f) {
						resistMaxDamageReduction = Mathf.Max(resistMaxDamageReduction, original.resistMaxDamageReduction);
					} else {
						resistMaxDamageReduction = original.resistMaxDamageReduction;
					}
				}

				blockDamageScale = Mathf.Min(blockDamageScale, original.blockDamageScale);
				if (blockMaxDamageReduction != 0f) {
					if (original.blockMaxDamageReduction > 0f) {
						blockMaxDamageReduction = Mathf.Max(blockMaxDamageReduction, original.blockMaxDamageReduction);
					} else {
						blockMaxDamageReduction = original.blockMaxDamageReduction;
					}
				}
			}

			public void ResetTransient() {
				if (transient) {
					original = null;
					valid = false;
				}
				Reset();
			}
		}

		[Replicated(Notify = "OnRep_replicatedProperties")]
		ReplicatedActorProperties _replicatedProperties;
		[Replicated(UpdateRate = 0.3f, Notify = "OnRep_replSpells")]
		List<StaticAssetRef<SpellClass>> _replSpells = new List<StaticAssetRef<SpellClass>>();
		[Replicated(UpdateRate = 0.3f)]
		List<float> _replSpellPower = new List<float>();

		[Replicated]
		float _fogOfWarSightRadius;
		[Replicated]
		float _fogOfWarObjectRadius;
		[Replicated]
		byte _fogOfWarTest;
#if BACKEND_SERVER
		[Replicated(Condition = EReplicateCondition.InitialOnly, Notify = "OnRep_level")]
#else
		[Replicated(Notify = "OnRep_level")]
#endif
		byte _level;
		[Replicated]
		bool _fogOfWarCanSeeUnderwater;

		float _fogOfWarMaxVisRadius;
		float _fogOfWarUnderwaterMaxVisRadius;
		float _spellPower;
		bool _fogOfWarLocalVisibility;

		readonly ActorRPC<SimulatedKillInfo> rpc_Multicast_ClientSimulateKill;
		readonly ActorRPC<byte> rpc_Multicast_ClientSetLevel;

		public abstract Vector3 projectileTargetPos { get; }
		public abstract float meleeAttackRadius { get; }
		public abstract Team team { get; }
		public abstract Server.Actors.ServerPlayerController serverOwningPlayer { get; }
		public abstract AttachmentLocations attachmentLocations { get; }
		protected abstract float defaultFogOfWarSightRadius { get; }
		protected abstract float defaultFogOfWarObjectRadius { get; }
		protected abstract bool defaultFogOfWarCanSeeUnderwater { get; }
		protected abstract EFogOfWarTest defaultFogOfWarTest { get; }
		protected abstract bool fogOfWarVisibleWhenDead { get; }
		public virtual bool projectileIceBarrier { get { return false; } }
		public abstract void EnableSelectionHitTest(bool enable);
		public abstract bool RaycastSpawnBloodSpray(Vector3 contactLocation, Vector3 direction, bool bidrectionalHitTest, out RaycastHit hitInfo);
		public abstract GameObject SpawnBloodSpray(Transform t, Vector3 pos, Quaternion rot);
		public abstract bool CapsuleCast(Vector3 p1, Vector3 p2, float radius, Vector3 direction, float maxDistance, out RaycastHit hitInfo);
		public abstract bool SphereCast(Vector3 p, float radius, Vector3 direction, float maxDistance, out RaycastHit hitInfo);
		public abstract bool Raycast(Vector3 p, Vector3 direction, float maxDistance, out RaycastHit hitInfo);
		protected abstract void ServerSimulateKill(SimulatedKillInfo ki);
		protected abstract void ClientSimulateKill(SimulatedKillInfo ki);

		protected virtual void OnFogOfWarLocalVisibilityChanged() { }
		protected virtual void OnFogOfWarLocalAlphaChanged(bool visChange) { }

		protected virtual float defaultFogOfWarMaxVisRadius {
			get {
				return 0f;
			}
		}

		protected virtual float defaultFogOfWarUnderwaterMaxVisRadius {
			get {
				return 0f;
			}
		}

		public virtual bool targetable {
			get {
				return true;
			}
		}

		public DamageableActor() {
			_roSpellEffects = new ReadOnlyCollection<SpellEffectActor>(_spellEffects);
			_roSpells = new ReadOnlyCollection<Spell>(_activeSpells);
			_roReplSpells = new ReadOnlyCollection<StaticAssetRef<SpellClass>>(_replSpells);
			_roReplSpellPower = new ReadOnlyCollection<float>(_replSpellPower);
			_level = 1;
			_damageScale = 1;
			_spellPower = 1;
			rpc_Multicast_ClientSimulateKill = BindRPC<SimulatedKillInfo>(Multicast_ClientSimulateKill);
			rpc_Multicast_ClientSetLevel = BindRPC<byte>(Multicast_ClientSetLevel);
		}

		protected virtual void ConstructDamagableActorClass(ConstructDamagableActorClassParams parms) {
			parms.CheckValid();

			powerScale = parms.powerScale;

			_physicalMaterial = parms.physicalMaterial;

			var properties = parms.properties ?? new ActorProperty[0];
			var resistances = parms.resistances ?? new DamageClass.Resistance[0];

			_metaClass = parms.metaClass;
			_properties = properties;
			_health = parms.health;

			if (hasAuthority) {
				_propertyInstances = new ActorPropertyInstance[properties.Length];
				_immutablePropertyInstances = new ImmutableActorPropertyInstance[properties.Length];

				for (int i = 0; i < _propertyInstances.Length; ++i) {
					var p = properties[i];
					_propertyInstances[i] = new ActorPropertyInstance(p, i, i);
					_immutablePropertyInstances[i] = _propertyInstances[i];

					if (p == _health) {
						_mutableHealth = _propertyInstances[i];
						_immutableHealth = _mutableHealth;
					}
				}

				_roProperties = new ReadOnlyCollection<ActorPropertyInstance>(_propertyInstances);
				_roImmutableProperties = new ReadOnlyCollection<ImmutableActorPropertyInstance>(_immutablePropertyInstances);
				_replicatedProperties = new ReplicatedActorProperties(_propertyInstances);

				_resistances = new DictionaryList<DamageMetaClass, Resistance>();
				for (int i = 0; i < resistances.Length; ++i) {
					var r = resistances[i];
					if (r.affectedDamage != null) {
						_resistances.Add(r.affectedDamage, new Resistance(r));
					}
				}

				_fogOfWarSightRadius = defaultFogOfWarSightRadius;
				_fogOfWarObjectRadius = defaultFogOfWarObjectRadius;
				_fogOfWarMaxVisRadius = defaultFogOfWarMaxVisRadius;
				_fogOfWarUnderwaterMaxVisRadius = defaultFogOfWarUnderwaterMaxVisRadius;
				_fogOfWarCanSeeUnderwater = defaultFogOfWarCanSeeUnderwater;
				_fogOfWarTest = (byte)defaultFogOfWarTest;

				PostConstructProperties();
			} else if (_immutablePropertyInstances != null) {
				PostConstructProperties();
			}
		}

		public override void PostConstruct() {
			base.PostConstruct();
			InitFogOfWar();
		}

		protected void InitFogOfWar() {
			if (team != null) {
				if (replicates && hasAuthority) {
					ServerAddActorToFogOfWar();
				} else if (!hasAuthority) {
					UpdateLocalFogOfWarVisibility();
					if (fogOfWarLocalVisibility) {
						_fogOfWarLocalAlpha = 1f;
					} else {
						_fogOfWarLocalAlpha = 0f;
					}
				}
			}
		}

		protected void ServerRemoveFromFogOfWar() {
			serverFogOfWar.RemoveActor(this);
		}

		protected override void OnDestroy() {
			base.OnDestroy();
			if (hasAuthority) {
				for (int i = 0; i < _activeSpells.Count; ++i) {
					_activeSpells[i].Dispose();
				}
			}
		}

		protected override void Dispose(bool disposing) {
			if (hasAuthority) {
				ServerRemoveFromFogOfWar();
			}
			base.Dispose(disposing);
		}

		protected virtual void ServerAddActorToFogOfWar() {
			serverFogOfWar.AddActor(this);
		}

		protected void ForceUpdateLocalFogOfWarVisibility() {
			fogOfWarUpdateFrame = -1;
			UpdateLocalFogOfWarVisibility();
		}

		public virtual void UpdateLocalFogOfWarVisibility() {

			if (!hasAuthority && (team != null)) {

				if (fogOfWarUpdateFrame == Time.frameCount) {
					return;
				}

				fogOfWarUpdateFrame = Time.frameCount;

				bool vis = false;

				if (dead && fogOfWarVisibleWhenDead) {
					vis = true;
				} else {
					vis = isNetRelevant;
				}

				vis = vis || Client.Actors.ClientPlayerController.debugFogOfWar;

				if (vis != fogOfWarLocalVisibility) {
					fogOfWarLocalVisibility = vis;
					OnFogOfWarLocalVisibilityChanged();
				}
			}
		}

		public override bool IsNetRelevantFor(ActorReplicationChannel channel) {
			if (team == null) {
				if (fogOfWarTest == EFogOfWarTest.AlwaysVisible) {
					return true;
				}
			}

			var player = (Server.Actors.ServerPlayerController)channel.owningPlayer;
			if (player.team == null) {
				return true;
			}
						
			//if ((dead && fogOfWarVisibleWhenDead) || (player.team.allUnitsAreDead && ((Server.ServerWorld)world).gameMode.liftFogOfWarAtEndOfMatch)) {
			//	return true;
			//}

			return (team != null) && serverFogOfWar.CanBeSeenByTeam(player.team, this);
		}

		void OnRep_replicatedProperties() {
			if (_immutablePropertyInstances == null) {
				_immutablePropertyInstances = new ImmutableActorPropertyInstance[_replicatedProperties.count];

				for (int i = 0; i < _immutablePropertyInstances.Length; ++i) {
					var p = _replicatedProperties.ClientNewPropertyInstance(_properties, i);
					_immutablePropertyInstances[i] = p;
					_replicatedProperties.ClientOnRepProperty(p, i);

					if (p.property == _health) {
						_mutableHealth = p;
						_immutableHealth = p;
					}
				}

				_roImmutableProperties = new ReadOnlyCollection<ImmutableActorPropertyInstance>(_immutablePropertyInstances);

				if (_metaClass != null) { // ConstructActorMetaClass() was run
					PostConstructProperties();
				}

				for (int i = 0; i < _immutablePropertyInstances.Length; ++i) {
					OnRepActorProperty(_properties[_immutablePropertyInstances[i].serverIndex], _immutablePropertyInstances[i]);
				}
			} else {
				for (int i = 0; i < _immutablePropertyInstances.Length; ++i) {
					var p = _immutablePropertyInstances[i];
					if (_replicatedProperties.ClientOnRepProperty((ActorPropertyInstance)p, i)) {
						OnRepActorProperty(_properties[p.serverIndex], p);
					}
				}
			}
		}

		protected virtual void PostConstructProperties() {}

		protected virtual void OnRepActorProperty(ActorProperty property, ImmutableActorPropertyInstance instance) {
			if (instance == health) {
				if (instance.value < _prevHealth) {
					ClientFlashHudOnDamage(_prevHealth - instance.value);
				}
				_prevHealth = instance.value;
			}
		}

		protected virtual void ClientFlashHudOnDamage(float amount) {
			if ((Client.Actors.ClientPlayerController.localPlayer != null) && (Client.Actors.ClientPlayerController.localPlayer.team == team)) {
				if (Client.Actors.ClientPlayerController.localPlayer.gameState.hud != null) {
					Client.Actors.ClientPlayerController.localPlayer.gameState.hud.DamageBlend(amount);
				}
			}
		}

		public bool dead {
			get;
			protected set;
		}

		public int powerScale {
			get;
			private set;
		}

		public void RaycastSpawnBloodSpray(Vector3 contactLocation, Vector3 direction, bool bidirectionalHitTest) {
			RaycastHit unused;
			RaycastSpawnBloodSpray(contactLocation, direction, bidirectionalHitTest, out unused);
		}

		public bool CheckFriendlyFire(EFriendlyFire friendlyFire, ActorWithTeam instigator, ActorWithTeam player) {
			if (IsFriendly(instigator, player)) {
				return (friendlyFire&EFriendlyFire.Friends) == EFriendlyFire.Friends;
			}
			return (friendlyFire&EFriendlyFire.Enemies) == EFriendlyFire.Enemies;
		}

		public bool CheckFriendlyFire(EFriendlyFire friendlyFire, Team team) {
			if (IsFriendly(team)) {
				return (friendlyFire&EFriendlyFire.Friends) == EFriendlyFire.Friends;
			}
			return (friendlyFire&EFriendlyFire.Enemies) == EFriendlyFire.Enemies;
		}

		public bool IsFriendly(Team team) {
#if NO_FRIENDLIES
			return false;
#else
			if (ReferenceEquals(this.team, team)) {
				return true;
			}
			if (this.team.isMonsterTeam || team.isMonsterTeam) {
				return false;
			}
			return this.team.isNPCTeam || team.isNPCTeam;
#endif
		}

		public bool IsEnemy(Team team) {
			return !IsFriendly(team);
		}

		public bool IsFriendly(ActorWithTeam actor) {
			return IsFriendly(actor.team);
		}

		public bool IsFriendly(ActorWithTeam actor, ActorWithTeam player) {
			if (actor != null) {
				return IsFriendly(actor.team);
			} else if (player != null) {
				return IsFriendly(player.team);
			}
			return false;
		}

		public bool IsEnemy(ActorWithTeam actor, ActorWithTeam player) {
			return !IsFriendly(actor, player);
		}

		public bool IsEnemy(ActorWithTeam actor) {
			return !IsFriendly(actor);
		}

		public virtual void Interrupt(float duration, EImmobilizeEffect immobilize, EUnitActionCueSlotExplosion pain) { }

		public Resistance GetResistance(DamageMetaClass damageClass) {
			Resistance r;
			if (!_resistances.TryGetValue(damageClass, out r)) {
				r = new Resistance(null);
				_resistances[damageClass] = r;
			}
			return r;
		}

		public Resistance FindResistance(DamageMetaClass damageClass) {
			Resistance r;
			if (_resistances.TryGetValue(damageClass, out r)) {
				return r;
			}
			return null;
		}

		protected float GetRecoveryTime(float time, UnitActionMetaClass metaClass) {
			for (int i = 0; i < _activeSpells.Count; ++i) {
				var s = _activeSpells[i];
				if (!(s.disposed || s.muted)) {
					time = s.ProcRecoveryTime(time, metaClass);
				}
			}
			return time;
		}

		protected float GetImpairmentTime(float time) {
			for (int i = 0; i < _activeSpells.Count; ++i) {
				var s = _activeSpells[i];
				if (!(s.disposed || s.muted)) {
					time = s.ProcImpairmentTime(time);
				}
			}
			return time;
		}

		protected float GetPainChanceForDamageGiven(DamageEvent damage, DamageableActor target, ActorPropertyInstance property, float amount, float painChance, DamageClass.Channel channel) {
			for (int i = 0; i < _activeSpells.Count; ++i) {
				var s = _activeSpells[i];
				if (!(s.disposed || s.muted)) {
					painChance = s.ProcPainChanceForDamageGiven(damage, target, property, amount, painChance, channel);
				}
			}
			return painChance;
		}

		protected float GetPainChanceForDamageReceived(DamageEvent damage, ActorPropertyInstance property, float amount, float painChance, DamageClass.Channel channel) {
			for (int i = 0; i < _activeSpells.Count; ++i) {
				var s = _activeSpells[i];
				if (!(s.disposed || s.muted)) {
					painChance = s.ProcPainChanceForDamageReceived(damage, property, amount, painChance, channel);
				}
			}

			return painChance;
		}

		public ActorMetaClass metaClass {
			get {
				return _metaClass;
			}
		}

		public ActorPropertyInstance mutableHealth {
			get {
				return _mutableHealth;
			}
		}

		public ImmutableActorPropertyInstance health {
			get {
				return _immutableHealth;
			}
		}

		public PhysicalMaterialClass physicalMaterial {
			get {
				return _physicalMaterial;
			}
		}

		public virtual float fogOfWarSightRadius {
			get {
				return dead ? 0f : _fogOfWarSightRadius;
			}
			set {
				_fogOfWarSightRadius = value;
			}
		}

		public virtual float fogOfWarMaxVisRadius {
			get {
				return dead ? 0f : _fogOfWarMaxVisRadius;
			}
			set {
				_fogOfWarMaxVisRadius = value;
			}
		}

		public virtual float fogOfWarUnderwaterMaxVisRadius {
			get {
				return dead ? 0f : _fogOfWarUnderwaterMaxVisRadius;
			}
			set {
				_fogOfWarUnderwaterMaxVisRadius = value;
			}
		}

		public float fogOfWarObjectRadius {
			get {
				return _fogOfWarObjectRadius;
			}
			set {
				_fogOfWarObjectRadius = value;
			}
		}

		public bool fogOfWarCanSeeUnderwater {
			get {
				return _fogOfWarCanSeeUnderwater;
			}
			set {
				_fogOfWarCanSeeUnderwater = value;
			}
		}

		public virtual bool fogOfWarIsUnderwater {
			get {
				return false;
			}
		}

		public Vector2 fogOfWarPosition {
			get {
				var p = go.transform.position;
				return new Vector2(p.x, p.z);
			}
		}

		public int fogOfWarActorID {
			get {
				return netID;
			}
		}

		public EFogOfWarTest fogOfWarTest {
			get {
				return (EFogOfWarTest)_fogOfWarTest;
			}
		}

		public bool fogOfWarLocalVisibility {
			get; protected set;
		}

		public float fogOfWarLocalAlpha {
			get {
				return _fogOfWarLocalAlpha;
			}
		}

		public int fogOfWarUpdateFrame {
			get;
			protected set;
		}

		protected FogOfWarController serverFogOfWar {
			get {
				return ((Server.ServerWorld)world).fogOfWar;
			}
		}

		public bool FogOfWarCanSee(DamageableActor target) {
			return serverFogOfWar.CanBeSeenByTeam(team, target);
		}

		public ActorPropertyInstance GetMutableProperty(ActorPropertyMetaClass metaClass) {
			if (hasAuthority) {
				for (int i = 0; i < _propertyInstances.Length; ++i) {
					var p = _propertyInstances[i];
					if (_properties[p.serverIndex].IsA(metaClass)) {
						return p;
					}
				}
			}
			return null;
		}

		public ReadOnlyCollection<ActorPropertyInstance> mutableProperties {
			get {
				return _roProperties;
			}
		}

		public ImmutableActorPropertyInstance GetProperty(ActorPropertyMetaClass metaClass) {
			for (int i = 0; i < _immutablePropertyInstances.Length; ++i) {
				var p = _immutablePropertyInstances[i];
				if (_properties[p.serverIndex].IsA(metaClass)) {
					return p;
				}
			}
			return null;
		}

		public ImmutableActorPropertyInstance GetProperty(ActorProperty property) {
			for (int i = 0; i < _immutablePropertyInstances.Length; ++i) {
				var p = _immutablePropertyInstances[i];
				if (_properties[p.serverIndex] == property) {
					return p;
				}
			}
			return null;
		}

		float GetPropertyPercentBonus(ActorPropertyMetaClass metaClass) {
			if (metaClass != null) {
				var p = GetProperty(metaClass);
				if (p != null) {
					return p.GetPercentBonus(_level);
				}
			}
			return 0f;
		}

		float GetPropertyPercentBonus(ActorPropertyMetaClass metaClass, float amount) {
			var percent = GetPropertyPercentBonus(metaClass);
			amount += (amount/100f)*percent;
			return amount;
		}

		public ReadOnlyCollection<ImmutableActorPropertyInstance> properties {
			get {
				return _roImmutableProperties;
			}
		}

		public double lastRagdollExplosionTime {
			get;
			set;
		}

		public bool ragdollExplosionRateLimited {
			get;
			set;
		}

		public int numConcurrentRagdollExplosions {
			get;
			set;
		}

		public virtual bool ragdollEnabled {
			get {
				return false;
			}
		}

		public virtual bool FadeOutRagdoll(float delay, float ttl) {
			return false;
		}

		public bool feared {
			get;
			set;
		}

		public int waterVolumeCount {
			get {
				return _waterCount;
			}
			set {
				var wasInWater = inWater;
				_waterCount = value;

				if (wasInWater != inWater) {
					if (inWater) {
						OnEnterWaterVolume();
					} else {
						OnExitWaterVolume();
					}
				}
			}
		}

		public bool inWater {
			get {
				return waterVolumeCount > 0;
			}
		}

		protected virtual void OnEnterWaterVolume() {

		}

		protected virtual void OnExitWaterVolume() {
		}

		void ServerKill(DamageEvent damage) {
			if (hasAuthority && !dead) {
				dead = true;

				((Server.ServerWorld)world).gameMode.NotifyKill(damage.instigatingPlayer, damage.instigatingActor, damage.targetPlayer, damage.targetActor);

				if (mutableHealth != null) {
					if (mutableHealth.value > 0) {
						mutableHealth.value = 0;
					}
				}

				if (_activeSpells.Count > 0) {
					List<Spell> dispel = new List<Spell>();

					for (int i = 0; i < _activeSpells.Count; ++i) {
						var spell = _activeSpells[i];
						if (!spell.disposed) {
							if (spell.spellClass.dispelOnTargetDeath) {
								dispel.Add(spell);
							}
						}
					}

					for (int i = 0; i < dispel.Count; ++i) {
						dispel[i].Dispose();
					}
				}

				ServerSimulateKill(damage.killInfo);
			}
		}

		public void ServerKill() {
			ServerKill(null);
		}

		public void ServerKill(Server.Actors.ServerPlayerController instigator) {
			if (hasAuthority && !dead) {
				dead = true;

				((Server.ServerWorld)world).gameMode.NotifyKill(instigator, null, serverOwningPlayer, this);

				if (mutableHealth != null) {
					if (mutableHealth.value > 0) {
						mutableHealth.value = 0;
					}
				}

				for (int i = 0; i < _activeSpells.Count; ++i) {
					var spell = _activeSpells[i];
					if (!spell.disposed && spell.spellClass.dispelOnTargetDeath) {
						spell.Dispose();
					}
				}
				
				ServerSimulateKill(new SimulatedKillInfo());
			}
		}
		
		[RPC(ERPCDomain.Multicast, Reliable = true)]
		protected void Multicast_ClientSimulateKill(SimulatedKillInfo ki) {
			if (hasAuthority) {
				rpc_Multicast_ClientSimulateKill.Invoke(ki);
			} else {
				dead = true;
				fogOfWarLocalVisibility = fogOfWarVisibleWhenDead;
				ClientFlashHudOnDeath();
				ClientSimulateKill(ki);
			}
		}

		protected virtual void ClientFlashHudOnDeath() {
			if ((Client.Actors.ClientPlayerController.localPlayer != null) && (Client.Actors.ClientPlayerController.localPlayer.team == team)) {
				if (Client.Actors.ClientPlayerController.localPlayer.gameState.hud != null) {
					Client.Actors.ClientPlayerController.localPlayer.gameState.hud.DeathBlend();
				}
			}
		}

		float Resist(DamageMetaClass metaClass, EBlockParry blockParry, float amount) {
			Resistance r = null;

			for (var searchClass = metaClass; searchClass != null; searchClass = searchClass.parent) {
				r = FindResistance(searchClass);
				if (r != null) {
					break;
				}
			}

			if (r != null) {
				switch (blockParry) {
					case EBlockParry.Block:
					if (r.original != null) {
						amount = Resist(amount, r.blockDamageScale, r.blockMaxDamageReduction, r.original.blockDamageScaleBonusMetaClass, r.original.blockMaxDamageBonusMetaClass);
					} else {
						amount = Resist(amount, r.blockDamageScale, r.blockMaxDamageReduction, null, null);
					}
					break;
					case EBlockParry.Parry:
					if (r.original != null) {
						amount = Resist(amount, r.blockDamageScale, r.blockMaxDamageReduction, r.original.parryDamageScaleBonusMetaClass, r.original.parryMaxDamageBonusMetaClass);
					} else {
						amount = Resist(amount, r.blockDamageScale, r.blockMaxDamageReduction, null, null);
					}
					break;
					default: {
						var resistChance = r.resistChance;
						if ((resistChance < 100f) && (r.original != null)) {
							resistChance += GetPropertyPercentBonus(r.original.resistChanceBonusMetaClass);
						}
						if ((resistChance > 0f) && ((GameManager.instance.randomNumber*100) <= resistChance)) {
							if (r.original != null) {
								amount = Resist(amount, r.resistDamageScale, r.resistMaxDamageReduction, r.original.resistDamageScaleBonusMetaClass, r.original.resistDamageScaleBonusMetaClass);
							} else {
								amount = Resist(amount, r.resistDamageScale, r.resistMaxDamageReduction, null, null);
							}
						}
					}
					break;
				}
			}

			return amount;
		}

		float Resist(float amount, float scale, float max, ActorPropertyMetaClass scaleBonusClass, ActorPropertyMetaClass maxBonusClass) {
			if (amount > 0f) {
				scale = GetPropertyPercentBonus(scaleBonusClass, scale);
				max = GetPropertyPercentBonus(maxBonusClass, max);
				
				var delta = amount*scale;
				if (max > 0) {
					max = _damageScale*max;
					delta = Mathf.Min(delta, max);
				}
				return Mathf.Max(0, amount - delta);
			}
			return amount;
		}

		bool SelectChannelDamage(ActorPropertyInstance property, DamageClass.Channel channel, ref DamageClass.ActorDamageFilterRule rule, ref int depth, ref int depth2) {
			bool selected = false;
			
			if ((channel.metaClass != null) && (channel.affectedActors != null)) {
				for (int k = 0; k < channel.affectedActors.Length; ++k) {
					var aactors = channel.affectedActors[k];
					int ruleDepth;
					if ((aactors.affectedProperties != null) && (aactors.affectedProperties.Length > 0) && aactors.Check(this, out ruleDepth)) {
						if (ruleDepth >= depth) {
							// do we contain this property?
							ActorPropertyMetaClass bestClass = null;
							int bestDepth = int.MinValue;

							for (int i = 0; i < aactors.affectedProperties.Length; ++i) {
								var metaClass = aactors.affectedProperties[i];
								if (property.property.IsA(metaClass)) {
									var metaDepth = metaClass.depth;
									if (metaDepth > bestDepth) {
										bestDepth = metaDepth;
										bestClass = metaClass;
									}
								}
							}

							if (bestClass != null) {
								if ((ruleDepth > depth) || ((ruleDepth == depth) && (bestDepth > depth2))) {
									depth = ruleDepth;
									depth2 = bestDepth;
									selected = true;
									rule = aactors;
								}
							}
						}
					}
				}
			}

			return selected;
		}

		ActorPropertyInstance ProcDamageGiven(ActorPropertyInstance property, DamageEvent damage, DamageClass.Channel channel, ref DamageMetaClass damageClass, ref float amount) {

			_workingSpells.AddRange(_activeSpells);

			for (int i = 0; i < _workingSpells.Count; ++i) {
				var s = _workingSpells[i];
				if (!s.disposed) {
					property = s.ProcDamageGiven(property, damage, channel, ref damageClass, ref amount);
				}
			}

			_workingSpells.Clear();

			return property;
		}

		ActorPropertyInstance ProcDamageReceived(ActorPropertyInstance property, DamageEvent damage, DamageClass.Channel channel, ref DamageMetaClass damageClass, ref float amount) {

			_workingSpells.AddRange(_activeSpells);

			for (int i = 0; i < _workingSpells.Count; ++i) {
				var s = _workingSpells[i];
				if (!s.disposed) {
					property = s.ProcDamageReceived(property, damage, channel, ref damageClass, ref amount);
				}
			}

			_workingSpells.Clear();

			return property;
		}

		float ProcAccuracyBonus() {
			var acc = 0f;

			_workingSpells.AddRange(_activeSpells);

			for (int i = 0; i < _workingSpells.Count; ++i) {
				var s = _workingSpells[i];
				if (!s.disposed) {
					acc += s.ProcAccuracyBonus();
				}
			}

			_workingSpells.Clear();

			acc = 1f-Mathf.Clamp(acc, -1, 1);

			return acc;
		}

		float ProcDudChanceModifier() {
			var dud = 0f;

			_workingSpells.AddRange(_activeSpells);

			for (int i = 0; i < _workingSpells.Count; ++i) {
				var s = _workingSpells[i];
				if (!s.disposed) {
					dud += s.ProcDudChanceModifier();
				}
			}

			_workingSpells.Clear();

			return Mathf.Clamp(dud, -100, 100);
		}

		DamageResult ChannelDamage(ActorPropertyInstance property, DamageEvent damage, float amount, DamageClass.Channel channel, DamageClass.ActorDamageFilterRule rule, EBlockParry blockParry, DamageResult result) {

			var damageClass = channel.metaClass;

			var damageableInstigator = damage.instigatingActor as DamageableActor;
			if (damageableInstigator != null) {
				property = damageableInstigator.ProcDamageGiven(property, damage, channel, ref damageClass, ref amount);
				if (property == null) {
					return new DamageResult();
				}
			}

			property = ProcDamageReceived(property, damage, channel, ref damageClass, ref amount);
			if (property == null) {
				return new DamageResult();
			}

			if (channel.scaleByPropertyMaxValue) {
				amount *= property.property.maxValue;
			}

			var aaResult = new DamageResult();
			var actualDamage = Resist(damageClass, blockParry, amount);
			
			aaResult.hit = true;

			if (actualDamage != 0) {
				aaResult.Append(ServerApplyDamage(damage, property, actualDamage, out actualDamage, rule.basePainChance, rule.scaledPainChance, channel, new DamageResult()));
			}

			var gm = GameManager.instance;

			rule.procOnHit.Execute(damage.damageLevel, damage.damageSpellPower, gm.randomNumber, gm.randomNumber, damage.instigatingTeam, damage.instigatingActor, damage.instigatingPlayer, this, actualDamage);
			
			if (aaResult.damaged || aaResult.healed) {
				rule.procOnDamage.Execute(damage.damageLevel, damage.damageSpellPower, gm.randomNumber, gm.randomNumber, damage.instigatingTeam, damage.instigatingActor, damage.instigatingPlayer, this, actualDamage);
			}

			result.Append(aaResult);

			return result;
		}

		DamageResult DirectDamage(DamageEvent damage, DamageResult result) {
			var damageClass = damage.damageClass as DirectDamageClass;
			if (damageClass != null) {
				var dd = damageClass.damage;

				if (dd != null) {
					for (int i = 0; i < _propertyInstances.Length; ++i) {
						var property = _propertyInstances[i];

						DamageClass.ActorDamageFilterRule rule = new DamageClass.ActorDamageFilterRule();
						int depth = int.MinValue;
						int depth2 = int.MinValue;
						DirectDamageClass.Direct bestChannel = null;

						// find best damage channel for this property.
						for (int k = 0; k < dd.Length; ++k) {
							var channel = dd[k];
							if (CheckFriendlyFire(channel.friendlyFire, damage.instigatingTeam)) {
								if (SelectChannelDamage(property, channel, ref rule, ref depth, ref depth2)) {
									bestChannel = channel;
								}
							}
						}

						if (bestChannel != null) {
							result = ChannelDamage(property, damage, damage.damage*bestChannel.damageScale, bestChannel, rule, damage.blockParry, result);
						}
					}
				}
			}

			return result;
		}

		DamageResult ExplosionDamage(DamageEvent damage, DamageResult result) {

			var damageClass = damage.damageClass as ExplosionDamageClass;
			if (damageClass != null) {

				var ed = damageClass.damage;
				if ((ed != null) && (ed.Length > 0)) {
					for (int i = 0; i < _propertyInstances.Length; ++i) {
						var property = _propertyInstances[i];

						DamageClass.ActorDamageFilterRule rule = new DamageClass.ActorDamageFilterRule();
						int depth = int.MinValue;
						int depth2 = int.MinValue;
						ExplosionDamageClass.Explosion bestChannel = null;

						// find best damage channel for this property.
						for (int k = 0; k < ed.Length; ++k) {
							var channel = ed[k];
							if (CheckFriendlyFire(channel.friendlyFire, damage.effectingActor as ActorWithTeam, damage.instigatingPlayer)) {
								if (SelectChannelDamage(property, channel, ref rule, ref depth, ref depth2)) {
									bestChannel = channel;
								}
							}
						}

						if (bestChannel != null) {
							result = ChannelDamage(property, damage, damage.damage*bestChannel.damageScale, bestChannel, rule, 0, result);
						}
					}
				}
			}

			return result;
		}

		protected virtual DamageResult ServerApplyDamage(DamageEvent damage, ActorPropertyInstance property, float amount, out float damageDone, float basePainChance, float scaledPainChance, DamageClass.Channel channel, DamageResult result) {
			var oldValue = property.value;
			property.value = oldValue - amount;
			var newValue = property.value;
			damageDone = oldValue - newValue;

#if !(LOGIN_SERVER || DEDICATED_SERVER)
			if ((serverOwningPlayer != null) && serverOwningPlayer.godMode && !(this is ProjectileActor)) {
				property.value = oldValue;
			}
#endif
			if (scaledPainChance > 0) {
				scaledPainChance = 1f/scaledPainChance*100;
			}

			var painChance = Mathf.Clamp(basePainChance + (scaledPainChance * damageDone/property.max), 0, 100f);

			painChance = GetPainChanceForDamageReceived(damage, property, damageDone, painChance, channel);

			if (damage.instigatingActor is DamageableActor) {
				var a = (DamageableActor)damage.instigatingActor;
				painChance = a.GetPainChanceForDamageGiven(damage, this, property, damageDone, painChance, channel);
			} else if (damage.effectingActor is DamageableActor) {
				var a = (DamageableActor)damage.effectingActor;
				painChance = a.GetPainChanceForDamageGiven(damage, this, property, damageDone, painChance, channel);
			}

			bool triggerPain = (damage.pain != 0) && (painChance > 0f) && ((GameManager.instance.randomNumber*100) <= painChance);
			
			if (newValue != oldValue) {
				result.damaged = newValue < oldValue;
				result.healed = newValue > oldValue;
				result.pain |= triggerPain && PainResponse(damage, property, amount, damageDone, channel);
				if (damageDone > 0f) {
					((Server.ServerWorld)world).gameMode.NotifyDamage(damage.instigatingPlayer, damage.instigatingActor, damage.targetPlayer, damage.targetActor, property, damageDone);
				}
			} else {
				result.pain |= triggerPain && PainResponse(damage, property, amount, 0, channel);
			}

			return result;	
		}

		protected virtual bool PainResponse(DamageEvent damage, ActorPropertyInstance property, float amount, float actual, DamageClass.Channel channel) { return false; }

		bool ProcCheatDeath(DamageEvent damage) {
			for (int i = 0; i < _activeSpells.Count; ++i) {
				var s = _activeSpells[i];
				if (!(s.disposed || s.muted) && s.ProcCheatDeath(damage)) {
					return true;
				}
			}
			return false;
		}

		void InternalServerDamage(DamageEvent damage) {
			if (!hasAuthority || dead || pendingKill) {
				return;
			}

			if (_damageInstigatorStack.Contains(damage.instigatingActor)) {
				return;
			}

			bool pushed = false;
			if (damage.instigatingActor != null) {
				pushed = true;
				_damageInstigatorStack.Push(damage.instigatingActor);
			}

			ServerSimulateDamage(damage);

			if (pushed) {
				_damageInstigatorStack.Pop();
			}
		}

		void ServerSimulateDamage(DamageEvent damage) {
			var result = DirectDamage(damage, new DamageResult());
			result = ExplosionDamage(damage, result);
			result.fatal = damage.fatal || result.fatal || ((health != null) && (health.value <= 0));
			
			bool simulate = true;
			if (result.fatal && !dead) {
				if (!ProcCheatDeath(damage)) {
					simulate = false;
					SimulateDamageGiven(damage, result);
					ServerKill(damage);
				}
			}

			if (simulate) {
				SimulateDamageGiven(damage, result);
				ServerSimulateDamageReceived(damage, result);
			}
		}

		protected virtual void ServerSimulateDamageReceived(DamageEvent damage, DamageResult result) { }
		protected virtual void ServerSimulateDamageGiven(DamageEvent damage, DamageResult result) { }

		public virtual void ServerInitActorLevel(int level) {
			_level = (byte)level;
			_damageScale = (_damageCurve != null) ? _damageCurve.Eval(level) : 1f;
			_spellPower = 1f;//GameManager.instance.staticData.xpTable.GetSpellPower(_level);
			if (_propertyInstances != null) {
				for (int i = 0; i <_propertyInstances.Length; ++i) {
					var p = _propertyInstances[i];
					p.ServerInitLevel(level);
				}
			}
	}

		public virtual void ServerInitActorGear(MetaGame.PlayerInventorySkills.ItemStats gear) {
			if (_propertyInstances != null) {
				for (int i = 0; i <_propertyInstances.Length; ++i) {
					var p = _propertyInstances[i];
					p.ServerInitGear(gear);
				}
			}
		}

		public virtual void ServerSetActorLevel(int level) {
			_level = (byte)level;
			_damageScale = (_damageCurve != null) ? _damageCurve.Eval(level) : 1f;
			_spellPower = 1f;//GameManager.instance.staticData.xpTable.GetSpellPower(_level);
			rpc_Multicast_ClientSetLevel.Invoke(_level);
			if (_propertyInstances != null) {
				for (int i = 0; i <_propertyInstances.Length; ++i) {
					var p = _propertyInstances[i];
					p.ServerSetLevel(level);
				}
			}
		}

		public virtual void ServerSetActorGear(MetaGame.PlayerInventorySkills.ItemStats gear) {
			if (_propertyInstances != null) {
				for (int i = 0; i <_propertyInstances.Length; ++i) {
					var p = _propertyInstances[i];
					p.ServerSetGear(gear);
				}
			}
		}

		public void ServerClampPropertyValues() {
			if (_propertyInstances != null) {
				for (int i = 0; i <_propertyInstances.Length; ++i) {
					var p = _propertyInstances[i];
					p.ServerClampValue();
				}
			}
		}

		void OnRep_level() {
			_spellPower = 1f;// GameManager.instance.staticData.xpTable.GetSpellPower(_level);
		}

		[RPC(ERPCDomain.Multicast, CheckRelevancy = true, Reliable = true)]
		protected virtual void Multicast_ClientSetLevel(byte level) {
			_level = level;
		}

		static void SimulateDamageGiven(DamageEvent damage, DamageResult result) {
			var instigator = damage.instigatingActor as DamageableActor;
			if ((instigator != null) && !instigator.pendingKill) {
				instigator.ServerSimulateDamageGiven(damage, result);
			}
		}

		public override void Tick() {
			Assert.IsNotNull(_metaClass);

			base.Tick();

			if (hasAuthority) {

				_fogOfWarSightRadius = defaultFogOfWarSightRadius;
				_fogOfWarObjectRadius = defaultFogOfWarObjectRadius;
				_fogOfWarMaxVisRadius = defaultFogOfWarMaxVisRadius;
				_fogOfWarUnderwaterMaxVisRadius = defaultFogOfWarUnderwaterMaxVisRadius;
				_fogOfWarCanSeeUnderwater = defaultFogOfWarCanSeeUnderwater;
				_fogOfWarTest = (byte)defaultFogOfWarTest;
				feared = false;

				var dt = world.deltaTime;
				var props = mutableProperties;

				if (props != null) {
					for (int i = 0; i < props.Count; ++i) {
						var p = props[i];
						p.ServerBeginRefresh(dt);
					}
				}

				for (int i = 0; i < _resistances.Values.Count; ++i) {
					var r = _resistances.Values[i];
					r.ResetTransient();
				}

				// Remove expired spells and update stacking

				bool spellRemoved = false;

				for (int i = 0; i < _activeSpells.Count;) {
					var s = _activeSpells[i];
					if (s.disposed) {
						_activeSpells.RemoveAt(i);
						_replSpells.RemoveAt(i);
						_replSpellPower.RemoveAt(i);
						spellRemoved = true;
					} else {

						if (spellRemoved) {
							Spell unused;
							// recompute stacking depth;
							s.stackDepth = GetProcStackDepth(s, s.spellClass, out unused);
						}

						s.ServerResetTransient();
						++i;
					}
										
				}

				// Update muted/suspended states

				for (int i = 0; i < _activeSpells.Count; ++i) {
					var s = _activeSpells[i];
					if (s.spellClass.canBeMuted || s.spellClass.canBeSuspended) {
						var mute = !s.spellClass.canBeMuted;
						var suspend = !s.spellClass.canBeSuspended;

						if (!(mute && suspend)) {
							for (int k = 0; k < _activeSpells.Count; ++k) {
								if (k != i) {
									var z = _activeSpells[k];
									if (!z.disposed && !z.muted) {
										if (!mute) {
											if (s.IsMutedBy(z.spellClass.metaClass) || z.Mutes(s.spellClass.metaClass)) {
												mute = true;
											}
										}
										if (!suspend) {
											if (s.IsSuspendedBy(z.spellClass.metaClass) || z.Suspends(s.spellClass.metaClass)) {
												suspend = true;
											}
										}

										if (mute && suspend) {
											break;
										}
									}
								}
							}
						}

						s.ServerSetMuted(mute && s.spellClass.canBeMuted);
						s.ServerSetSuspended(suspend && s.spellClass.canBeSuspended);
					}
					
				}
				
				for (int i = 0; i < _activeSpells.Count; ++i) {
					var s = _activeSpells[i];
					if (!s.disposed) {
						s.ServerBeginUpdate(dt);
					}
				}

				for (int i = 0; i < _activeSpells.Count; ++i) {
					var s = _activeSpells[i];
					if (!s.disposed && s.updating) {
						s.ServerUpdate(dt*s.tickRate, dt);
					}
				}

				for (int i = 0; i < _activeSpells.Count; ++i) {
					var s = _activeSpells[i];
					if (!s.disposed && s.updating) {
						s.ServerEndUpdate();
					}
				}

				if (props != null) {
					for (int i = 0; i < props.Count; ++i) {
						var p = props[i];
						p.ServerEndRefresh();
					}
				}

				_accuracyBonus = ProcAccuracyBonus();
				_dudChanceModifier = ProcDudChanceModifier();
			} else {

				UpdateLocalFogOfWarVisibility();

				if (fogOfWarLocalVisibility) {
					if (_fogOfWarLocalAlpha < 1f) {
						bool visChange = (_fogOfWarLocalAlpha <= 0f);
						_fogOfWarLocalAlpha += world.deltaTime*FOGOFWAR_FADE_TIME;
						if (_fogOfWarLocalAlpha > 1f) {
							_fogOfWarLocalAlpha = 1f;
						}
						OnFogOfWarLocalAlphaChanged(visChange);
					}
				} else if (_fogOfWarLocalAlpha > 0f) {
					_fogOfWarLocalAlpha -= world.deltaTime*FOGOFWAR_FADE_TIME;
					if (_fogOfWarLocalAlpha <= 0f) {
						_fogOfWarLocalAlpha = 0f;
						OnFogOfWarLocalAlphaChanged(true);
					} else {
						OnFogOfWarLocalAlphaChanged(false);
					}
				}
			}
		}

		public Spell ServerApplySpell(int level, float spellPower, SpellClass spellClass, Team instigatingTeam, Actor instigator, Server.Actors.ServerPlayerController instigatingPlayer) {
			return ServerApplySpellWithDuration(level, spellPower, spellClass, instigatingTeam, instigator, instigatingPlayer, 0f);
        }

        public Spell ServerApplySpellWithDuration(int level, float spellPower, SpellClass spellClass, Team instigatingTeam, Actor instigator, Server.Actors.ServerPlayerController instigatingPlayer, float duration) {

			if (ServerCheckCanAddSpell(spellClass, instigatingTeam, instigator, instigatingPlayer)) {
				var spell = spellClass.New<Spell>(level, spellPower, (Server.ServerWorld)world, instigatingTeam, instigator, instigatingPlayer, this);
				if (spell != null) {
					if (duration > 0f) {
						spell.ServerSetDuration(duration);
					}
					try {
						ServerAddSpell(spell);
						return spell;
					} catch (Exception e) {
						Debug.LogException(e);
					}
				}
			} 

			return null;
		}

		protected virtual bool ServerCheckCanAddSpell(SpellClass spellClass, Team instigatingTeam, Actor instigator, Server.Actors.ServerPlayerController instigatingPlayer) {
			if (dead || !spellClass.CheckPreReqs(instigatingTeam, this)) {
				return false;
			}

			// is this spell mutexed?
			if (spellClass.canBeMutexed) {
				var procClass = spellClass.metaClass;
				var srcMutexed = spellClass.mutexedClasses;

				for (int i = 0; i < _activeSpells.Count; ++i) {
					var s = _activeSpells[i];
					if (!s.disposed) {
						var mutexed = s.spellClass.mutexedClasses;
						if ((mutexed != null) && procClass.IsAny(mutexed)) {
							return false;
						}
						if ((srcMutexed != null) && s.spellClass.metaClass.IsAny(srcMutexed)) {
							return false;
						}
					}
				}
			}

			{
				var req = spellClass.prereqAnyClasses;
				if ((req != null) && (req.Length > 0)) {
					var found = false;

					for (int i = 0; i < req.Length; ++i) {
						var metaClass = req[i];

						for (int k = 0; k < _activeSpells.Count; ++k) {
							var s = _activeSpells[k];
							if (!s.disposed) {
								if (s.spellClass.metaClass.IsA(metaClass)) {
									found = true;
									break;
								}
							}
						}

						if (found) {
							break;
						}
					}

					if (!found) {
						return false;
					}
				}
			}

			{
				var req = spellClass.prereqAllClasses;
				if ((req != null) && (req.Length > 0)) {
					for (int i = 0; i < req.Length; ++i) {
						var metaClass = req[i];

						var found = false;
						for (int k = 0; k < _activeSpells.Count; ++k) {
							var s = _activeSpells[k];
							if (!s.disposed) {
								if (s.spellClass.metaClass.IsA(metaClass)) {
									found = true;
									break;
								}
							}
						}

						if (!found) {
							return false;
						}
					}
				}
			}

			{
				Spell instance;
				int numInstances = GetProcStackDepth(null, spellClass, out instance);
								
				if (numInstances > 0) {
					switch (spellClass.stackingBehavior) {
						case EStackingBehavior.Discard:
							return false;
						case EStackingBehavior.Replace:
							return true;
						case EStackingBehavior.Stack:
							if (numInstances >= spellClass.stackLimit) {
								return false;
							}
							break;
						default:
							return false;
					}
				}
			}

			return true;
		}

		void ServerAddSpell(Spell spell) {

			if (_spellStack.Contains(spell.spellClass)) {
				string stack = string.Empty;
				foreach (var _class in _spellStack) {
					stack += "\n" + _class.name;
				}

                throw new System.Exception("Recursive spell casting detected while trying to add " + spell.spellClass.name + " to '" + go.transform.GetPath() + "' casting stack: " + stack);
			}

			_spellStack.Add(spell.spellClass);

			// this spell will apply, cleanse procs as specified.
			var cleansed = spell.spellClass.cleansedClasses;
			if (cleansed != null) {
				int count = 0;
				for (int i = 0; i < _activeSpells.Count; ++i) {
					var s = _activeSpells[i];
					if (!s.disposed) {
						if (s.spellClass.canBeCleansed && s.spellClass.metaClass.IsAny(cleansed)) {
							s.OnProcEnd(EExpiryReason.Cleansed, spell, spell.instigatingActor, spell.instigatingPlayer);
							++count;
							if ((spell.spellClass.cleanseLimit > 0) && (count >= spell.spellClass.cleanseLimit)) {
								break;
							}
						}
					}
				}
			}

			{
				Spell instance = null;
				spell.spawnedStackDepth = GetProcStackDepth(null, spell.spellClass, out instance) + 1;
				spell.stackDepth = spell.spawnedStackDepth;

				if (spell.spawnedStackDepth > 1) {
					switch (spell.spellClass.stackingBehavior) {
						case EStackingBehavior.Discard:
							_spellStack.Remove(spell.spellClass);
							throw new System.Exception("ServerAddSpell: discard stacking behavior!");
						case EStackingBehavior.Replace:
							spell.spawnedStackDepth = 1;
							instance.OnProcEnd(EExpiryReason.Replaced, spell, spell.instigatingActor, spell.instigatingPlayer);
							break;
						case EStackingBehavior.Stack:
							if (spell.spawnedStackDepth > spell.spellClass.stackLimit) {
								_spellStack.Remove(spell.spellClass);
								throw new System.Exception("ServerAddSpell: stack stacking behavior!");
							}
						break;
						default:
						_spellStack.Remove(spell.spellClass);
						throw new System.Exception("ServerAddSpell: invalid stacking behavior!");
					}
					spell.stackDepth = spell.spawnedStackDepth;
					spell.OnProcBegin((spell.spellClass.stackingBehavior == EStackingBehavior.Replace) ? instance : null);
				} else {
					spell.OnProcBegin(null);
				}
			}

			_spellStack.Remove(spell.spellClass);
		}

		public void ServerRemoveSpells(SpellMetaClass[] spellTypes, Actor effectingActor, Server.Actors.ServerPlayerController effectingPlayer, EExpiryReason reason) {
			if ((spellTypes != null) && (spellTypes.Length > 1)) {
				bool cleansing = reason == EExpiryReason.Cleansed;

				for (int i = 0; i < _activeSpells.Count; ++i) {
					var s = _activeSpells[i];
					if (!s.disposed) {
						if ((!cleansing || s.spellClass.canBeCleansed) && s.spellClass.metaClass.IsAny(spellTypes)) {
							s.OnProcEnd(reason, null, effectingActor, effectingPlayer);
						}
					}
				}
			}
		}

		public void ServerRemoveSpells(SpellMetaClass spellType, Actor effectingActor, Server.Actors.ServerPlayerController effectingPlayer, EExpiryReason reason) {
			if (spellType != null) {
				bool cleansing = reason == EExpiryReason.Cleansed;

				for (int i = 0; i < _activeSpells.Count; ++i) {
					var s = _activeSpells[i];
					if (!s.disposed) {
						if ((!cleansing || s.spellClass.canBeCleansed) && s.spellClass.metaClass.IsA(spellType)) {
							s.OnProcEnd(reason, null, effectingActor, effectingPlayer);
						}
					}
				}
			}
		}

		int GetProcStackDepth(Spell stopAt, SpellClass spellClass, out Spell instance) {
			instance = null;
			int depth = 0;
			var cleansed = spellClass.cleansedClasses;

			for (int i = 0; i < _activeSpells.Count; ++i) {
				var s = _activeSpells[i];
				if (!s.disposed) {
					if ((s.spellClass == spellClass)) {
						if (!(s.spellClass.canBeCleansed && s.spellClass.metaClass.IsAny(cleansed))) {
							instance = s;
							++depth;
						}
					}
					if (s == stopAt) {
						break;
					}
				}
			}
			return depth;
		}

		public void NotifySpellAdded(Spell spell) {
			_activeSpells.Add(spell);
			_replSpells.Add(spell.spellClass);
			_replSpellPower.Add(spell.spellPower);
		}

		public void NotifyEffectAdded(SpellEffectActor spell) {
			Assert.IsFalse(_spellEffects.Contains(spell));
			_spellEffects.Add(spell);
		}

		public void NotifyEffectRemoved(SpellEffectActor spell) {
			_spellEffects.Remove(spell);
		}

		protected virtual void OnRep_replSpells() {	}

		public ReadOnlyCollection<SpellEffectActor> activeSpellEffects {
			get {
				return _roSpellEffects;
			}
		}

		public ReadOnlyCollection<Spell> serverActiveSpells {
			get {
				return _roSpells;
			}
		}

		public ReadOnlyCollection<StaticAssetRef<SpellClass>> clientActiveSpells {
			get {
				return _roReplSpells;
			}
		}

		public ReadOnlyCollection<float> clientActiveSpellPower {
			get {
				return _roReplSpellPower;
			}
		}

		public int level {
			get {
				return _level;
			}
		}

		public bool elite {
			get {
				return false;// (_level & XPTable.ELITE_LEVEL_FLAG) != 0;
			}
		}

		public virtual float accuracyBonus {
			get {
				return _accuracyBonus;
			}
		}

		public virtual float dudChanceModifier {
			get {
				return _dudChanceModifier;
			}
		}

		public float spellPower {
			get {
				return _spellPower;
			}
		}

		public bool HasSpell(SpellMetaClass spellClass) {
			if (hasAuthority) {
				for (int i = 0; i < _activeSpells.Count; ++i) {
					var s = _activeSpells[i];
					if (!s.disposed) {
						if (s.spellClass.metaClass.IsA(spellClass)) {
							return true;
						}
					}
				}
			} else {
				for (int i = 0; i < _spellEffects.Count; ++i) {
					var s = _spellEffects[i];
					if (!s.pendingKill) {
						if (s.spellClass.metaClass.IsA(spellClass)) {
							return true;
						}
					}
				}
			}
			return false;       
        }

		public bool HasAnySpells(IList<SpellMetaClass> spellClasses) {
			for (int i = 0; i < spellClasses.Count; ++i) {
				var spellClass = spellClasses[i];
				if ((spellClass != null) && HasSpell(spellClass)) {
					return true;
				}
			}
			return false;
		}

		protected void ServerExecuteDamage(DamageEvent damage) {
			ServerExecuteDamage((Server.ServerWorld)world, damage);
		}

		public static void ServerExecuteDamage(Server.ServerWorld world, DamageEvent damage) {
			if (damage.instigatingTeam != null) {
				if ((damage.effectingActor != null) && damage.effectingActor.pendingKill) {
					damage.effectingActor = null;
				}
				if ((damage.instigatingActor != null) && damage.instigatingActor.pendingKill) {
					damage.instigatingActor = null;
				}
				if ((damage.instigatingPlayer != null) && damage.instigatingPlayer.pendingKill) {
					damage.instigatingPlayer = null;
				}
				if ((damage.targetPlayer != null) && damage.targetPlayer.pendingKill) {
					damage.targetPlayer = null;
				}

				if (damage.damageClass is ExplosionDamageClass) {
					if (world != null) {
						ServerExplosion(world, damage);
					}
				} else if ((damage.targetActor != null) && (damage.damageClass != null)) {
					damage.targetActor.InternalServerDamage(damage);
				}
			}
		}

		static void ServerExplosion(Server.ServerWorld world, DamageEvent damage) {
			var damageClass = (ExplosionDamageClass)damage.damageClass;
			var ed = damageClass.damage;
			if ((ed != null) && (ed.Length > 0) && (damageClass.explosionMinMaxDistance.y > 0f)) {
			
				List<DamageableActor> hitActors = new List<DamageableActor>();
				List<float> damageScale = new List<float>();

				var components = Physics.OverlapSphere(damage.hitLocation, damageClass.explosionMinMaxDistance.y, damageClass.explosionTargetLayers.ToLayerMask());
				if (components.Length > 0) {
					for (int i = 0; i < components.Length; ++i) {
						var c = components[i];
						var a = c.transform.FindServerActorUpwards() as DamageableActor;
						
						if ((a != null) && !(a.dead || a.pendingKill) && ((damage.ignoredActors == null) || !damage.ignoredActors.Contains(a))) {
							var colliderCenter = c.GetWorldSpaceCenter();
							if ((damageClass.explosionBlockingLayers == 0) || !Physics.Linecast(damage.hitLocation, colliderCenter, damageClass.explosionBlockingLayers.ToLayerMask())) {
#if UNITY_EDITOR
								Debug.DrawLine(damage.hitLocation, c.GetWorldSpaceCenter(), Color.green, 5f);
#endif
								int index = hitActors.FindIndex((x) => ReferenceEquals(x, a));
								if (index != -1) {
									var dist = damageScale[index];
									var newDist = (damage.hitLocation - colliderCenter).magnitude;
									if (newDist < dist) {
										damageScale[index] = newDist;
									}
								} else {
									hitActors.Add(a);
									damageScale.Add((damage.hitLocation - colliderCenter).magnitude);
								}
							} else {
#if UNITY_EDITOR
								Debug.DrawLine(damage.hitLocation, c.GetWorldSpaceCenter(), Color.red, 5f);
#endif
							}
						}
					}

					if (hitActors.Count > 0) {

						// convert damage scales
						for (int i = 0; i < hitActors.Count; ++i) {
							var d = damageScale[i];
							if (d < damageClass.explosionMinMaxDistance.x) {
								d = damageClass.explosionMinMaxDistance.x;
							}
							if (d > damageClass.explosionMinMaxDistance.y) {
								d = 0f;
							} else {
								var r = damageClass.explosionMinMaxDistance.y - damageClass.explosionMinMaxDistance.x;
								if (r > 0f) {
									d = (d - damageClass.explosionMinMaxDistance.x) / r;

									if (damageClass.explosionFalloff == ExplosionDamageClass.EFalloff.Exponential) {
										d = d*d;
									}

									d = Mathf.Lerp(damageClass.explosionMinMaxDamageScale.x, damageClass.explosionMinMaxDamageScale.y, d);
								} else {
									d = 0f;
								}
							}
							damageScale[i] = d;
						}

						for (int i = 0; i < hitActors.Count; ++i) {
							var a = hitActors[i];
							var ds = damageScale[i];

							var scaledDamage = damage;
							scaledDamage.targetActor = a;
							scaledDamage.targetPlayer = (a != null) ? a.serverOwningPlayer : null;
							scaledDamage.damage *= ds;
							scaledDamage.gibForce *= ds;
							scaledDamage.killInfo = new SimulatedKillInfo(new PhysicalDamageForces());
							
							if (scaledDamage.damage != 0f) {
								a.InternalServerDamage(scaledDamage);
							}
						}
					}
				}

				var explosiveForce = new ExplosiveForce(damage);
				for (int i = 0; i < world.clientConnections.Count; ++i) {
					var c = world.clientConnections[i];
					var owner = (Server.Actors.ServerPlayerController)c.owningPlayer;
					if (owner != null) {
						owner.ClientRunExplosion(explosiveForce);
					}
				}
			}
		}
	}

	public struct DamageEvent {
		public float damage;
		public float gibForce;
		public float distance;
		public bool fatal;
		//public int damageLevel;
		public EBlockParry blockParry;
		public EUnitActionCueSlotExplosion pain;
		public DamageClass damageClass;
		public SimulatedKillInfo killInfo;
		public Actor effectingActor; // projectile/spell -- the thing that physically delivered the damage.
		public Actor instigatingActor; // actor that caused the thing to happen
		public Team instigatingTeam;
		public DamageableActor targetActor; // target
		public Server.Actors.ServerPlayerController instigatingPlayer;
		public Server.Actors.ServerPlayerController targetPlayer;
		public Collider hitCollider;
		public Transform hitTransform;
		public Vector3 hitLocation;
		public List<Actor> ignoredActors;
		public int damageLevel;
		public float damageSpellPower;
	}

	
	[ReplicatedUsing(typeof(SimulatedKillInfoSerializer))]
	public struct SimulatedKillInfo {
		PhysicalDamageForces _physicalDamageForces;

		public SimulatedKillInfo(PhysicalDamageForces physicalDamageForces) {
			_physicalDamageForces = physicalDamageForces;
		}

		public void Serialize(Archive archive) {
			_physicalDamageForces.Serialize(archive);
		}

		public PhysicalDamageForces physicalDamageForces {
			get {
				return _physicalDamageForces;
			}
		}
	}

	public class SimulatedKillInfoSerializer : SerializableObjectNonReferenceFieldSerializer<SimulatedKillInfoSerializer> {
		
		public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
			SimulatedKillInfo killInfo = (SimulatedKillInfo)field;
			killInfo.Serialize(archive);
			field = killInfo;
			return archive.isLoading;
		}

		public override bool FieldsAreEqual(object a, object b) {
			return (a != null) && (b != null) && ((SimulatedKillInfo)a).Equals((SimulatedKillInfo)b);
		}

		public override object Copy(object toCopy) {
			return (SimulatedKillInfo)toCopy;
		}
	}

	public enum EPhysicalImpactType {
		None,
		Impact
	}

	public struct ImpactForce : System.IEquatable<ImpactForce> {
		public Vector3 location;
		public Vector3 force;

		public void Serialize(Archive archive) {
			archive.Serialize(ref location);
			archive.Serialize(ref force);
		}

		public bool Equals(ImpactForce other) {
			return (location == other.location)
				&& (force == other.force);
		}

		public static bool operator == (ImpactForce a, ImpactForce b) {
			return a.Equals(b);
		}

		public static bool operator != (ImpactForce a, ImpactForce b) {
			return !a.Equals(b);
		}

		public override bool Equals(object obj) {
			if (obj is ImpactForce) {
				return Equals((ImpactForce)obj);
			}
			return false;
		}

		public override int GetHashCode() {
			return location.GetHashCode() ^ force.GetHashCode();
		}
	}

	[ReplicatedUsing(typeof(ExplosiveForceSerializer))]
	public struct ExplosiveForce : System.IEquatable<ExplosiveForce> {
		public Vector3 worldPos;
		public ExplosionDamageClass.EFalloff falloff;
		public int blockingLayers;
		public int shockwaveLayers;
		public float inner;
		public float outer;
		public float innerForce;
		public float outerForce;
		public float ejection;

		public ExplosiveForce(DamageEvent damage) {
			var damageClass = (ExplosionDamageClass)damage.damageClass;
			falloff = damageClass.shockwaveFalloff;
			worldPos = damage.hitLocation;
			inner = damageClass.shockwaveDistance.x;
			outer = damageClass.shockwaveDistance.y;
			innerForce = damageClass.shockwaveScale.x*damage.gibForce;
			outerForce = damageClass.shockwaveScale.y*damage.gibForce;
			blockingLayers = damageClass.explosionBlockingLayers.ToLayerMask();
			shockwaveLayers = damageClass.shockwaveLayers.ToLayerMask();
			ejection = damageClass.ejectionModifier;
		}

		public void Serialize(Archive archive) {
			archive.SerializeAsInt(ref falloff);
			archive.Serialize(ref worldPos);
			archive.Serialize(ref inner);
			archive.Serialize(ref outer);
			archive.Serialize(ref innerForce);
			archive.Serialize(ref outerForce);
			archive.Serialize(ref blockingLayers);
			archive.Serialize(ref shockwaveLayers);
			archive.Serialize(ref ejection);
		}

		public bool Equals(ExplosiveForce other) {
			return (falloff == other.falloff)
				&& (worldPos == other.worldPos)
				&& (inner == other.inner)
				&& (outer == other.outer)
				&& (innerForce == other.innerForce)
				&& (outerForce == other.outerForce)
				&& (blockingLayers == other.blockingLayers)
				&& (shockwaveLayers == other.shockwaveLayers)
				&& (ejection == other.ejection);
		}

		public static bool operator == (ExplosiveForce a, ExplosiveForce b) {
			return a.Equals(b);
		}

		public static bool operator != (ExplosiveForce a, ExplosiveForce b) {
			return !a.Equals(b);
		}

		public override bool Equals(object obj) {
			if (obj is ExplosiveForce) {
				return Equals((ExplosiveForce)obj);
			}
			return false;
		}

		public override int GetHashCode() {
			return falloff.GetHashCode() ^ worldPos.GetHashCode() ^ inner.GetHashCode() ^ outer.GetHashCode()
				^ innerForce.GetHashCode() ^ outerForce.GetHashCode() ^ ejection.GetHashCode()
				^ blockingLayers.GetHashCode() ^ shockwaveLayers.GetHashCode();
		}
	}

	public class ExplosiveForceSerializer : SerializableObjectNonReferenceFieldSerializer<ExplosiveForceSerializer> {
		
		public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
			ExplosiveForce force = (ExplosiveForce)field;
			force.Serialize(archive);
			field = force;
			return archive.isLoading;
		}

		public override bool FieldsAreEqual(object a, object b) {
			return (a != null) && (b != null) && ((ExplosiveForce)a).Equals((ExplosiveForce)b);
		}

		public override object Copy(object toCopy) {
			return (ExplosiveForce)toCopy;
		}
	}

	[ReplicatedUsing(typeof(PhysicalDamageForcesSerializer))]
	public struct PhysicalDamageForces : System.IEquatable<PhysicalDamageForces> {
		EPhysicalImpactType _impactType;
		ImpactForce _impactForce;

		public PhysicalDamageForces(ImpactForce impactForce) {
			_impactType = EPhysicalImpactType.Impact;
			_impactForce = impactForce;
		}
						
		public void Serialize(Archive archive) {
			archive.SerializeAsInt(ref _impactType);
			_impactForce.Serialize(archive);
		}

		public EPhysicalImpactType impactType {
			get {
				return _impactType;
			}
		}

		public ImpactForce impactForce {
			get {
				return _impactForce;
			}
		}

		public bool Equals(PhysicalDamageForces other) {
			return (_impactType == other._impactType)
				&& (_impactForce == other._impactForce);
		}

		public static bool operator == (PhysicalDamageForces a, PhysicalDamageForces b) {
			return a.Equals(b);
		}

		public static bool operator != (PhysicalDamageForces a, PhysicalDamageForces b) {
			return !a.Equals(b);
		}

		public override bool Equals(object obj) {
			if (obj is PhysicalDamageForces) {
				return Equals((PhysicalDamageForces)obj);
			}
			return false;
		}

		public override int GetHashCode() {
			return _impactType.GetHashCode() ^ _impactForce.GetHashCode();
		}
	}

	public class PhysicalDamageForcesSerializer : SerializableObjectNonReferenceFieldSerializer<PhysicalDamageForcesSerializer> {
		
		public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
			PhysicalDamageForces pi = (PhysicalDamageForces)field;
			pi.Serialize(archive);
			field = pi;
			return archive.isLoading;
		}

		public override bool FieldsAreEqual(object a, object b) {
			return (a != null) && (b != null) && ((PhysicalDamageForces)a).Equals((PhysicalDamageForces)b);
		}

		public override object Copy(object toCopy) {
			return (PhysicalDamageForces)toCopy;
		}
	}
}