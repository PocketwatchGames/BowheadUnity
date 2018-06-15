// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using Bowhead.Server.Actors;

namespace Bowhead.Actors.Spells {
	public sealed class StandardSpell : Spell {

		struct DOT {
			public float time;
			public StandardSpellClass.DOT dot;
		}

		struct ResistanceVal {
			public float chanceDelta;
			public float scaleDelta;
			public float maxDamageReductionDelta;
		}

		struct Resistance {
			public DamageMetaClass damageClass;
			public ResistanceVal parry;
			public ResistanceVal block;
			public ResistanceVal resist;
		}

		struct PropertyModifier {
			public ActorPropertyInstance prop;
			public float baseMin;
			public float baseMinScale;
			public float baseMax;
			public float baseMaxScale;
		}

		struct Condition {
			public StandardSpellClass.Condition condition;
			public List<ImmutableActorPropertyInstance> props;
			public float time;
			public int success;
			public int fail;
		}

		public StandardSpell(int level, float spellPower, Server.ServerWorld world, SpellClass spellClass, Team team, Actor instigator, ServerPlayerController instigatingPlayer, DamageableActor target) : base(level, spellPower, world, spellClass, team, instigator, instigatingPlayer, target) {
			this.spellClass = (StandardSpellClass)spellClass;
		}

		List<DOT> _dots;
		List<Resistance> _resistances;
		List<PropertyModifier> _propModifiers;
		List<Condition> _conditions;
		StandardSpellClass.FogOfWarModifier _fogOfWar;
		StandardSpellClass.DefenseRatingModifier _defenseRating;
		StandardSpellClass.UnitImmobilize _immobilize;

		bool _fear;
		float _speedScale;

		public override void OnProcBegin(Spell chainedFrom) {
			base.OnProcBegin(chainedFrom);

			InitConditions();
			InitDOTs();
			InitResistances();
			InitFear();
			InitSpeedModifiers();
			InitPropertyModifiers();
			InitFogOfWar();
			InitImmobilize();
			InitDefenseRating();
			TriggerEmote();

			if (spellClass.attackTargetModifier.mode != StandardSpellClass.EAttackTargetModifierMode.Disabled) {
				ApplyAttackTargetModifier();
			}
		}

		public override void OnProcEnd(EExpiryReason reason, Spell chained, Actor effectingActor, ServerPlayerController effectingPlayer) {
			base.OnProcEnd(reason, chained, effectingActor, effectingPlayer);

			if (chained == null) {
				ClearImmobilize();
			}
		}

		protected override void OnMutedChanged() {
			base.OnMutedChanged();

			if (muted) {
				ClearImmobilize();
			} else {
				DoImmobilize();
			}
		}

		protected override void OnSuspendedChanged() {
			base.OnSuspendedChanged();

			if (muted) { // NOTE: "muted" tests for muted || suspended!
				ClearImmobilize();
			} else {
				DoImmobilize();
			}
		}

		public override void ServerBeginUpdate(float unscaledDt) {
			base.ServerBeginUpdate(unscaledDt);

			if (suspended || disposed) {
				return;
			}

			ApplyConditions(unscaledDt);

			if (disposed) {
				return;
			}

			ApplyResistances();
			ApplyTickRates();
		}

		public override void ServerUpdate(float dt, float unscaledDt) {
			base.ServerUpdate(dt, unscaledDt);

			if (suspended || disposed) {
				return;
			}

			if (_fear) {
				target.feared = true;
			}

			ApplyDOTs(dt, unscaledDt);
			ApplyPropertyModifiers();
			ApplyUnitActionEnabledModifiers();
			ApplyUnitActionRecoveryTimeModifiers();
			ApplyFogOfWar();
			ApplyDefenseRating();

			if (spellClass.attackTargetModifier.mode == StandardSpellClass.EAttackTargetModifierMode.SetInstigatorAsTargetContinous) {
				ApplyAttackTargetModifier();
			}

			//if (uTarget != null) {
			//	uTarget.serverSpeedScale *= _speedScale;
			//}
		}

		void InitConditions() {
			if (spellClass.conditions != null) {
				for (int i = 0; i < spellClass.conditions.Length; ++i) {
					var cond = spellClass.conditions[i];

					int unused;
					if ((cond.mode != StandardSpellClass.EConditionMode.RequiredToCast) && cond.affectedActors.Check(team, target, out unused)) {
						List<ImmutableActorPropertyInstance> props = new List<ImmutableActorPropertyInstance>();
						if (cond.propertyClasses != null) {
							for (int k = 0; k < cond.propertyClasses.Length; ++k) {
								var propClass = cond.propertyClasses[k];
								if (propClass != null) {
									var prop = target.GetProperty(propClass);
									if (prop != null) {
										props.Add(prop);
									}
								}
							}
						}

						if (props.Count > 0) {
							Condition cw = new Condition();
							cw.condition = cond;
							cw.props = props;
							
							if (_conditions == null) {
								_conditions = new List<Condition>();
							}
							_conditions.Add(cw);
						}
					}
				}
			}
		}

