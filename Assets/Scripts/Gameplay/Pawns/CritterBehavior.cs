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
            if (c.canJump && c.activity == Pawn.Activity.OnGround) {
                input.inputs[(int)InputType.Jump] = InputState.JustPressed;
            }

        }
    }
	public class CritterBehaviorMeleeAttack : CritterBehavior {


		public Vector3 desiredOffset;

		override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {

			var weapon = c.GetInventorySlot(0) as Weapon;
			float minRange = 2;
			float maxRange = 5;
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

					input.inputs[(int)InputType.Jump] = InputState.Pressed;
					desiredOffset = Vector3.zero;


				} else {

					if (diff.y <= -enemyElevationDeltaToJump) {
						if (c.canJump && c.activity == Pawn.Activity.OnGround) {
							input.inputs[(int)InputType.Jump] = InputState.JustPressed;
						}
					}
					diff.y = 0;
					if (diff == Vector3.zero) {
						diff.x = 1;
					}

					if (desiredOffset == Vector3.zero) {
						var angle = Random.Range(0, Mathf.PI * 2);
						desiredOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
					}

					var desiredPos = c.lastKnownPosition + desiredOffset * ((maxRange-minRange)/2+minRange);
					var move = desiredPos - c.position;
					move.y = 0;

					float dist = diff.magnitude;


					if (dist > minRange && dist < maxRange && c.canAttack && c.activity == Pawn.Activity.OnGround && move.magnitude < (maxRange-minRange)/2) {
						input.look = -diff;
						if (weapon.CanCast()) {
							if (c.CanSee(c.gameMode.players[0].playerPawn) > 0) {
								input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
								desiredOffset = Vector3.zero;
							}
						}
					} else {
						input.movement = move.normalized;
						input.look = move.normalized;
						if (c.stamina > c.maxStamina * 0.5f) {
							input.inputs[(int)InputType.Jump] = InputState.Pressed;
						}
					}
				}
			}

		}


	}
	public class CritterBehaviorRangedAttack : CritterBehavior {
		override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {

			var weapon = c.GetInventorySlot(0) as Weapon;
			float minRange = 5;
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

					var player = c.gameMode.players[0].playerPawn;
					if (c.CanSee(player) > 0 || c.CanSmell(player) > 0 || c.CanHear(player) > 0) {
						if (dist > minRange && dist < maxRange) {
							if (c.canAttack && c.activity == Pawn.Activity.OnGround) {
								input.look = -diff;
								if (weapon.CanCast()) {
									input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
								}
							}
						}
						else {
							input.movement = move.normalized;
							input.look = move.normalized;
							if (c.stamina > c.maxStamina*0.5f) {
								input.inputs[(int)InputType.Jump] = InputState.Pressed;
							}
						}
					}
				}
			}

		}


	}
}
