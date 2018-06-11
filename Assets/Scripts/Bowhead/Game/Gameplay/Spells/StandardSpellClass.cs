// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;
using Bowhead.Server.Actors;

namespace Bowhead.Actors.Spells {

	public sealed class StandardSpellClass : SpellClass {

		[Serializable]
		public struct DamageModifierCondition {
			public EConditionTest test;
			public ActorPropertyMetaClass[] propertyClasses;
			public bool scaleByPropertyMaxValue;
			public bool failIfNoMatchingProperties;
			public float referenceValue;
		}

		[Serializable]
		public struct DamageModifier {
			public ActorSpellFilterRule affectedActors;
			public DamageMetaClass srcDamageClass;
			public DamageMetaClass dstDamageClass;
			public ActorPropertyMetaClass dstProperty;
			public ActorDamageScale damageScale;
			public DamageModifierCondition condition;
			public float additionalDamage;
			public float damageBonusPerMeterTraveled;
			public float maxTraveledDamageBonus;
			[Range(0, 100)]
			public float chance;
		}

		[Serializable]
		public struct DamageRedirect {
			public ActorSpellFilterRule affectedActors;
			public DamageMetaClass srcDamageClass;
			public DamageClass redirectDamageClass;
			public ActorDamageScale damageScale;
			public float additionalDamage;
			[Range(0, 100)]
			public float chance;
		}

		[Serializable]
		public struct DamageCooldown {
			public ActorSpellFilterRule affectedActors;
			public DamageMetaClass srcDamageClass;
			public ActorPropertyMetaClass dstProperty;
			public ActorDamageScale damageScale;
			public float timeScale;
			public float baseTime;
		}

		[Serializable]
		public struct DamageSpellClass {
			public SpellClass[] spells;
			public bool scaleDurationByDamage;
			public float baseDuration;
			public float durationScale;
			public float maxDuration;
		}

		[Serializable]
		public struct DamageEssenceGain {
			public float scale;
		}

		[Serializable]
		public struct DamageSpellCastRule {
			public ActorSpellFilterRule affectedActors;
			public DamageSpellClass[] spells;
			[EditorFlags]
			public EFriendlyFire friendlyFire;
			public DamageMetaClass damageClass;
			public DamageEssenceGain essenceGain;
			public float minDamageToProc;
			[Range(0, 100)]
			public float chance;
			public bool scaleWithLevel;

			public float GetMinDamageToProc(float scale) {
				return scaleWithLevel ? (minDamageToProc*scale) : minDamageToProc;
			}
			
			public bool Execute(int level, float spellPower, float random, Team instigatingTeam, Actor instigator, ServerPlayerController instigatingPlayer, DamageableActor target, float damage, float damageScale, List<Spell> cast) {
				bool didCast = false;

				if ((spells != null) && ((chance >= 100) || ((random*100) < chance))) {
					for (int i = 0; i < spells.Length; ++i) {
						var s = spells[i];

						if (s.spells != null) {
							float duration = 0f;

							if (s.scaleDurationByDamage) {
								duration = Mathf.Min(s.maxDuration, s.baseDuration+(damage*s.durationScale));
							}

							for (int k = 0; k < s.spells.Length; ++k) {
								var ss = s.spells[k];
								if (ss != null) {
									var x = target.ServerApplySpellWithDuration(level, spellPower, ss, instigatingTeam, instigator, instigatingPlayer, duration);
									if ((x != null) && (cast != null)) {
										didCast = true;
										cast.Add(x);
									}
								}
							}
						}
					}
				}

				if (instigatingPlayer != null) {
					var essenceReward = essenceGain.scale*damage;
					if (essenceReward > 0) {
						//instigatingPlayer.AddFractionalSoulStonePoints(essenceReward);	
					}
				}
				return didCast;
			}