		void ApplyConditions(float dt) {
			if (_conditions != null) {
				for (int i = _conditions.Count-1; i >= 0; --i) {
					var cond = _conditions[i];

					bool success = false;
					bool fail = false;

					cond.time -= dt;

					if (cond.time <= 0f) {
						cond.time = cond.condition.rate;
						
						for (int k = 0; k < cond.props.Count; ++k) {
							if (StandardSpellClass.CheckCondition(cond.condition, cond.props[k])) {
								success = true;
								break;
							} else {
								fail = true;
							}
						}

						if (success) {
							++cond.success;
							if ((cond.condition.maxSucceedCount <= 0) || (cond.success <= cond.condition.maxSucceedCount)) {
								cond.condition.successAction.Execute(level, spellPower, team, instigatingActor, instigatingPlayer, target);
							}
							if (!disposed) {
								switch (cond.condition.successAction.cleanseOrExpire) {
									case StandardSpellClass.EConditionActionCleanseOrExpire.Cleanse:
										OnProcEnd(EExpiryReason.Cleansed, null, instigatingActor, instigatingPlayer);
									break;
									case StandardSpellClass.EConditionActionCleanseOrExpire.Expire:
										OnProcEnd(EExpiryReason.Expired, null, instigatingActor, instigatingPlayer);
									break;
								}
							}
						} else if (fail) {
							++cond.fail;
							if ((cond.condition.maxFailCount <= 0) || (cond.fail <= cond.condition.maxFailCount)) {
								cond.condition.failAction.Execute(level, spellPower, team, instigatingActor, instigatingPlayer, target);
							}
							if (!disposed) {
								switch (cond.condition.failAction.cleanseOrExpire) {
									case StandardSpellClass.EConditionActionCleanseOrExpire.Cleanse:
										OnProcEnd(EExpiryReason.Cleansed, null, instigatingActor, instigatingPlayer);
									break;
									case StandardSpellClass.EConditionActionCleanseOrExpire.Expire:
										OnProcEnd(EExpiryReason.Expired, null, instigatingActor, instigatingPlayer);
									break;
								}
							}
						}

					}

					_conditions[i] = cond;
					
					if (success || fail) {
						if (cond.condition.mode == StandardSpellClass.EConditionMode.DisableOnSuccessOrFail) {
							_conditions.RemoveAt(i);
						}
					}
				}
			}
		}

		void InitDOTs() {
			if ((spellClass.dots != null) && (spellClass.dots.Length > 0)) {
				_dots = new List<DOT>();
				for (int i = 0; i < spellClass.dots.Length; ++i) {
					var dotClass = spellClass.dots[i];
					if (dotClass.damageClass != null) {
						var dot = new DOT();
						dot.time = dotClass.delay;
						dot.dot = dotClass;
						_dots.Add(dot);
					}
				}
				if (_dots.Count < 1) {
					_dots = null;
				}
			}
		}

		void ApplyDOTs(float dt, float unscaledDt) {
			if (_dots != null) {
				for (int i = 0; i < _dots.Count; ++i) {
					var dot = _dots[i];

					if (dot.dot.affectedByTickRate) {
						dot.time -= dt;
					} else {
						dot.time -= unscaledDt;
					}
					if (dot.time <= 0f) {
						dot.time = dot.dot.rate;

						// deal damage.
						if (!muted) {
							DamageEvent damage = new DamageEvent();
							damage.effectingActor = instigatingActor;
							damage.instigatingTeam = team;
							damage.instigatingActor = instigatingActor;
							damage.instigatingPlayer = instigatingPlayer;
							damage.targetActor = target;
							damage.targetPlayer = target.serverOwningPlayer;
							damage.pain = EUnitActionCueSlotExplosion.MidCenter;
							damage.damageClass = dot.dot.damageClass;
							damage.damageLevel = level;
							damage.damageSpellPower = spellPower;
							damage.gibForce = dot.dot.baseDamage;
							damage.damage = damage.gibForce*spellPower;

							DamageableActor.ServerExecuteDamage(world, damage);
						}
					}

					_dots[i] = dot;

					if (target.dead) {
						break;
					}
				}
			}
		}

