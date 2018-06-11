using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bowhead.Actors {
	[Flags]
	public enum EFriendlyFire {
		Friends = 0x1,
		Enemies = 0x2
	}
	
	public abstract class DamageClass : VersionedObject {
		public bool spawnBloodFX;

		[Serializable]
		public struct ActorDamageFilterRule {
			[SerializeField]
			ActorMetaClass[] affectedClasses;
			[SerializeField]
			ActorMetaClass[] excludedClasses;
			public ActorPropertyMetaClass[] affectedProperties;
			public DamageProcEvent procOnHit;
			public DamageProcEvent procOnDamage;
			[Range(0, 100)]
			public float basePainChance;
			[Range(0, 100)]
			public float scaledPainChance;
			[HideInInspector]
			public bool initialized;

			public bool Check(DamageableActor actor, out int depth) {
				var metaClass = actor.metaClass;

				depth = -1;

				// excluded
				for (int i = 0; i < excludedClasses.Length; ++i) {
					var c = excludedClasses[i];
					if ((c != null) && metaClass.IsA(c)) {
						return false;
					}
				}

				bool selected = false;

				for (int i = 0; i < affectedClasses.Length; ++i) {
					var c = affectedClasses[i];
					if ((c != null) && metaClass.IsA(c) && (c.depth > depth)) {
						depth = c.depth;
						selected = true;
					}
				}

				return (affectedClasses.Length == 0) || selected;
			}

			public bool empty {
				get {
					return (affectedClasses.Length == 0) &&
						(excludedClasses.Length == 0);
				}
			}

			public void ClientPrecache() {
				procOnHit.ClientPrecache();
				procOnDamage.ClientPrecache();
			}

			public static void ClientPrecache(IList<ActorDamageFilterRule> rules) {
				if (rules != null) {
					for (int i = 0; i < rules.Count; ++i) {
						rules[i].ClientPrecache();
					}
				}
			}
		}

		[Serializable]
		public struct DamageSpellClass {
			public Spells.SpellClass[] spells;
			public bool scaleDurationByDamage;
			public float baseDuration;
			public float durationScale;
			public float maxDuration;

			public void ClientPrecache() {
				Spells.SpellClass.ClientPrecache(spells);
			}

			public static void ClientPrecache(IList<DamageSpellClass> classes) {
				if (classes != null) {
					for (int i = 0; i < classes.Count; ++i) {
						var c = classes[i];
						c.ClientPrecache();
					}
				}
			}
		}

		[Serializable]
		public struct DamageSpellCastRule {
			public DamageSpellClass[] spells;
			[EditorFlags]
			public EFriendlyFire friendlyFire;
			public float minDamageToProc;
			[Range(0, 100)]
			public float chance;

			public bool Execute(int level, float spellPower, float random, Team instigatingTeam, Actor instigator, Server.Actors.ServerPlayerController instigatingPlayer, DamageableActor target, float damage, List<Spells.Spell> cast) {
				bool didCast = false;

				if ((spells != null) && ((random*100) <= chance)) {
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

				return didCast;
			}

			public void ClientPrecache() {
				DamageSpellClass.ClientPrecache(spells);
			}

			public static void ClientPrecache(IList<DamageSpellCastRule> rules) {
				if (rules != null) {
					for (int i = 0; i < rules.Count; ++i) {
						rules[i].ClientPrecache();
					}
				}
			}
		}

		[Serializable]
		public struct DamageProcEvent {
			public DamageSpellCastRule[] selfProcs;
			public DamageSpellCastRule[] targetProcs;

			public void Execute(int level, float spellPower, float random, float random2, Team instigatingTeam, Actor instigator, Server.Actors.ServerPlayerController instigatingPlayer, DamageableActor target, float damage) {
				if (target != null) {
					if (targetProcs != null) {
						for (int i = 0; i < targetProcs.Length; ++i) {
							var rule = targetProcs[i];
							if (target.CheckFriendlyFire(rule.friendlyFire, instigatingTeam) && (damage >= rule.minDamageToProc)) {
								rule.Execute(level, spellPower, random, instigatingTeam, instigator, instigatingPlayer, target, damage, null);
							}
						}
					}
				}
				if (selfProcs != null) {
					var instigatorDamage = instigator as DamageableActor;
					if (instigatorDamage != null) {
						for (int i = 0; i < selfProcs.Length; ++i) {
							var rule = selfProcs[i];
							if (instigatorDamage.CheckFriendlyFire(rule.friendlyFire, instigatingTeam) && (damage >= rule.minDamageToProc)) {
								rule.Execute(level, spellPower, random, instigatingTeam, instigator, instigatingPlayer, instigatorDamage, damage, null);
							}
						}
					}
				}
			}

			public void ClientPrecache() {
				DamageSpellCastRule.ClientPrecache(selfProcs);
				DamageSpellCastRule.ClientPrecache(targetProcs);
			}
		}

		[Serializable]
		public class Channel {
			public DamageMetaClass metaClass;
			public ActorDamageFilterRule[] affectedActors;
			[EditorFlags]
			public EFriendlyFire friendlyFire;
			public float damageScale;
			public bool scaleByPropertyMaxValue;
			[HideInInspector]
			public bool initialized;

			public void ClientPrecache() {
				ActorDamageFilterRule.ClientPrecache(affectedActors);
			}

			public static void ClientPrecache(IList<Channel> channels) {
				if (channels != null) {
					for (int i = 0; i < channels.Count; ++i) {
						var c = channels[i];
						if (c != null) {
							c.ClientPrecache();
						}
					}
				}
			}
		}
		[Serializable]
		public class Resistance {
			public DamageMetaClass affectedDamage;
			public ActorPropertyMetaClass parryDamageScaleBonusMetaClass;
			public ActorPropertyMetaClass parryMaxDamageBonusMetaClass;
			public float parryDamageScale;
			public float parryMaxDamageReduction;
			public XPCurve parryMaxDamageReductionScaling;
			public float GetParryMaxDamageReduction(int toLevel) {
				var scale = 1f;
				if (parryMaxDamageReductionScaling != null) {
					scale = GameManager.instance.staticData.xpTable.Eval(parryMaxDamageReductionScaling, toLevel);
				}
				return scale*parryMaxDamageReduction;
			}
			public ActorPropertyMetaClass resistChanceBonusMetaClass;
			public ActorPropertyMetaClass resistDamageScaleBonusMetaClass;
			public ActorPropertyMetaClass resistMaxDamageBonusMetaClass;
			[Range(0,100)]
			public float resistChance;
			public float resistDamageScale;
			public float resistMaxDamageReduction;
			public XPCurve resistMaxDamageReductionScaling;
			public float GetResistMaxDamageReduction(int toLevel) {
				var scale = 1f;
				if (resistMaxDamageReductionScaling != null) {
					scale = GameManager.instance.staticData.xpTable.Eval(resistMaxDamageReductionScaling, toLevel);
				}
				return scale*resistMaxDamageReduction;
			}
			public ActorPropertyMetaClass blockDamageScaleBonusMetaClass;
			public ActorPropertyMetaClass blockMaxDamageBonusMetaClass;
			public float blockDamageScale;
			public float blockMaxDamageReduction;
			public XPCurve blockMaxDamageReductionScaling;
			public float GetBlockMaxDamageReduction(int toLevel) {
				var scale = 1f;
				if (blockMaxDamageReductionScaling != null) {
					scale = GameManager.instance.staticData.xpTable.Eval(blockMaxDamageReductionScaling, toLevel);
				}
				return scale*blockMaxDamageReduction;
			}
		}

#if UNITY_EDITOR
		protected override void InitVersion() {
			base.InitVersion();
			if (version < 1) {
				spawnBloodFX = true;
			}
		}

		protected void InitChannel(Channel channel) {
			if (channel.affectedActors != null) {
				for (int i = 0; i < channel.affectedActors.Length; ++i) {
					var r = channel.affectedActors[i];
					if (!r.initialized) {
						r.basePainChance = 100f;
						r.initialized = true;
						channel.affectedActors[i] = r;
					}
				}
			}
		}
#endif
	}
}