			public static bool GetBestRule(IList<DamageSpellCastRule> rules, Team instigatingTeam, DamageableActor target, DamageMetaClass damageClass, out DamageSpellCastRule selected) {
				int bestDepth = int.MinValue;
				selected = new DamageSpellCastRule();

				for (int i = 0; i < rules.Count; ++i) {
					int depth;
					var rule = rules[i];
					if (target.CheckFriendlyFire(rule.friendlyFire, instigatingTeam) && rule.affectedActors.Check(instigatingTeam, target, out depth) && (depth > bestDepth)) {
						if ((rule.damageClass == null) || damageClass.IsA(rule.damageClass)) {
							bestDepth = depth;
							selected = rule;
						}
					}
				}

				return (bestDepth > int.MinValue);
			}
		}

		[Serializable]
		public struct DamageProcEvent {
			public DamageSpellCastRule[] selfProcs;
			public DamageSpellCastRule[] targetProcs;

			public void Execute(int level, float spellPower, float random, float random2, Team instigatingTeam, Actor instigator, ServerPlayerController instigatingPlayer, DamageableActor target, DamageMetaClass damageClass, float damage, float damageScale) {
				if ((target != null) && (target.team != null)) {
					if (targetProcs != null) {
						DamageSpellCastRule rule;
						if (DamageSpellCastRule.GetBestRule(targetProcs, target.team, target, damageClass, out rule)) {
							var proc = damage >= rule.GetMinDamageToProc(damageScale);
							if (proc) {
								rule.Execute(level, spellPower, random, instigatingTeam, instigator, instigatingPlayer, target, damage, damageScale, null);
							}
						}
					}
				}
				if (selfProcs != null) {
					var instigatorDamage = instigator as DamageableActor;
					if (instigatorDamage != null) {
						DamageSpellCastRule rule;
						if (DamageSpellCastRule.GetBestRule(selfProcs, instigatingTeam, instigatorDamage, damageClass, out rule)) {
							var proc = damage >= rule.GetMinDamageToProc(damageScale);
							if (proc) {
								rule.Execute(level, spellPower, random2, instigatingTeam, instigatorDamage, instigatingPlayer, instigatorDamage, damage, damageScale, null);
							}
						}
					}
				}
			}
		}

		public enum EConditionTest {
			Always,
			LEqual,
			Less,
			GEqual,
			Greater,
			Equal,
			NotEqual,
			Immobilized,
			NotImmobilized
		}

		public enum EConditionMode {
			RequiredToCast,
			Continuous,
			DisableOnSuccessOrFail
		}

		[Serializable]
		public struct ConditionSpellCastRule {
			public ActorSpellFilterRule affectedActors;
			public SpellClass[] spells;
			[EditorFlags]
			public EFriendlyFire friendlyFire;
			
			public bool Execute(int level, float spellPower, Team instigatingTeam, Actor instigator, ServerPlayerController instigatingPlaying, DamageableActor target, List<Spell> cast) {
				bool didCast = false;

				if (spells != null) {
					for (int i = 0; i < spells.Length; ++i) {
						var s = spells[i];
						if (s != null) {
							var x = target.ServerApplySpell(level, spellPower, s, instigatingTeam, instigator, instigatingPlaying);
							if ((x != null) && (cast != null)) {
								didCast = true;
								cast.Add(x);
							}
						}
					}
				}

				return didCast;
			}

			public static bool GetBestRule(IList<ConditionSpellCastRule> rules, Team instigatingTeam, DamageableActor target, out ConditionSpellCastRule selected) {
				int bestDepth = int.MinValue;
				selected = new ConditionSpellCastRule();

				for (int i = 0; i < rules.Count; ++i) {
					int depth;
					var rule = rules[i];
					if (target.CheckFriendlyFire(rule.friendlyFire, instigatingTeam) && rule.affectedActors.Check(instigatingTeam, target, out depth) && (depth > bestDepth)) {
						bestDepth = depth;
						selected = rule;
					}
				}

				return (bestDepth > int.MinValue);
			}
		}

		public enum EConditionActionCleanseOrExpire {
			None,
			Cleanse,
			Expire
		}