		void InitResistances() {
			if (spellClass.resistanceModifiers != null) {
				_resistances = new List<Resistance>();
				for (int i = 0; i < spellClass.resistanceModifiers.Length; ++i) {
					var m = spellClass.resistanceModifiers[i];
					if (m.affectedDamage != null) {
						var r = new Resistance();
						r.damageClass = m.affectedDamage;
						r.parry.chanceDelta = Mathf.Lerp(m.parry.chanceDelta.x, m.parry.chanceDelta.y, GameManager.instance.randomNumber);
						r.parry.scaleDelta = m.parry.scaleDelta;
						r.parry.maxDamageReductionDelta = m.parry.maxDamageReductionDelta*spellPower;

						r.block.chanceDelta = Mathf.Lerp(m.block.chanceDelta.x, m.block.chanceDelta.y, GameManager.instance.randomNumber);
						r.block.scaleDelta = m.block.scaleDelta;
						r.block.maxDamageReductionDelta = m.block.maxDamageReductionDelta*spellPower;

						r.resist.chanceDelta = Mathf.Lerp(m.resist.chanceDelta.x, m.resist.chanceDelta.y, GameManager.instance.randomNumber);
						r.resist.scaleDelta = m.resist.scaleDelta;
						r.resist.maxDamageReductionDelta = m.resist.maxDamageReductionDelta*spellPower;

						_resistances.Add(r);
					}
				}

				if (_resistances.Count < 1) {
					_resistances = null;
				}
			}
		}

		void ApplyResistances() {
			if (_resistances != null) {
				//var u = target as Unit;
				//for (int i = 0; i < _resistances.Count; ++i) {
				//	var m = _resistances[i];
				//	var r = target.GetResistance(m.damageClass);

				//	r.parryDamageScale = Mathf.Clamp01(r.parryDamageScale + m.parry.scaleDelta);
				//	r.parryMaxDamageReduction = Mathf.Max(0f, r.parryMaxDamageReduction + m.parry.maxDamageReductionDelta);

				//	r.blockDamageScale = Mathf.Clamp01(r.blockDamageScale + m.block.scaleDelta);
				//	r.blockMaxDamageReduction = Mathf.Max(0f, r.blockMaxDamageReduction + m.block.maxDamageReductionDelta);

				//	r.resistChance = Mathf.Clamp(r.resistChance + m.resist.chanceDelta, 0, 100);
				//	r.resistDamageScale = Mathf.Clamp01(r.resistDamageScale + m.resist.scaleDelta);
				//	r.resistMaxDamageReduction = Mathf.Max(0f, r.resistMaxDamageReduction + m.resist.maxDamageReductionDelta);

				//	if (u != null) {
				//		u.parryChance = Mathf.Clamp(u.parryChance + m.parry.chanceDelta, 0, 100);
				//		u.blockChance = Mathf.Clamp(u.blockChance + m.block.chanceDelta, 0, 100);
				//	}
				//}
			}
		}

		void InitFear() {
			if (spellClass.fear != null) {
				for (int i = 0; i < spellClass.fear.Length; ++i) {
					var f = spellClass.fear[i];
					int unused;
					if (f.affectedActors.Check(team, target, out unused)) {
						_fear = true;
						break;
					}
				}
			}
		}

		void ApplyTickRates() {
			if (spellClass.tickRateModifiers != null) {
				var spells = target.serverActiveSpells;
				for (int i = 0; i < spells.Count; ++i) {
					var spell = spells[i];
					if ((spell != this) && !spell.disposed) {
						int bestDepth = int.MinValue;
						float bestTickRate = 1f;

						for (int k = 0; k < spellClass.tickRateModifiers.Length; ++k) {
							var m = spellClass.tickRateModifiers[k];
							if ((m.affectedSpellClass != null) && (m.affectedSpellClass.depth > bestDepth) && spell.spellClass.metaClass.IsA(m.affectedSpellClass)) {
								bestDepth = m.affectedSpellClass.depth;
								bestTickRate = m.tickRate;
							}
						}

						if (bestDepth > int.MinValue) {
							spell.tickRate = Mathf.Max(0, bestTickRate);
						}
					}
				}
			}
		}

		void InitSpeedModifiers() {
			_speedScale = 1f;

			if ((spellClass.speedModifiers != null)/* && (uTarget != null)*/) {
				int bestDepth = int.MinValue;
				StandardSpellClass.SpeedModifier speedModifier = new StandardSpellClass.SpeedModifier();

				for (int i = 0; i < spellClass.speedModifiers.Length; ++i) {
					var m = spellClass.speedModifiers[i];
					int depth;
					if (m.affectedActors.Check(team, target, out depth) && (depth > bestDepth)) {
						bestDepth = depth;
						speedModifier = m;
					}
				}

				if (bestDepth > int.MinValue) {
					_speedScale = speedModifier.speed;
				}
			}
		}

