using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

    public enum ECritterBehaviorType {
        Flee,
        MeleeAttack,
		RangedAttack
    }

    abstract public class CritterBehavior {
        abstract public void Tick(Critter c, float dt, ref Pawn.Input_t input);

        public static CritterBehavior Create(ECritterBehaviorType t) {
            switch (t) {
                case ECritterBehaviorType.Flee:
                    return new CritterBehaviorFlee();
				case ECritterBehaviorType.MeleeAttack:
					return new CritterBehaviorMeleeAttack();
				case ECritterBehaviorType.RangedAttack:
					return new CritterBehaviorRangedAttack();
			}
			return null;
        }
    }

    public class CritterBehaviorFlee : CritterBehavior {
        override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {
            input.inputs[(int)InputType.Jump] = InputState.Released;
            input.inputs[(int)InputType.AttackRight] = InputState.Released;
            if (c.hasLastKnownPosition) {
                var diff = c.position - c.lastKnownPosition;
                diff.z = 0;
                if (diff == Vector3.zero) {
                    diff.x = 1;
                }
                input.movement = diff.normalized;
                input.look = input.movement;
            }
            if (c.canJump && c.activity == Pawn.Activity.OnGround && c.sprintTimer == 0) {
                input.inputs[(int)InputType.Jump] = InputState.JustPressed;
            }

        }
    }
	public class CritterBehaviorMeleeAttack : CritterBehavior {
		override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {

			var weapon = c.GetInventorySlot(0) as Weapon;
			float minRange = 2;
			float maxRange = 5;
			float fleeRange = 5;
			float fleeStaminaLimit = 20;
			float enemyElevationDeltaToJump = 3;

			input.movement = Vector3.zero;
			input.inputs[(int)InputType.Jump] = InputState.Released;
			input.inputs[(int)InputType.AttackRight] = InputState.Released;

			if (c.hasLastKnownPosition) {
				var diff = c.rigidBody.position - c.lastKnownPosition;

				if (c.stamina < fleeStaminaLimit) {
					var desiredPos = c.lastKnownPosition + diff.normalized * fleeRange;
					var move = desiredPos - c.position;
					move.y = 0;

					float dist = diff.magnitude;

					input.movement = move.normalized;
					input.look = -diff;

				}
				else {

					if (diff.y <= -enemyElevationDeltaToJump) {
						if (c.canJump && c.activity == Pawn.Activity.OnGround) {
							input.inputs[(int)InputType.Jump] = InputState.JustPressed;
						}
					}
					diff.y = 0;
					if (diff == Vector3.zero) {
						diff.x = 1;
					}
					var desiredPos = c.lastKnownPosition + diff.normalized * maxRange;
					var move = desiredPos - c.position;
					move.y = 0;

					float dist = diff.magnitude;


					if (c.CanSee(c.gameMode.players[0].playerPawn) > 0) {
						if (dist > minRange && dist < maxRange && c.canAttack && c.activity == Pawn.Activity.OnGround && weapon.CanCast()) {
							input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
						}
						else {
							input.movement = move.normalized;
						}
					}
					input.look = -diff;
				}
			}

		}


	}
	public class CritterBehaviorRangedAttack : CritterBehavior {
		override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {

			var weapon = c.GetInventorySlot(0) as Weapon;
			float minRange = 2;
			float maxRange = 10;
			float fleeRange = 10;
			float fleeStaminaLimit = 20;
			float enemyElevationDeltaToJump = 3;

			input.movement = Vector3.zero;
			input.inputs[(int)InputType.Jump] = InputState.Released;
			input.inputs[(int)InputType.AttackRight] = InputState.Released;

			if (c.hasLastKnownPosition) {
				var diff = c.rigidBody.position - c.lastKnownPosition;

				if (c.stamina < fleeStaminaLimit) {
					var desiredPos = c.lastKnownPosition + diff.normalized * fleeRange;
					var move = desiredPos - c.position;
					move.y = 0;

					float dist = diff.magnitude;

					input.movement = move.normalized;
					input.look = -diff;

				}
				else {

					if (diff.y <= -enemyElevationDeltaToJump) {
						if (c.canJump && c.activity == Pawn.Activity.OnGround) {
							input.inputs[(int)InputType.Jump] = InputState.JustPressed;
						}
					}
					diff.y = 0;
					if (diff == Vector3.zero) {
						diff.x = 1;
					}
					var desiredPos = c.lastKnownPosition + diff.normalized * maxRange;
					var move = desiredPos - c.position;
					move.y = 0;

					float dist = diff.magnitude;


					if (c.CanSee(c.gameMode.players[0].playerPawn) > 0) {
						if (dist > minRange && dist < maxRange && c.canAttack && c.activity == Pawn.Activity.OnGround && weapon.CanCast()) {
							input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
						}
						else {
							input.movement = move.normalized;
						}
					}
					input.look = -diff;
				}
			}

		}


	}
}
