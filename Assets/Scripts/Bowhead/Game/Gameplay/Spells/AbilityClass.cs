// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;
using Bowhead.Server.Actors;

namespace Bowhead.Actors.Spells {
	
	[Serializable]
	public struct SelectionFilter {
		public ActorSpellFilterRule[] actorTypes;
		public int requiredSelectedCount;
		public bool canSelectMore;

		public bool Check(PlayerController player, DamageableActor actor) {
			if (actor.pendingKill || actor.dead) {
				return false;
			}

			for (int i = 0; i < actorTypes.Length; ++i) {
				int depthUnused;
				if (actorTypes[i].Check(player.team, actor, out depthUnused)) {
					return true;
				}
			}
			return actorTypes.Length == 0;
		}
	}

	public enum EPassiveAbilityMode {
		PassivesAlwaysOn,
		PassivesCleansedByActiveSpells,
		PassivesCleansedAndRecastAfterActiveSpells
	}

	[Serializable]
	public struct AbilitySounds {
		public SoundCue cast;
		public SoundCue notReady;
		public SoundCue placingTotem;
		public SoundCue invalidTotemPlacement;
		public SoundCue placedTotem;

		public void Precache() {
			SoundCue.Precache(cast);
			SoundCue.Precache(notReady);
			SoundCue.Precache(placingTotem);
			SoundCue.Precache(invalidTotemPlacement);
			SoundCue.Precache(placedTotem);
		}
	}

	[Serializable]
	public struct ResurrectionProperty {
		public ActorPropertyMetaClass propertyClass;
		[Range(0, 1)]
		public float value;
	}

	[Serializable]
	public struct ResurrectionRule {
		public ActorSpellFilterRule affectedActors;
		public ResurrectionProperty[] properties;
		public SpellClass[] spells;

		public bool Execute(float spellPower, Team instigatingTeam, Actor instigator, ServerPlayerController player, DamageableActor target, List<Spell> cast) {
			bool didCast = false;

			if (spells != null) {
				for (int i = 0; i < spells.Length; ++i) {
					var s = spells[i];
					if (s != null) {
						var x = target.ServerApplySpell(target.level, spellPower, s, instigatingTeam, instigator, player);
						if ((x != null) && (cast != null)) {
							didCast = true;
							cast.Add(x);
						}
					}
				}
			}

			if (properties != null) {
				for (int i = 0; i < properties.Length; ++i) {
					var rp = properties[i];
					if (rp.propertyClass != null) {
						var p = target.GetMutableProperty(rp.propertyClass);
						if (p != null) {
							p.value = rp.value * p.max;
						}
					}
				}
			}

			return didCast;
		}

		public static bool GetBestRule(IList<ResurrectionRule> rules, Team instigatingTeam, DamageableActor target, out ResurrectionRule selected) {
			int bestDepth = int.MinValue;
			selected = new ResurrectionRule();

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
	}

	public class AbilityClass : StaticVersionedAsset {
		public const float GLOBAL_COOLDOWN = 1f;
		public string type;
		public string customNameKey;
		public string customDescKey;
		public float descParam1;
		public float descParam2;
		public Sprite_WRef icon;
		public Sprite_WRef icon2;
		public float cooldown;
		public float cooldownWhenAOEPickedUp;
		public int maxUseCount;
		public float activeSoulStoneCost;
		public int passiveSoulStoneCost;
		public AreaOfEffectClass aoeClass;
		public bool canPickupAOE;
		public bool isResurrect;
		public bool dontScaleDescriptionParams;
		public EPassiveAbilityMode passivity;
		public SelectionFilter[] selectionTypes;
		public SpellMetaClass[] selectionMutexingSpells;
		public SpellMetaClass[] globalMutexingSpells;
		public SpellCastRule[] activeSpells;
		public SpellCastRule[] passiveSpells;
		public ResurrectionRule[] resurrectionRules;
		public ActorMetaClass[] aoeFOWSightRestriction;
		public float globalCooldownReduction;
		public float globalEssenceBonus;
		
		public bool ValidateSelection<T>(PlayerController player, IList<T> selection) where T : DamageableActor {

			int numNotControlledByPlayer = 0;

			var isServer = player is ServerPlayerController;

			for (int i = 0; i < selection.Count; ++i) {
				var u = selection[i];
				if (!(u.pendingKill || u.dead) && u.HasAnySpells(selectionMutexingSpells)) {
					return false;
				}
				if (isServer && (u.serverOwningPlayer != player)) {
					++numNotControlledByPlayer;
				}
			}

			if (numNotControlledByPlayer > 1) {
				// someone hacked their client:
				// can't cast spells on more than 1 unit at a time
				return false;
			}

			if (selectionTypes.Length < 1) {
				// no selection required
				return true;
			}

			for (int i = 0; i < selectionTypes.Length; ++i) {
				var filter = selectionTypes[i];

				if (!filter.canSelectMore) {
					if (selection.Count > filter.requiredSelectedCount) {
						continue;
					}
				}

				int count = 0;
				for (int k = 0; k < selection.Count; ++k) {
					var u = selection[k];
					if (filter.Check(player, u)) {
						++count;
						if (count == filter.requiredSelectedCount) {
							return true;
						}
					}
				}
			}

			return false;
		}

		public bool ValidateAllUnits<T>(IList<T> allUnits) where T : DamageableActor {
			for (int i = 0; i < allUnits.Count; ++i) {
				var u = allUnits[i];
				if (u.HasAnySpells(globalMutexingSpells)) {
					return false;
				}
			}
			return true;
		}

		public override void ClientPrecache() {
			base.ClientPrecache();
			WeakAssetRef.Precache(icon);
			if (aoeClass != null) {
				aoeClass.ClientPrecache();
			}
			SpellCastRule.ClientPrecache(activeSpells);
			SpellCastRule.ClientPrecache(passiveSpells);
		}

		public static void ClientPrecache(IList<AbilityClass> classes) {
			if (classes != null) {
				for (int i = 0; i < classes.Count; ++i) {
					var c = classes[i];
					if (c != null) {
						c.ClientPrecache();
					}
				}
			}
		}

		public bool CheckSightRestrictions(ActorMetaClass metaClass) {
			if ((aoeFOWSightRestriction != null) && (aoeFOWSightRestriction.Length > 0)) {
				for (int k = 0; k < aoeFOWSightRestriction.Length; ++k) {
					var test = aoeFOWSightRestriction[k];
					if (metaClass.IsA(test)) {
						return true;
					}
				}
				return false;
			}
			return true;
		}

		public string nameKey {
			get {
				return string.IsNullOrEmpty(customNameKey) ? ("UI.HUD.Ability.Name." + name) : customNameKey;
			}
		}

		public string descriptionKey {
			get {
				return string.IsNullOrEmpty(customDescKey) ? ("UI.HUD.Ability.Description." + name) : customDescKey;
			}
		}

		public string localizedName {
			get {
				return Utils.GetLocalizedText(nameKey);
			}
		}

		public string FormatLocalizedDescription(float spellPower) {
			return Utils.GetLocalizedText(descriptionKey, dontScaleDescriptionParams ? descParam1 : Mathf.FloorToInt(spellPower*descParam1), dontScaleDescriptionParams ? descParam2 : Mathf.FloorToInt(spellPower*descParam2));
		}

		public string localizedType {
			get {
				return Utils.GetLocalizedText("UI.HUD.Ability.Type." + type);
			}
		}
	
		public bool hasActiveSpells {
			get {
				return isResurrect || (aoeClass != null) || ((activeSpells != null) && (activeSpells.Length > 0));
			}
		}
	}
}