		void InitPropertyModifiers() {
			if (spellClass.propertyModifiers != null) {
				_propModifiers = new List<PropertyModifier>();

				var props = target.mutableProperties;
				for (int i = 0; i < props.Count; ++i) {
					var prop = props[i];
					int bestDepth = int.MinValue;
					StandardSpellClass.ActorPropertyModifier mod = new StandardSpellClass.ActorPropertyModifier();

					for (int k = 0; k < spellClass.propertyModifiers.Length; ++k) {
						var pm = spellClass.propertyModifiers[i];
						if (prop.property.IsAny(pm.propertyClasses)) {
							int depth;
							if (pm.affectedActors.Check(team, target, out depth) && (depth > bestDepth)) {
								bestDepth = depth;
								mod = pm;
							}
						}
					}

					if (bestDepth > int.MinValue) {
						var pm = new PropertyModifier();
						pm.prop = prop;

						pm.baseMin = spellPower*mod.baseMin;
						pm.baseMinScale = spellPower*mod.baseMinScale;
						pm.baseMax = spellPower*mod.baseMax;
						pm.baseMaxScale = spellPower*mod.baseMaxScale;
						
						_propModifiers.Add(pm);
					}
				}

				if (_propModifiers.Count < 1) {
					_propModifiers = null;
				}
			}
		}

		void ApplyPropertyModifiers() {
			if (_propModifiers != null) {
				for (int i = 0; i < _propModifiers.Count; ++i) {
					var pm = _propModifiers[i];
					pm.prop.min = pm.baseMin + (pm.prop.property.minValue * pm.baseMinScale);
					pm.prop.max = pm.baseMax + (pm.prop.property.maxValue * pm.baseMaxScale);
				}
			}
		}

		//void ApplyUnitActionRecoveryTimeModifiers(UnitActionBase action) {
		//	int bestDepth = int.MinValue;
		//	StandardSpellClass.UnitActionRecoveryTimeModifier mod = new StandardSpellClass.UnitActionRecoveryTimeModifier();

		//	for (int i = 0; i < spellClass.unitActionRecoveryTimeModifiers.Length; ++i) {
		//		var m = spellClass.unitActionRecoveryTimeModifiers[i];
		//		if ((m.actionMetaClass.depth > bestDepth) && action.actionClass.metaClass.IsA(m.actionMetaClass)) {
		//			bestDepth = m.actionMetaClass.depth;
		//			mod = m;
		//		}
		//	}

		//	if (bestDepth > int.MinValue) {
		//		action.recoveryTime = Mathf.Max(action.recoveryTime * mod.recoveryTimeMultiplier, 0);

		//		//var counter = action as UnitCounterAction;
		//		//if (counter != null) {
		//		//	counter.actionRecoveryTime = Mathf.Max(counter.actionRecoveryTime * mod.recoveryTimeMultiplier, 0);
		//		//}
		//	}
		//}

		void ApplyUnitActionRecoveryTimeModifiers() {
			//if ((uTarget != null) && (spellClass.unitActionRecoveryTimeModifiers != null)) {
			//	uTarget.ApplyActionsFunc(ApplyUnitActionRecoveryTimeModifiers);
			//}
		}

		//void ApplyUnitActionEnabledModifiers(UnitActionBase action) {
		//	int bestDepth = int.MinValue;
		//	StandardSpellClass.UnitActionEnabledModifier mod = new StandardSpellClass.UnitActionEnabledModifier();

		//	for (int i = 0; i < spellClass.unitActionEnabledModifiers.Length; ++i) {
		//		var m = spellClass.unitActionEnabledModifiers[i];
		//		if ((m.actionMetaClass != null) && (m.actionMetaClass.depth > bestDepth) && action.actionClass.metaClass.IsA(m.actionMetaClass)) {
		//			bestDepth = m.actionMetaClass.depth;
		//			mod = m;
		//		}
		//	}

		//	if (bestDepth > int.MinValue) {
		//		action.enabled = mod.enabled;
		//	}
		//}

		void ApplyUnitActionEnabledModifiers() {
			//if ((uTarget != null) && (spellClass.unitActionEnabledModifiers != null)) {
			//	uTarget.ApplyActionsFunc(ApplyUnitActionEnabledModifiers);
			//}
		}

		void ApplyAttackTargetModifier() {
			//if ((uTarget != null) && (dInstigator != null) && !(dInstigator.pendingKill || dInstigator.dead)) {
			//	uTarget.aggroOverrideTarget = dInstigator;
			//}
		}

