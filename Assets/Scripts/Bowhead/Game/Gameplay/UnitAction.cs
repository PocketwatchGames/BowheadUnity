// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead.Actors {
	public class Unit { };

	public abstract class UnitActionClassBase : VersionedObjectWithSerializationCallback {
		public UnitActionMetaClass metaClass;
		public float recoveryTime;
		public int interruptLevel;
		public bool validWhenMoving;
		public bool stopMovingWhileExecuting;
		public bool enabled;
		public Spells.ProcEvent procOnUse;
		public Spells.AreaOfEffectClass dropOnUse;
		[Range(0, 100)]
		public float dropOnUseChance;
		[HideInInspector]
		[SerializeField]
		protected string _actionClass;
		
		private Type actionClass {
			get;
			set;
		}

		public T NewAction<T>(Unit unit) where T : UnitActionBase {
			if (actionClass == null) {
				return null;
			}
			try {
				return (T)Activator.CreateInstance(actionClass, unit, this);
			} catch (Exception e) {
				Debug.LogException(e);
			}
			return null;
		}

		public override void OnAfterDeserialize() {
			if (string.IsNullOrEmpty(_actionClass)) {
				actionClass = null;
			} else {
				actionClass = Type.GetType(_actionClass);
			}
		}

		public override void ClientPrecache() {
			base.ClientPrecache();
			if (dropOnUse != null) {
				dropOnUse.ClientPrecache();
			}
			procOnUse.ClientPrecache();
		}

		public static void ClientPrecache(IList<UnitActionClassBase> actions) {
			if (actions != null) {
				for (int i = 0; i < actions.Count; ++i) {
					var a = actions[i];
					if (a != null) {
						a.ClientPrecache();
					}
				}
			}
		}
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

	public abstract class UnitActionClass : UnitActionClassBase {
		public float fov;
		public int priority;
		[MinMaxSlider(0, 500)]
		public Vector2 attackRange;
		[MinMaxSlider(0, 500)]
		public Vector2 selectRange;
		public Spells.ProcEvent procOnInterrupted;
				
		public Vector2 sqAttackRange {
			get;
			private set;
		}

		public Vector2 sqSelectRange {
			get;
			private set;
		}

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();
			sqAttackRange = new Vector2(attackRange.x*attackRange.x, attackRange.y*attackRange.y);
			sqSelectRange = new Vector2(selectRange.x*selectRange.x, selectRange.y*selectRange.y);
		}

		public override void ClientPrecache() {
			base.ClientPrecache();
			procOnInterrupted.ClientPrecache();
		}

#if UNITY_EDITOR
		protected override void InitVersion() {
			base.InitVersion();
			if (version < 1) {
				enabled = true;
				validWhenMoving = true;
				stopMovingWhileExecuting = true;
				recoveryTime = 1.5f;
				fov = 90;
			}
		}
#endif
	}

	public abstract class UnitActionBase {

		public UnitActionBase(Unit unit, UnitActionClassBase actionClass) {
			this.unit = unit;
			this.actionClass = actionClass;
			enabled = actionClass.enabled;
			recoveryTime = actionClass.recoveryTime;
		}

		public virtual void ProcOnUse(DamageableActor target) {
			//var gm = GameManager.instance;
			//var spellPower = unit.spellPower;

			//actionClass.procOnUse.Execute(unit.level, spellPower, gm.randomNumber, gm.randomNumber, unit.team, unit, unit.serverOwningPlayer, target);

			//if (actionClass.dropOnUse != null) {
			//	if (actionClass.dropOnUseChance > 0) {
			//		var random = GameManager.instance.randomNumber;
			//		if ((random*100) <= actionClass.dropOnUseChance) {
			//			var aoe = actionClass.dropOnUse.Spawn<Spells.AreaOfEffectActor>(unit.level, spellPower, (Server.ServerWorld)unit.world, unit.serverOwningPlayer, unit, null, unit.team);
			//			aoe.ServerPlace(unit.go.transform.position, unit.go.transform.rotation.eulerAngles.y);
			//		}
			//	}
			//}
		}

		public abstract void Interrupt(DamageableActor target, EUnitActionCueSlotExplosion pain);

		public virtual void ResetTransient() {
			enabled = actionClass.enabled;
			recoveryTime = actionClass.recoveryTime;
		}
		
		public Unit unit {
			get;
			private set;
		}

		public bool enabled {
			get;
			set;
		}

		public float recoveryTime {
			get;
			set;
		}

		public UnitActionClassBase actionClass {
			get;
			private set;
		}
	}

	public abstract class UnitAction : UnitActionBase {

		public UnitAction(Unit unit, UnitActionClassBase actionClass) : base(unit, actionClass) {
			this.actionClass = (UnitActionClass)actionClass;
		}

		public abstract void SwitchTo(UnitAction from);
		
		public new UnitActionClass actionClass {
			get;
			private set;
		}
	}
}