		[Serializable]
		public struct ConditionAction {
			public ConditionSpellCastRule[] instigatorProcs;
			public ConditionSpellCastRule[] targetProcs;
			public EConditionActionCleanseOrExpire cleanseOrExpire;

			public void Execute(int level, float spellPower, Team instigatingTeam, Actor instigator, ServerPlayerController instigatingPlayer, DamageableActor target) {
				if (target != null) {
					if (targetProcs != null) {
						ConditionSpellCastRule rule;
						if (ConditionSpellCastRule.GetBestRule(targetProcs, instigatingTeam, target, out rule)) {
							rule.Execute(level, spellPower, instigatingTeam, instigator, instigatingPlayer, target, null);
						}
					}
				}
				if (instigatorProcs != null) {
					var instigatorDamage = instigator as DamageableActor;
					if (instigatorDamage != null) {
						ConditionSpellCastRule rule;
						if (ConditionSpellCastRule.GetBestRule(instigatorProcs, instigatingTeam, instigatorDamage, out rule)) {
							rule.Execute(level, spellPower, instigatingTeam, instigatorDamage, instigatingPlayer, instigatorDamage, null);
						}
					}
				}
			}
		}

		[Serializable]
		public struct Condition {
			public ActorSpellFilterRule affectedActors;
			public ActorPropertyMetaClass[] propertyClasses;
			public EConditionMode mode;
			public EConditionTest test;
			public ConditionAction successAction;
			public ConditionAction failAction;
            public float rate;
			public float referenceValue;
			public bool scaleByPropertyMaxValue;
			public bool failIfNoMatchingProperties;
			public int maxFailCount;
			public int maxSucceedCount;
		}

		public Condition[] conditions;
						
		[Serializable]
		public struct CleanseOnDamage {
			public DamageMetaClass damageClass;
			[EditorFlags]
			public EFriendlyFire friendlyFire;
			public float minDamageToCleanse;
			public bool scaleWithLevel;
		}

		[Serializable]
		public struct DamageGivenProc {
			public DamageProcEvent procs;
			public DamageRedirect[] selfDamage;
			public DamageModifier[] targetDamage;
			public CleanseOnDamage[] cleanseOnDamage;
			public DamageCooldown[] globalCooldownEffects;
		}

		[Serializable]
		public struct DamageReceivedProc {
			public DamageProcEvent procs;
			public DamageModifier[] selfDamage;
			public DamageRedirect[] targetDamage;
			public CleanseOnDamage[] cleanseOnDamage;
			public DamageCooldown[] globalCooldownEffects;
		}

		public DamageGivenProc damageGiven;
		public DamageReceivedProc damageReceived;

		[Serializable]
		public struct ActorPropertyModifier {
			public ActorSpellFilterRule affectedActors;
			public ActorPropertyMetaClass[] propertyClasses;
			public float baseMin;
			public float baseMinScale;
			public float baseMax;
			public float baseMaxScale;
		}

		public ActorPropertyModifier[] propertyModifiers;

		public enum EPainModifierMode {
			Override,
			Min,
			Max,
			Additional
		}

		[Serializable]
		public struct PainModifier {
			public ActorSpellFilterRule instigatingActors;
			public ActorSpellFilterRule affectedActors;
			public DamageMetaClass damageClass;
			public ActorPropertyMetaClass[] propertyClasses;
			public EPainModifierMode mode;
			[Range(0, 100)]
			public float basePainChance;
			[Range(0, 100)]
			public float scaledPainChance;
		}

		[Serializable]
		public struct PainModifiers {
			public PainModifier[] damageReceived;
			public PainModifier[] damageGiven;
		}

		public PainModifiers painModifiers;

		[Serializable]
		public struct DOT {
			public DamageClass damageClass;
			public float baseDamage;
			public float rate;
			public float delay;
			public bool affectedByTickRate;
		}

		public DOT[] dots;

		[Serializable]
		public struct SpeedModifier {
			public ActorSpellFilterRule affectedActors;
			public float speed;
		}