		void InitFogOfWar() {
			int bestDepth = int.MinValue;
			_fogOfWar = new StandardSpellClass.FogOfWarModifier();

			_fogOfWar.sightRadiusScale = 1f;
			_fogOfWar.objectRadiusScale = 1f;
			_fogOfWar.maxVisRadiusScale = 1f;
			_fogOfWar.maxUnderwaterVisRadiusScale = 1f;
			_fogOfWar.canSeeUnderwater = StandardSpellClass.EFogOfWarCanSeeUnderWater.Unchanged;

			if (spellClass.fogOfWar != null) {
				for (int i = 0; i < spellClass.fogOfWar.Length; ++i) {
					var m = spellClass.fogOfWar[i];
					int depth;
					if (m.affectedActors.Check(team, target, out depth) && (depth > bestDepth)) {
						_fogOfWar = m;
						bestDepth = depth;
					}
				}
			}
		}

		void ApplyFogOfWar() {
			target.fogOfWarSightRadius = (target.fogOfWarSightRadius * _fogOfWar.sightRadiusScale) + _fogOfWar.additionalSightRadius;
			target.fogOfWarUnderwaterMaxVisRadius = (target.fogOfWarSightRadius * _fogOfWar.sightRadiusScale) + _fogOfWar.additionalSightRadius;
			target.fogOfWarObjectRadius = (target.fogOfWarObjectRadius * _fogOfWar.objectRadiusScale) + _fogOfWar.additionalObjectRadius;
			target.fogOfWarUnderwaterMaxVisRadius = (target.fogOfWarUnderwaterMaxVisRadius * _fogOfWar.maxUnderwaterVisRadiusScale) + _fogOfWar.additionalUnderwaterMaxVisRadius;
			if (_fogOfWar.canSeeUnderwater != StandardSpellClass.EFogOfWarCanSeeUnderWater.Unchanged) {
				target.fogOfWarCanSeeUnderwater = (_fogOfWar.canSeeUnderwater == StandardSpellClass.EFogOfWarCanSeeUnderWater.Yes);
			}
		}

		void InitImmobilize() {
			int bestDepth = int.MinValue;
			_immobilize = new StandardSpellClass.UnitImmobilize();

			if (spellClass.immobilize != null) {
				for (int i = 0; i < spellClass.immobilize.Length; ++i) {
					var m = spellClass.immobilize[i];
					if ((m.effect != EImmobilizeEffect.None) || m.pain) {
						int depth;
						if (m.affectedActors.Check(team, target, out depth) && (depth > bestDepth)) {
							_immobilize = m;
							bestDepth = depth;
						}
					}
				}
			}

			if (bestDepth > int.MinValue) {
				DoImmobilize();
			}
		}

		void DoImmobilize() {
			if (!(target.dead || target.pendingKill) && ((_immobilize.effect != EImmobilizeEffect.None) || _immobilize.pain)) {
				target.Interrupt(float.MaxValue, _immobilize.effect, _immobilize.pain ? _immobilize.painSlot : 0);
			}
		}

		void ClearImmobilize() {
			if (!(target.dead || target.pendingKill) && ((_immobilize.effect != EImmobilizeEffect.None) || _immobilize.pain)) {
				target.Interrupt(0f, EImmobilizeEffect.None, 0);
			}
		}
				
		void InitDefenseRating() {
			//if (uTarget != null) {
			//	int bestDepth = int.MinValue;
			//	_defenseRating = new StandardSpellClass.DefenseRatingModifier();

			//	if (spellClass.defenseRatingModifiers != null) {
			//		for (int i = 0; i < spellClass.defenseRatingModifiers.Length; ++i) {
			//			var m = spellClass.defenseRatingModifiers[i];
			//			int depth;
			//			if (m.affectedActors.Check(team, target, out depth) && (depth > bestDepth)) {
			//				_defenseRating = m;
			//				bestDepth = depth;
			//			}
			//		}
			//	}
			//}
		}

		void ApplyDefenseRating() {
			//if (uTarget != null) {
			//	uTarget.parryChance = Mathf.Clamp(uTarget.parryChance + _defenseRating.parryChanceDelta, 0, 100f);
			//	uTarget.dodgeChance = Mathf.Clamp(uTarget.dodgeChance + _defenseRating.dodgeChanceDelta, 0, 100f);
			//	uTarget.blockChance = Mathf.Clamp(uTarget.blockChance + _defenseRating.blockChanceDelta, 0, 100f);
			//}
		}

		void TriggerEmote() {
			//if ((spellClass.emoteTriggers != null) && (uTarget != null)) {
			//	int bestDepth = int.MinValue;
			//	var best = new StandardSpellClass.EmoteTrigger();

			//	for (int i = 0; i < spellClass.emoteTriggers.Length; ++i) {
			//		var trigger = spellClass.emoteTriggers[i];
			//		int depth;
			//		if (trigger.affectedActors.Check(team, target, out depth) && (depth > bestDepth)) {
			//			best = trigger;
			//			bestDepth = depth;
			//		}
			//	}

			//	if (bestDepth > int.MinValue) {
			//		if (!string.IsNullOrEmpty(best.customEmote)) {
			//		//	uTarget.Multicast_Emote(best.customEmote);
			//		} else {
			//		//	uTarget.Multicast_Emote(best.emote);
			//		}
			//	}
			//}
		}

