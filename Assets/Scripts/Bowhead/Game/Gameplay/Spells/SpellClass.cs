// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Bowhead.Actors.Spells {

	public enum EStackingPenalty {
		None,
		Linear
	}

	public enum EStackingBehavior {
		Discard,
		Replace,
		Stack
	}

	public abstract class SpellClass : StaticVersionedAssetWithSerializationCallback {
		const int VERSION = 1;

		public enum EWaterInteraction {
			None,
			CastRequireWater,
			CastRequireDeepWater,
			ExpireRequireWater,
			CleanseRequireWater,
			ExpireRequireDeepWater,
			CleanseRequireDeepWater,
			ExpireInWater,
			CleanseInWater,
			ExpireInDeepWater,
			CleanseInDeepWater
		}

		public SpellMetaClass metaClass;
		public string type;
		public string customNameKey;
		public string customDescKey;
		public float descParam1;
		public Sprite_WRef icon;
		public bool hidden;
		public bool cannotBeCleansed;
		public bool cannotBeMutexed;
		public bool cannotBeMuted;
		public bool cannotBeSuspended;
		public bool dispelOnTargetDeath;
		public bool cleanseOnOrders;
		public bool expireAfterOneTick;
		public bool doesNotScale;
		public EWaterInteraction waterInteraction;
		public EStackingBehavior stackingBehavior;
		public int stackLimit;
		public int cleanseLimit;
		[MinMaxSlider(0, 300)]
		public Vector2 duration;
		public EStackingPenalty durationStackingPenalty;
		public AreaOfEffectClass attachedAOE;
		public AreaOfEffectClass dropAOE;
		[Range(0, 100)]
		public float dropAOEChance;
		[SerializeField]
		SpellMetaClass[] _mutexedClasses;
		[SerializeField]
		SpellMetaClass[] _mutedClasses;
		[SerializeField]
		SpellMetaClass[] _mutingClasses;
		[SerializeField]
		SpellMetaClass[] _suspendedClasses;
		[SerializeField]
		SpellMetaClass[] _suspendingClasses;
		[SerializeField]
		SpellMetaClass[] _cleansedClasses;
		[SerializeField]
		SpellMetaClass[] _cleansingClasses;
		[SerializeField]
		SpellMetaClass[] _prereqAllClasses;
		[SerializeField]
		SpellMetaClass[] _prereqAnyClasses;
		[SerializeField]
		SpellEffectClass[] _effectClasses;

		public SpellCastRule[] procOnCleanse;
		public SpellCastRule[] procOnExpiry;
		public SpellMetaClass[] cleanseOnCleanse;
		public SpellMetaClass[] cleanseOnExpiry;
		public SpellMetaClass[] expireOnCleanse;
		public SpellMetaClass[] expireOnExpiry;

		[HideInInspector]
		[SerializeField]
		protected string spellClassString;

		bool _precached;
		ConstructorInfo _constructor;
		static readonly Type[] _constructorArgs = new[] { typeof(int), typeof(float), typeof(Server.ServerWorld), typeof(SpellClass), typeof(Team), typeof(Actor), typeof(Server.Actors.ServerPlayerController), typeof(DamageableActor) };

		public SpellMetaClass[] mutexedClasses {
			get {
				return _mutexedClasses;
			}
		}

		public SpellMetaClass[] mutedClasses {
			get {
				return _mutedClasses;
			}
		}

		public SpellMetaClass[] mutingClasses {
			get {
				return _mutingClasses;
			}
		}

		public SpellMetaClass[] suspendedClasses {
			get {
				return _suspendedClasses;
			}
		}

		public SpellMetaClass[] suspendingClasses {
			get {
				return _suspendingClasses;
			}
		}

		public SpellMetaClass[] cleansedClasses {
			get {
				return _cleansedClasses;
			}
		}

		public SpellMetaClass[] cleansingClasses {
			get {
				return _cleansingClasses;
			}
		}

		public SpellMetaClass[] prereqAllClasses {
			get {
				return _prereqAllClasses;
			}
		}

		public SpellMetaClass[] prereqAnyClasses {
			get {
				return _prereqAnyClasses;
			}
		}

		public SpellEffectClass[] effectClasses {
			get {
				return _effectClasses;
			}
		}

		public bool canBeCleansed {
			get {
				return !cannotBeCleansed;
			}
		}

		public bool canBeMuted {
			get {
				return !cannotBeMuted;
			}
		}

		public bool canBeMutexed {
			get {
				return !cannotBeMutexed;
			}
		}

		public bool canBeSuspended {
			get {
				return !cannotBeSuspended;
			}
		}

		public Type instanceType {
			get;
			private set;
		}

		public T New<T>(int level, float spellPower, Server.ServerWorld world, Team team, Actor instigator, Server.Actors.ServerPlayerController player, DamageableActor target) where T : Spell {
			if ((instanceType != null) && (_constructor != null)) {
				return (T)_constructor.Invoke(new object[] { level, spellPower, world, this, team, instigator, player, target });
			}
			throw new System.Exception("Missing Spell class " + spellClassString);
		}

		public virtual bool CheckPreReqs(Team team, DamageableActor target) {
			if (waterInteraction == EWaterInteraction.None) {
				return true;
			}

			if (target.inWater) {
				if ((waterInteraction == EWaterInteraction.CastRequireWater) || (waterInteraction == EWaterInteraction.ExpireRequireWater) || (waterInteraction == EWaterInteraction.CleanseRequireWater)) {
					return true;
				}
				if ((waterInteraction == EWaterInteraction.CastRequireDeepWater) || (waterInteraction == EWaterInteraction.ExpireRequireDeepWater) || (waterInteraction == EWaterInteraction.CleanseRequireDeepWater)) {
					//var unit = target as Unit;
					//if ((unit != null) && unit.isUnderwater) {
					//	return true;
					//}
				}
				if ((waterInteraction == EWaterInteraction.ExpireInWater) ||
					(waterInteraction == EWaterInteraction.CleanseInWater)) {
					return false;
				}
				if ((waterInteraction == EWaterInteraction.ExpireInDeepWater) ||
					(waterInteraction == EWaterInteraction.CleanseInDeepWater)) {
					//var unit = target as Unit;
					//if ((unit != null) && unit.isUnderwater) {
					//	return false;
					//}
				}
				return true;
			}

			return !((waterInteraction == EWaterInteraction.CastRequireWater) ||
				  (waterInteraction == EWaterInteraction.CastRequireDeepWater) ||
				  (waterInteraction == EWaterInteraction.ExpireRequireWater) ||
				  (waterInteraction == EWaterInteraction.CleanseRequireWater) ||
				  (waterInteraction == EWaterInteraction.ExpireRequireDeepWater) ||
				  (waterInteraction == EWaterInteraction.CleanseRequireDeepWater));
		}

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();
			
			if (string.IsNullOrEmpty(spellClassString)) {
				instanceType = null;
			} else {
				instanceType = Type.GetType(spellClassString);
			}

			if (instanceType != null) {
				_constructor = instanceType.GetConstructor(_constructorArgs);
			} else {
				_constructor = null;
			}
		}

		public override void ClientPrecache() {
			if (!_precached) {
				_precached = true;
				base.ClientPrecache();
				WeakAssetRef.Precache(icon);

				if (attachedAOE != null) {
					attachedAOE.ClientPrecache();
				}

				if (dropAOE != null) {
					dropAOE.ClientPrecache();
				}

				SpellEffectClass.Precache(_effectClasses);
				SpellCastRule.ClientPrecache(procOnCleanse);
				SpellCastRule.ClientPrecache(procOnExpiry);
			}
		}

		public static void ClientPrecache(IList<SpellClass> spells) {
			if (spells != null) {
				for (int i = 0; i < spells.Count; ++i) {
					var s = spells[i];
					if (s != null) {
						s.ClientPrecache();
					}
				}
			}
		}

		public string localizedName {
			get {
				var key = string.IsNullOrEmpty(customNameKey) ? ("UI.HUD.Spell.Name." + name) : customNameKey;
				return Utils.GetLocalizedText(key);
			}
		}

		public string FormatLocalizedDescription(float spellPower) {
			var key = string.IsNullOrEmpty(customDescKey) ? ("UI.HUD.Spell.Description." + name) : customDescKey;
			return Utils.GetLocalizedText(key, doesNotScale ? descParam1 : Mathf.FloorToInt(spellPower*descParam1));
		}

		public string localizedType {
			get {
				return Utils.GetLocalizedText("UI.HUD.Spell.Type." + type);
			}
		}

#if UNITY_EDITOR
		protected sealed override void InitVersion() {
			OnInitVersion();
			version = VERSION;
		}

		protected virtual void OnInitVersion() {
			if (version < 1) {
				duration.x = 1f;
				duration.y = 1f;
			}
		}
#endif
	}

}

