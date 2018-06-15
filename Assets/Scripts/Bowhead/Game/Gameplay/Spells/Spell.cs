// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using Bowhead.Server.Actors;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bowhead.Actors.Spells {

	public enum EEliteSpellFilter {
		None,
		OnlyElites,
		NeverElites
	}

	[Obfuscation(Feature = "renaming", Exclude = true, ApplyToMembers = true)] // Used in flow canvas
	[Serializable]
	public struct ActorSpellFilterRule {
		[SerializeField]
		ActorMetaClass[] affectedClasses;
		[SerializeField]
		ActorMetaClass[] excludedClasses;
		[SerializeField]
		EEliteSpellFilter eliteFilter;
		[SerializeField]
		[EditorFlags]
		EFriendlyFire friendlyFire;

		public bool Check(Team team, DamageableActor actor, out int depth) {

			depth = -1;

			var elite = actor.elite;

			if (elite && (eliteFilter == EEliteSpellFilter.NeverElites)) {
				return false;
			} else if (!elite && (eliteFilter == EEliteSpellFilter.OnlyElites)) {
				return false;
			}

			if (!actor.CheckFriendlyFire(friendlyFire, team)) {
				return false;
			}

			var metaClass = actor.metaClass;

			if (excludedClasses != null) {
				for (int i = 0; i < excludedClasses.Length; ++i) {
					var c = excludedClasses[i];
					if ((c != null) && metaClass.IsA(c)) {
						return false;
					}
				}
			}

			bool selected = false;

			if (affectedClasses != null) {
				for (int i = 0; i < affectedClasses.Length; ++i) {
					var c = affectedClasses[i];
					if ((c != null) && metaClass.IsA(c) && (c.depth > depth)) {
						depth = c.depth;
						selected = true;
					}
				}
			}

			return selected || (affectedClasses == null) || (affectedClasses.Length == 0);
		}

		public bool empty {
			get {
				return ((affectedClasses == null) || (affectedClasses.Length == 0)) &&
					((excludedClasses == null) || (excludedClasses.Length == 0));
			}
		}
	}

	[Obfuscation(Feature = "renaming", Exclude = true, ApplyToMembers = true)] // Used in flow canvas
	[Serializable]
	public struct SpellCastRule {
		public ActorSpellFilterRule affectedActors;
		public SpellClass[] spells;
		[Range(0, 100)]
		public float chance;

		public bool Execute(int level, float spellPower, float random, Team instigatingTeam, Actor instigator, ServerPlayerController instigatingPlayer, DamageableActor target, List<Spell> cast) {
			bool didCast = false;

			if ((random*100) <= chance) {
				if (spells != null) {
					for (int i = 0; i < spells.Length; ++i) {
						var s = spells[i];
						if (s != null) {
							var x = target.ServerApplySpell(level, spellPower, s, instigatingTeam, instigator, instigatingPlayer);
							if ((x != null) && (cast != null)) {
								didCast = true;
								cast.Add(x);
							}
						}
					}
				}
			}

			return didCast;
		}

		public static bool GetBestRule(IList<SpellCastRule> rules, Team instigatingTeam, DamageableActor target, out SpellCastRule selected) {
			int bestDepth = int.MinValue;
			selected = new SpellCastRule();

			for (int i = 0; i < rules.Count; ++i) {
				int depth;
				var rule = rules[i];
				if (rule.affectedActors.Check(instigatingTeam, target, out depth) && (depth > bestDepth)) {
					bestDepth = depth;
					selected = rule;
				}
			}

			return (bestDepth > int.MinValue);
		}

		public void ClientPrecache() {}

		public static void ClientPrecache(IList<SpellCastRule> rules) {
			if (rules != null) {
				for (int i = 0; i < rules.Count; ++i) {
					rules[i].ClientPrecache();
				}
			}
		}
	}

	[Serializable]
	public struct ProcEvent {
		public SpellCastRule[] selfProcs;
        public SpellCastRule[] targetProcs;
		
		public void Execute(int level, float spellPower, float random, float random2, Team instigatingTeam, Actor instigator, ServerPlayerController instigatingPlayer, DamageableActor target) {
			if (target != null) {
				if (targetProcs != null) {
					SpellCastRule rule;
					if (SpellCastRule.GetBestRule(targetProcs, instigatingTeam, target, out rule)) {
						rule.Execute(level, spellPower, random, instigatingTeam, instigator, instigatingPlayer, target, null);
					}
				}
			}
			if (selfProcs != null) {
				var instigatorDamage = instigator as DamageableActor;
				if (instigatorDamage != null) {
					SpellCastRule rule;
					if (SpellCastRule.GetBestRule(selfProcs, instigatingTeam, instigatorDamage, out rule)) {
						rule.Execute(level, spellPower, random2, instigatingTeam, instigatorDamage, instigatingPlayer, instigatorDamage, null);
					}
				}
			}
		}
		 
		public void ClientPrecache() {
			SpellCastRule.ClientPrecache(selfProcs);
			SpellCastRule.ClientPrecache(targetProcs);
		}
	}

	[Serializable]
	public struct ActorDamageScale {
		public float multiplier;
		public bool oneOver;
		public float maxDelta;

		public float GetValue(float value, float spellPower) {
			var mod = value;
			if (oneOver) {
				if (value != 0f) {
					mod = (1f / value) * multiplier;
				}
			} else {
				mod = value * multiplier;
			}

			if (maxDelta > 0) {
				var delta = Mathf.Abs(mod - value);
				var max = maxDelta * spellPower;
				if (delta > max) {
					if (mod > value) {
						mod = value + max;
					} else {
						mod = value - max;
					}
				}
			}

			return mod;
		}
	}

	public enum EExpiryReason {
		Expired,
		Cleansed,
		Replaced
	}

	public abstract class Spell : IDisposable {

		Actor _instigatingActor;
		ServerPlayerController _instigatingPlayer;
		DamageableActor _target;
		Team _team;
		AreaOfEffectActor _attachedAOE;

		List<SpellEffectActor> _effects = new List<SpellEffectActor>();
		
		bool _muted;
		bool _wasMuted;
		bool _suspended;
		bool _wasSuspended;
		bool _expired;
		bool _didEnd;
		bool _disposed;
		bool _mutedChanged;
		bool _updating;
		float _time;
		float _duration;
		int _stackDepth;
		int _level;
						
		public Spell() {
			tickRate = 1f;
		}

		public Spell(int level, float spellPower, Server.ServerWorld world, SpellClass spellClass, Team team, Actor instigator, ServerPlayerController instigatingPlayer, DamageableActor target) {
			_team = team;
			_instigatingActor = instigator;
			_level = level;
			_instigatingPlayer = instigatingPlayer;
			_target = target;
			_duration = Mathf.Lerp(spellClass.duration.x, spellClass.duration.y, GameManager.instance.randomNumber);
			this.spellPower = spellClass.doesNotScale ? 1f : spellPower;
			this.spellClass = spellClass;
			this.world = world;
		}

		public void ServerSetDuration(float duration) {
			_duration = duration;
		}

		public virtual void ServerResetTransient() {
			_wasMuted = _muted;
			_wasSuspended = _suspended;
			_muted = false;
			_suspended = false;
			tickRate = 1f;
		}

		public virtual void ServerBeginUpdate(float unscaledDt) {
			_updating = true;

			var water = spellClass.waterInteraction;
			if ((water != SpellClass.EWaterInteraction.None) &&
				(water != SpellClass.EWaterInteraction.CastRequireWater) &&
				(water != SpellClass.EWaterInteraction.CastRequireDeepWater)) {
				if (target.inWater) {
					//if (water == SpellClass.EWaterInteraction.ExpireRequireDeepWater) {
					//	if ((uTarget == null) || !uTarget.isUnderwater) {
					//		OnProcEnd(EExpiryReason.Expired, null, instigatingActor, instigatingPlayer);
					//	}
					//} else if (water == SpellClass.EWaterInteraction.CleanseRequireDeepWater) {
					//	if ((uTarget == null) || !uTarget.isUnderwater) {
					//		OnProcEnd(EExpiryReason.Cleansed, null, instigatingActor, instigatingPlayer);
					//	}
					//} else if (water == SpellClass.EWaterInteraction.ExpireInWater) {
					//	OnProcEnd(EExpiryReason.Expired, null, instigatingActor, instigatingPlayer);
					//} else if (water == SpellClass.EWaterInteraction.CleanseInWater) {
					//	OnProcEnd(EExpiryReason.Cleansed, null, instigatingActor, instigatingPlayer);
					//} else if (water == SpellClass.EWaterInteraction.ExpireInDeepWater) {
					//	if ((uTarget != null) && uTarget.isUnderwater) {
					//		OnProcEnd(EExpiryReason.Expired, null, instigatingActor, instigatingPlayer);
					//	}
					//} else if (water == SpellClass.EWaterInteraction.CleanseInDeepWater) {
					//	if ((uTarget != null) && uTarget.isUnderwater) {
					//		OnProcEnd(EExpiryReason.Cleansed, null, instigatingActor, instigatingPlayer);
					//	}
					//}

				} else if (water == SpellClass.EWaterInteraction.ExpireRequireWater) {
					OnProcEnd(EExpiryReason.Expired, null, instigatingActor, instigatingPlayer);
				} else if (water == SpellClass.EWaterInteraction.CleanseRequireDeepWater) {
					OnProcEnd(EExpiryReason.Cleansed, null, instigatingActor, instigatingPlayer);
				}
			}
		}

		public virtual void ServerUpdate(float dt, float unscaledDt) {
			if (!suspended) {
				_time += dt*tickRate;
				if ((_duration > 0f) && (_time > _duration)) {
					_time = _duration;
				}
			}
		}

		public bool ServerSetMuted(bool muted) {
			_muted = muted;
			if (_muted != _wasMuted) {
				OnMutedChanged();
			}
			return _muted != _wasMuted;
		}

		public bool ServerSetSuspended(bool suspended) {
			_suspended = suspended;
			if (_suspended != _wasSuspended) {
				OnSuspendedChanged();
			}
			return _suspended != _wasSuspended;
		}

		public virtual void ServerEndUpdate() {
			if (((_duration > 0f) && (_time >= _duration)) || spellClass.expireAfterOneTick) {
				Dispose();
			} else if (_mutedChanged) {
				_mutedChanged = false;
				if (muted) {
					DestroyEffects();
				} else {
					CreateEffects();
				}
			}
			_updating = false;
		}

		public virtual void OnProcBegin(Spell chainedFrom) {

			dInstigator = instigatingActor as DamageableActor;
			//uTarget = target as Unit;

			if (target != null) {
				target.NotifySpellAdded(this);

				if (_duration > 0f) {
					if (spellClass.durationStackingPenalty != EStackingPenalty.None) {
						if (spawnedStackDepth > 1) {
							_duration = _duration/spawnedStackDepth;
							if (_duration <= 0f) {
								_duration = float.Epsilon;
							}
						}
					}
				}

				if (!muted && (chainedFrom == null)) {
					CreateEffects();
				}

				if (spellClass.attachedAOE != null) {
					_attachedAOE = spellClass.attachedAOE.Spawn<AreaOfEffectActor>(level, spellPower, world, instigatingPlayer, instigatingActor, target, team);
                }

				if (spellClass.dropAOE != null) {
					var random = GameManager.instance.randomNumber;
					if ((random*100) <= spellClass.dropAOEChance) {
						var aoe = spellClass.dropAOE.Spawn<AreaOfEffectActor>(level, spellPower, world, instigatingPlayer, instigatingActor, null, team);
						aoe.ServerPlace(target.go.transform.position, target.go.transform.rotation.eulerAngles.y);
					}
				}
			}
		}

		public virtual void OnProcEnd(EExpiryReason reason, Spell chained, Actor effectingActor, ServerPlayerController effectingPlayer) {
			Assert.IsFalse(_didEnd);

			_didEnd = true;

			if (reason == EExpiryReason.Replaced) {
				// transfer existing effects to chained spell
				for (int i = 0; i < _effects.Count; ++i) {
					var spe = _effects[i];
					if (!spe.pendingKill) {
						chained._effects.Add(spe);
						spe.ServerInit(chained, spe.effectClass);
					}
				}
			} else {
				DestroyEffects();
				
				if (reason == EExpiryReason.Cleansed) {
					SpellCastRule rule;
					if (SpellCastRule.GetBestRule(spellClass.procOnCleanse, team, target, out rule)) {
						rule.Execute(level, spellPower, GameManager.instance.randomNumber, team, instigatingActor, instigatingPlayer, target, null);
					}
					target.ServerRemoveSpells(spellClass.cleanseOnCleanse, effectingActor, effectingPlayer, EExpiryReason.Cleansed);
					target.ServerRemoveSpells(spellClass.expireOnCleanse, effectingActor, effectingPlayer, EExpiryReason.Expired);
				} else {
					SpellCastRule rule;
					if (SpellCastRule.GetBestRule(spellClass.procOnExpiry, team, target, out rule)) {
						rule.Execute(level, spellPower, GameManager.instance.randomNumber, team, instigatingActor, instigatingPlayer, target, null);
					}
					target.ServerRemoveSpells(spellClass.cleanseOnExpiry, effectingActor, effectingPlayer, EExpiryReason.Cleansed);
					target.ServerRemoveSpells(spellClass.expireOnExpiry, effectingActor, effectingPlayer, EExpiryReason.Expired);
				}
			}

			_effects = null;

			if (!_disposed) {
				Dispose();
			}
		}

		public void Dispose() {
			Dispose(true, EExpiryReason.Expired);
		}


		protected virtual void Dispose(bool disposing, EExpiryReason reason) {

			if (_disposed) {
				return;
			}

			_disposed = true;
		
			if (disposing) {
				if (!_didEnd) {
					OnProcEnd(reason, null, null, null);
				}
				if (_attachedAOE != null) {
					_attachedAOE.Destroy();
					_attachedAOE = null;
				}
			}
		}

		protected virtual void OnMutedChanged() {
			_mutedChanged = true;
		}

		protected virtual void OnSuspendedChanged() {
			_mutedChanged = true;
		}

		void CreateEffects() {
			if (spellClass.effectClasses != null) {
				for (int i = 0; i < spellClass.effectClasses.Length; ++i) {
					var effectClass = spellClass.effectClasses[i];
					if ((effectClass != null) && (effectClass.instanceType != null)) {
						var effect = (SpellEffectActor)target.world.Spawn(effectClass.instanceType, null, SpawnParameters.defaultParameters);
						effect.ServerInit(this, effectClass);
						_effects.Add(effect);
					}
				}
			}
		}

		void DestroyEffects() {
			for (int i = 0; i < _effects.Count; ++i) {
				var spe = _effects[i];
				if (!spe.pendingKill) {
					spe.ServerEndProc(EExpiryReason.Expired);
				}
			}
			_effects.Clear();
		}

		public virtual ActorPropertyInstance ProcDamageGiven(ActorPropertyInstance property, DamageEvent damage, DamageClass.Channel channel, ref DamageMetaClass damageClass, ref float amount) {
			return property;
		}

		public virtual ActorPropertyInstance ProcDamageReceived(ActorPropertyInstance property, DamageEvent damage, DamageClass.Channel channel, ref DamageMetaClass damageClass, ref float amount) {
			return property;
		}

		public virtual bool ProcCheatDeath(DamageEvent damage) {
			return false;
		}

		public virtual float ProcRecoveryTime(float time, UnitActionMetaClass metaClass) {
			return time;
		}

		public virtual float ProcImpairmentTime(float time) {
			return time;
		}

		public virtual float ProcFogOfWarSightRadius(float radius) {
			return radius;
		}

		public virtual float ProcFogOfWarObjectRadius(float radius) {
			return radius;
		}

		public virtual float ProcAccuracyBonus() {
			return 0;
		}

		public virtual float ProcDudChanceModifier() {
			return 0;
		}

		public virtual EFogOfWarTest ProcFogOfWarTest(EFogOfWarTest test) {
			return test;
		}

		public virtual float ProcPainChanceForDamageGiven(DamageEvent damage, DamageableActor target, ActorPropertyInstance property, float amount, float painChance, DamageClass.Channel channel) {
			return painChance;
		}

		public virtual float ProcPainChanceForDamageReceived(DamageEvent damage, ActorPropertyInstance property, float amount, float painChance, DamageClass.Channel channel) {
			return painChance;
		}

		public virtual bool IsMutedBy(SpellMetaClass metaClass) {
			return (spellClass.mutingClasses != null) && metaClass.IsAny(spellClass.mutingClasses);
		}

		public virtual bool Mutes(SpellMetaClass metaClass) {
			return (spellClass.mutedClasses != null) && metaClass.IsAny(spellClass.mutedClasses);
		}

		public virtual bool IsSuspendedBy(SpellMetaClass metaClass) {
			return (spellClass.suspendingClasses != null) && metaClass.IsAny(spellClass.suspendingClasses);
		}

		public virtual bool Suspends(SpellMetaClass metaClass) {
			return (spellClass.suspendedClasses != null) && metaClass.IsAny(spellClass.suspendedClasses);
		}

		public Actor instigatingActor {
			get {
				return _instigatingActor;
			}
		}

		public ServerPlayerController instigatingPlayer {
			get {
				return _instigatingPlayer;
			}
		}

		public Team team {
			get {
				return _team;
			}
		}

		public DamageableActor target {
			get {
				return _target;
			}
		}

		public bool muted {
			get {
				return _muted || _suspended;
			}
		}

		public bool updating {
			get {
				return _updating;
			}
		}

		public bool suspended {
			get {
				return _suspended;
			}
		}

		public bool canBeMuted {
			get {
				return spellClass.canBeMuted;
			}
		}

		public float time {
			get {
				return _time;
			}
		}

		public float spellPower {
			get;
			private set;
		}

		public float tickRate {
			get;
			set;
		}

		public float duration {
			get {
				return _duration;
			}
		}

		public float timeToLive {
			get {
				if (tickRate > 0f) {
					return _duration / tickRate;
				}
				return _duration;
			}
		}

		public int level {
			get {
				return _level;
			}
		}

		// how many instances of this type of spell are stacked below this spell (including itself).

		public int spawnedStackDepth {
			get;
			set;
		}

		public int stackDepth {
			get;
			set;
		}

		public SpellClass spellClass {
			get;
			private set;
		}

		public DamageableActor dInstigator {
			get;
			private set;
		}

		//public Unit uTarget {
		//	get;
		//	private set;
		//}

		public Server.ServerWorld world {
			get;
			private set;
		}

		public bool disposed {
			get {
				return _disposed;
			}
		}
	}
}