		public override bool ProcCheatDeath(DamageEvent damage) {

			// get best cheat death.
			if (spellClass.cheatDeath != null) {
				int bestDepth = int.MinValue;
				StandardSpellClass.CheatDeath cheatDeath = new StandardSpellClass.CheatDeath();

				for (int i = 0; i < spellClass.cheatDeath.Length; ++i) {
					var x = spellClass.cheatDeath[i];
					int depth;
					if (x.affectedActors.Check(team, target, out depth) && (depth > bestDepth)) {
						bestDepth = depth;
						cheatDeath = x;
					}
				}

				if (bestDepth > int.MinValue) {
					if (cheatDeath.chance > 0f) {
						if ((GameManager.instance.randomNumber*100f) <= cheatDeath.chance) {
							var health = Mathf.Lerp(0, cheatDeath.restoredHealth, GameManager.instance.randomNumber);
							if (health > 0f) {
								var p = target.mutableHealth;
								p.value = Mathf.Lerp(p.min, p.max, health);
								if (p.value > 0f) {
									return true;
								}
							}
						}
					}
				}
			}
			
			return false;
		}

		public override ActorPropertyInstance ProcDamageGiven(ActorPropertyInstance property, DamageEvent damage, DamageClass.Channel channel, ref DamageMetaClass damageClass, ref float amount) {

			if (CheckCleanseOnDamage(damage.instigatingTeam, spellClass.damageGiven.cleanseOnDamage, damageClass, amount)) {
				if (!disposed) {
					OnProcEnd(EExpiryReason.Cleansed, null, damage.effectingActor, damage.instigatingPlayer);
				}
				return property;
			}

			DamageCooldowns(target, spellClass.damageGiven.globalCooldownEffects, damage, damageClass, amount);

			var gm = GameManager.instance;

			spellClass.damageGiven.procs.Execute(level, spellPower, gm.randomNumber, gm.randomNumber, team, target, target.serverOwningPlayer, damage.targetActor, damageClass, amount, spellPower);

			RedirectDamage(target, spellClass.damageGiven.selfDamage, damage, damageClass, amount);

			return ModifyDamage(property, spellClass.damageGiven.targetDamage, damage.targetActor, damage.targetPlayer, damage.distance, ref damageClass, ref amount);
		}

		public override ActorPropertyInstance ProcDamageReceived(ActorPropertyInstance property, DamageEvent damage, DamageClass.Channel channel, ref DamageMetaClass damageClass, ref float amount) {

			if (CheckCleanseOnDamage(damage.instigatingTeam, spellClass.damageReceived.cleanseOnDamage, damageClass, amount)) {
				if (!disposed) {
					OnProcEnd(EExpiryReason.Cleansed, null, damage.effectingActor, damage.instigatingPlayer);
				}
				return property;
			}

			DamageCooldowns(target, spellClass.damageReceived.globalCooldownEffects, damage, damageClass, amount);

			var dInstigator = damage.instigatingActor as DamageableActor;

			var gm = GameManager.instance;

			spellClass.damageReceived.procs.Execute(level, spellPower, gm.randomNumber, gm.randomNumber, team, target, target.serverOwningPlayer, dInstigator, damageClass, amount, spellPower);

			RedirectDamage(damage.instigatingActor as DamageableActor, spellClass.damageReceived.targetDamage, damage, damageClass, amount);

			return ModifyDamage(property, spellClass.damageReceived.selfDamage, damage.targetActor, damage.targetPlayer, damage.distance, ref damageClass, ref amount);
		}

		public override float ProcPainChanceForDamageReceived(DamageEvent damage, ActorPropertyInstance property, float amount, float painChance, DamageClass.Channel channel) {
			if ((spellClass.painModifiers.damageReceived != null) && (spellClass.painModifiers.damageReceived.Length > 0)) {
				int bestDepth = int.MinValue;
				StandardSpellClass.PainModifier mod = new StandardSpellClass.PainModifier();

				var instigator = damage.instigatingActor as DamageableActor;

				for (int i = 0; i < spellClass.painModifiers.damageReceived.Length; ++i) {
					var test = spellClass.painModifiers.damageReceived[i];
					int depth;
					int unused;
					if (property.property.IsAny(test.propertyClasses) && test.affectedActors.Check(team, target, out depth) && (depth > bestDepth) && (test.instigatingActors.empty || ((instigator != null) && test.instigatingActors.Check(team, instigator, out unused)))) {
						mod = test;
						bestDepth = depth;
					}
				}

				if (bestDepth > int.MinValue) {
					var scaledPainChance = mod.scaledPainChance;
					if (scaledPainChance > 0) {
						scaledPainChance = 1f/scaledPainChance*100;
					}

					var newPainChance = Mathf.Clamp(mod.basePainChance + (scaledPainChance*amount/property.max), 0, 100);
					switch (mod.mode) {
						case StandardSpellClass.EPainModifierMode.Min:
							return Mathf.Min(newPainChance, painChance);
						case StandardSpellClass.EPainModifierMode.Max:
							return Mathf.Max(newPainChance, painChance);
					}
					return newPainChance;
				}
			}
			return base.ProcPainChanceForDamageReceived(damage, property, amount, painChance, channel);
		}