		public SpeedModifier[] speedModifiers;

		[Serializable]
		public struct TickRateModifier {
			public SpellMetaClass affectedSpellClass;
			public float tickRate;
		}

		public TickRateModifier[] tickRateModifiers;

		[Serializable]
		public struct ResistanceValue {
			[MinMaxSlider(-100f, 100f)]
			public IntMath.Vector2i chanceDelta;
			public float scaleDelta;
			public float maxDamageReductionDelta;
		}

		[Serializable]
		public struct ResitanceModifier {
			public DamageMetaClass affectedDamage;
			public ResistanceValue parry;
			public ResistanceValue block;
			public ResistanceValue resist;
		}

		public ResitanceModifier[] resistanceModifiers;

		[Serializable]
		public struct CheatDeath {
			public ActorSpellFilterRule affectedActors;
			[Range(0, 100f)]
			public float chance;
			[Range(0, 1)]
			public float restoredHealth;
		}

		public CheatDeath[] cheatDeath;

		[Serializable]
		public struct Fear {
			public ActorSpellFilterRule affectedActors;
		}

		public Fear[] fear;

		[Serializable]
		public struct UnitActionRecoveryTimeModifier {
			public UnitActionMetaClass actionMetaClass;
			public float recoveryTimeMultiplier;
		}

		public UnitActionRecoveryTimeModifier[] unitActionRecoveryTimeModifiers;

		[Serializable]
		public struct UnitActionEnabledModifier {
			public UnitActionMetaClass actionMetaClass;
			public bool enabled;
		}

		public UnitActionEnabledModifier[] unitActionEnabledModifiers;

		[Serializable]
		public struct UnitImmobilize {
			public ActorSpellFilterRule affectedActors;
			public EImmobilizeEffect effect;
			public bool pain;
			public EUnitActionCueSlotExplosion painSlot;
		}

		public UnitImmobilize[] immobilize;

		public enum EFogOfWarCanSeeUnderWater {
			Unchanged,
			Yes,
			No
		}

		[Serializable]
		public struct FogOfWarModifier {
			public ActorSpellFilterRule affectedActors;
			public float sightRadiusScale;
			public float additionalSightRadius;
			public float objectRadiusScale;
			public float additionalObjectRadius;
			public float maxVisRadiusScale;
			public float additionalMaxVisRadius;
			public float maxUnderwaterVisRadiusScale;
			public float additionalUnderwaterMaxVisRadius;
			public EFogOfWarCanSeeUnderWater canSeeUnderwater;
		}

		public FogOfWarModifier[] fogOfWar;

		[Serializable]
		public struct DefenseRatingModifier {
			public ActorSpellFilterRule affectedActors;
			[Range(0, 100)]
			public float parryChanceDelta;
			[Range(0, 100)]
			public float blockChanceDelta;
			[Range(0, 100)]
			public float dodgeChanceDelta;
		}

		public DefenseRatingModifier[] defenseRatingModifiers;

		public enum EAttackTargetModifierMode {
			Disabled,
			SetInstigatorAsTargetAtStart,
			SetInstigatorAsTargetContinous
		}

		[Serializable]
		public struct AttackTargetModifier {
			public EAttackTargetModifierMode mode;
		}

		public AttackTargetModifier attackTargetModifier;

		public enum EUnitEmote {
			Celebrate,
			Taunt,
			Alert,
			Spawn,
			Resurrect
		}

		[Serializable]
		public struct EmoteTrigger {
			public ActorSpellFilterRule affectedActors;
			public EUnitEmote emote;
			public string customEmote;
		}

		public EmoteTrigger[] emoteTriggers;
		[Range(-1, 1)]
		public float accuracyBonus;
		[Range(-100, 100)]
		public float dudChanceModifier;