		public override float ProcPainChanceForDamageGiven(DamageEvent damage, DamageableActor target, ActorPropertyInstance property, float amount, float painChance, DamageClass.Channel channel) {
			if ((spellClass.painModifiers.damageGiven != null) && (spellClass.painModifiers.damageGiven.Length > 0)) {
				int bestDepth = int.MinValue;
				int bestDepth2 = int.MinValue;
				StandardSpellClass.PainModifier mod = new StandardSpellClass.PainModifier();
				var damageMetaClass = channel.metaClass;

				for (int i = 0; i < spellClass.painModifiers.damageGiven.Length; ++i) {
					var test = spellClass.painModifiers.damageGiven[i];
					int depth;
					int unused;
					if (property.property.IsAny(test.propertyClasses) && test.affectedActors.Check(team, target, out depth) && (depth >= bestDepth) && (test.instigatingActors.empty || ((dInstigator != null) && test.instigatingActors.Check(team, dInstigator, out unused)))) {
						int depth2 = (test.damageClass != null) ? test.damageClass.depth : -1;
						if ((depth2 > bestDepth2) || (depth > bestDepth)) {
							if ((test.damageClass == null) || damageMetaClass.IsA(test.damageClass)) {
								mod = test;
								bestDepth = depth;
								bestDepth2 = depth2;
							}
						}
					}
				}

				if (bestDepth > int.MinValue) {
					var scaledPainChance = mod.scaledPainChance;
					if (scaledPainChance > 0) {
						scaledPainChance = 1f/scaledPainChance*100;
					}

					var newPainChance = Mathf.Clamp(mod.basePainChance + (scaledPainChance*amount/property.max), 0, 100);
					switch (mod.mode) {
						case StandardSpellClass.EPainModifierMode.Min:
							return Mathf.Min(newPainChance, painChance);
						case StandardSpellClass.EPainModifierMode.Max:
							return Mathf.Max(newPainChance, painChance);
						case StandardSpellClass.EPainModifierMode.Additional:
							return Mathf.Clamp(newPainChance+painChance, 0, 100);
					}
					return newPainChance;
				}
			}
			return base.ProcPainChanceForDamageGiven(damage, target, property, amount, painChance, channel);
		}
		
		bool CheckCleanseOnDamage(Team instigatingTeam, StandardSpellClass.CleanseOnDamage[] cleanse, DamageMetaClass damageClass, float damage) {
			if (cleanse != null) {
				int bestDepth = int.MinValue;
				StandardSpellClass.CleanseOnDamage mod = new StandardSpellClass.CleanseOnDamage();

				for (int i = 0; i < cleanse.Length; ++i) {
					var m = cleanse[i];
					if ((m.damageClass != null) && (m.damageClass.depth > bestDepth) && damageClass.IsA(m.damageClass)) {
						bestDepth = m.damageClass.depth;
						mod = m;
					}
				}

				if (bestDepth > int.MinValue) {
					var minDamageToCleanse = mod.minDamageToCleanse;
					if (mod.scaleWithLevel) {
						minDamageToCleanse = spellPower*minDamageToCleanse;
					}

					if (target.CheckFriendlyFire(mod.friendlyFire, instigatingTeam) && (damage >= minDamageToCleanse)) {
						return true;
					}
				}
			}

			return false;
		}

		void DamageCooldowns(DamageableActor target, StandardSpellClass.DamageCooldown[] cooldowns, DamageEvent damage, DamageMetaClass srcDamageClass, float amount) {
			if ((target != null) && (target.serverOwningPlayer != null) && (cooldowns != null) && (cooldowns.Length > 0)) {
				int bestDepth = int.MinValue;
				int bestDepth2 = int.MinValue;
				StandardSpellClass.DamageCooldown cd = new StandardSpellClass.DamageCooldown();

				for (int i = 0; i < cooldowns.Length; ++i) {
					var test = cooldowns[i];

					int depth;
					if ((test.srcDamageClass != null) && (test.affectedActors.Check(damage.instigatingTeam, target, out depth) && (depth >= bestDepth)) && ((test.srcDamageClass.depth > bestDepth2) || (depth > bestDepth)) && srcDamageClass.IsA(test.srcDamageClass)) {
						bestDepth = depth;
						bestDepth2 = test.srcDamageClass.depth;
						cd = test;
					}
				}

				if (bestDepth > int.MinValue) {
					var unscaledAmount = amount / spellPower;
					var time = cd.baseTime + (cd.damageScale.GetValue(unscaledAmount, spellPower) * cd.timeScale);
					target.serverOwningPlayer.GlobalAdvanceCooldown(time);
				}
			}
		}

		void RedirectDamage(DamageableActor target, StandardSpellClass.DamageRedirect[] redirects, DamageEvent damage, DamageMetaClass srcDamageClass, float amount) {
			if ((target != null) && (redirects != null) && (redirects.Length > 0)) {
				int bestDepth = int.MinValue;
				int bestDepth2 = int.MinValue;
				StandardSpellClass.DamageRedirect dmg = new StandardSpellClass.DamageRedirect();

				for (int i = 0; i < redirects.Length; ++i) {
					var red = redirects[i];

					int depth;
					if ((red.srcDamageClass != null) && (red.affectedActors.Check(damage.instigatingTeam, target, out depth) && (depth >= bestDepth)) && ((red.srcDamageClass.depth > bestDepth2) || (depth > bestDepth)) && srcDamageClass.IsA(red.srcDamageClass)) {
						bestDepth = depth;
						bestDepth2 = red.srcDamageClass.depth;
						dmg = red;
					}
				}

				if (bestDepth > int.MinValue) {
					if ((dmg.chance >= 100) || ((GameManager.instance.randomNumber*100) < dmg.chance)) {
						DamageEvent newDamage = new DamageEvent();
						newDamage.damageClass = dmg.redirectDamageClass;
						newDamage.gibForce = amount + dmg.additionalDamage;
						newDamage.damage = dmg.damageScale.GetValue(amount, spellPower) + (spellPower*dmg.additionalDamage);
						newDamage.damageLevel = level;
						newDamage.damageSpellPower = spellPower;
						newDamage.instigatingActor = this.target;
						newDamage.instigatingPlayer = this.target.serverOwningPlayer;
						newDamage.instigatingTeam = team;
						newDamage.targetActor = target;
						newDamage.targetPlayer = target.serverOwningPlayer;

						DamageableActor.ServerExecuteDamage(world, newDamage);
					}
				}
			}
		}

		ActorPropertyInstance ModifyDamage(ActorPropertyInstance property, StandardSpellClass.DamageModifier[] modifiers, DamageableActor target, ServerPlayerController player, float distance, ref DamageMetaClass damageClass, ref float amount) {

			if((modifiers != null) && (modifiers.Length > 0)) {
				int bestDepth = int.MinValue;
				int bestDepth2 = int.MinValue;

				StandardSpellClass.DamageModifier dmg = new StandardSpellClass.DamageModifier();
				ActorPropertyInstance redirectProperty = null;
				DamageMetaClass redirectDamageClass = null;

				for (int i = 0; i < modifiers.Length; ++i) {
					var m = modifiers[i];

					int depth;

					if ((m.srcDamageClass != null) && (m.affectedActors.Check(team, target, out depth) && (depth >= bestDepth)) && ((m.srcDamageClass.depth > bestDepth2) || (depth > bestDepth)) && damageClass.IsA(m.srcDamageClass) ) {

						var p = (m.dstProperty != null) ? target.GetMutableProperty(m.dstProperty) : null;
						redirectProperty = p ?? property;

						redirectDamageClass = m.dstDamageClass ?? damageClass;

						bestDepth = depth;
						bestDepth2 = m.srcDamageClass.depth;
						dmg = m;
					}
				}

				if (bestDepth > int.MinValue) {
					if ((dmg.chance >= 100) || ((GameManager.instance.randomNumber*100) < dmg.chance)) {
						if (StandardSpellClass.CheckCondition(dmg.condition, target)) {
							damageClass = redirectDamageClass;
							amount = dmg.damageScale.GetValue(amount, spellPower) + (dmg.additionalDamage*spellPower);

							var travel = distance * dmg.damageBonusPerMeterTraveled * spellPower;
							if (dmg.maxTraveledDamageBonus > 0) {
								travel = Mathf.Min(travel, dmg.maxTraveledDamageBonus*spellPower);
							}

							amount += travel;

							return redirectProperty;
						}
					}
				}
			}

			return property;
		}

		public override float ProcAccuracyBonus() {
			return spellClass.accuracyBonus;
		}

		public override float ProcDudChanceModifier() {
			return spellClass.dudChanceModifier;
		}

		public new StandardSpellClass spellClass {
			get;
			private set;
		}
	}
}