		public static bool CheckCondition(Condition cond, ImmutableActorPropertyInstance property) {
			if (cond.test != EConditionTest.Always) {
				var refVal = cond.referenceValue;
				if (cond.scaleByPropertyMaxValue) {
					refVal *= property.max;
				}

				switch (cond.test) {
					case EConditionTest.LEqual:
						return property.value <= refVal;
					case EConditionTest.Less:
						return property.value < refVal;
					case EConditionTest.GEqual:
						return property.value >= refVal;
					case EConditionTest.Greater:
						return property.value > refVal;
					case EConditionTest.Equal:
						return property.value == refVal;
					case EConditionTest.NotEqual:
						return property.value != refVal;
				}
			}

			return true;
		}

		static bool CheckCondition(Condition cond, DamageableActor actor) {

			//if (cond.test == EConditionTest.Immobilized) {
			//	var u = actor as Unit;
			//	return (u != null) && u.immobilized;
			//}

			//if (cond.test == EConditionTest.NotImmobilized) {
			//	var u = actor as Unit;
			//	return (u == null) || !u.immobilized;
			//}

			if (cond.propertyClasses != null) {
				bool hasClass = false;
				bool hasProp = false;

				for (int i = 0; i < cond.propertyClasses.Length; ++i) {
					var propClass = cond.propertyClasses[i];
					if (propClass != null) {
						hasClass = true;
						var prop = actor.GetProperty(propClass);
						if (prop != null) {
							hasProp = true;
							if (!CheckCondition(cond, prop)) {
								return false;
							}
						}
					}
				}

				if (hasClass && !hasProp && cond.failIfNoMatchingProperties) {
					return false;
				}
			}

			return true;
		}

		public static bool CheckCondition(DamageModifierCondition cond, ImmutableActorPropertyInstance property) {
			if (cond.test != EConditionTest.Always) {
				var refVal = cond.referenceValue;
				if (cond.scaleByPropertyMaxValue) {
					refVal *= property.max;
				}

				switch (cond.test) {
					case EConditionTest.LEqual:
					return property.value <= refVal;
					case EConditionTest.Less:
					return property.value < refVal;
					case EConditionTest.GEqual:
					return property.value >= refVal;
					case EConditionTest.Greater:
					return property.value > refVal;
					case EConditionTest.Equal:
					return property.value == refVal;
					case EConditionTest.NotEqual:
					return property.value != refVal;
				}
			}

			return true;
		}

		public static bool CheckCondition(DamageModifierCondition cond, DamageableActor actor) {

			//if (cond.test == EConditionTest.Immobilized) {
			//	var u = actor as Unit;
			//	return (u != null) && u.immobilized;
			//}

			//if (cond.test == EConditionTest.NotImmobilized) {
			//	var u = actor as Unit;
			//	return (u == null) || !u.immobilized;
			//}

			if (cond.propertyClasses != null) {
				bool hasClass = false;
				bool hasProp = false;

				for (int i = 0; i < cond.propertyClasses.Length; ++i) {
					var propClass = cond.propertyClasses[i];
					if (propClass != null) {
						hasClass = true;
						var prop = actor.GetProperty(propClass);
						if (prop != null) {
							hasProp = true;
							if (!CheckCondition(cond, prop)) {
								return false;
							}
						}
					}
				}

				if (hasClass && !hasProp && cond.failIfNoMatchingProperties) {
					return false;
				}
			}

			return true;
		}

		public override bool CheckPreReqs(Team team, DamageableActor actor) {
			if (!base.CheckPreReqs(team, actor)) {
				return false;
			}

			if (conditions != null) {
				for (int i = 0; i < conditions.Length; ++i) {
					var cond = conditions[i];

					int unused;
					if ((cond.mode == EConditionMode.RequiredToCast) && cond.affectedActors.Check(team, actor, out unused)) {
						if (!CheckCondition(cond, actor)) {
							return false;
						}
					}
					
				}
			}

			return true;
		}

#if UNITY_EDITOR
		protected override void OnInitVersion() {
			base.OnInitVersion();

			if (version < 1) {
				spellClassString = typeof(StandardSpell).FullName;
			}
		}
#endif
	}

}