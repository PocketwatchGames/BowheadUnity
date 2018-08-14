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
		public CritterBehavior(Critter c) {	}

        abstract public void Tick(Critter c, float dt, ref Pawn.Input_t input);

        public static CritterBehavior Create(ECritterBehaviorType t, Critter c) {
            switch (t) {
                case ECritterBehaviorType.Flee:
                    return new CritterBehaviorFlee(c);
				case ECritterBehaviorType.MeleeAttack:
					return new CritterBehaviorMeleeAttack(c);
				case ECritterBehaviorType.RangedAttack:
					return new CritterBehaviorRangedAttack(c);
			}
			return null;
        }
    }

    public class CritterBehaviorFlee : CritterBehavior {
		public CritterBehaviorFlee(Critter c) : base(c) { }

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

		float minRange;
		float maxRange;
		float fleeStaminaLimit;
		float enemyElevationDeltaToJump;
		float fleeRange;
		float sprintStaminaLimit;
		Vector3 desiredOffset;
		SubBehavior curSubBehavior;


		private enum SubBehavior {
			Idle,
			MeleeAttack,
			FleeAndRecover,
		}


		public CritterBehaviorMeleeAttack(Critter c) : base(c) {
			minRange = 2;
			maxRange = 5;
			fleeStaminaLimit = c.maxStamina * 0.2f;
			enemyElevationDeltaToJump = 3;
			fleeRange = 10;
			sprintStaminaLimit = c.maxStamina * 0.5f;
		}



		override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {


			// choose sub behavior
			if (!c.hasLastKnownPosition) {
				curSubBehavior = SubBehavior.Idle;
			} else {
				if (c.stamina < fleeStaminaLimit) {
					curSubBehavior = SubBehavior.FleeAndRecover;
				} else {
					curSubBehavior = SubBehavior.MeleeAttack;
				}
			}

			// execute sub Behavior
			switch (curSubBehavior) {
				case SubBehavior.MeleeAttack:
					MeleeAttack(c, dt, ref input);
					break;
				case SubBehavior.FleeAndRecover:
					Flee(c, dt, ref input);
					break;
				case SubBehavior.Idle:
				default:
					Idle(ref input);
					break;
			}

		}

		private void Idle(ref Pawn.Input_t input) {
			input.movement = Vector3.zero;
			input.inputs[(int)InputType.Jump] = InputState.Released;
			input.inputs[(int)InputType.AttackRight] = InputState.Released;
		}

		private void Flee(Critter c, float dt, ref Pawn.Input_t input) {
			var diff = c.rigidBody.position - c.lastKnownPosition;

			var desiredPos = c.lastKnownPosition + diff.normalized * fleeRange;
			var move = desiredPos - c.position;
			move.y = 0;

			float dist = diff.magnitude;

			input.movement = move.normalized;
			input.look = -diff;

			desiredOffset = Vector3.zero;

			if (diff.y <= -enemyElevationDeltaToJump) {
				if (c.canJump && c.activity == Pawn.Activity.OnGround) {
					input.inputs[(int)InputType.Jump] = InputState.JustPressed;
				}
			}

		}

		private void MeleeAttack(Critter c, float dt, ref Pawn.Input_t input) {
			var diff = c.rigidBody.position - c.lastKnownPosition;
			diff.y = 0;
			if (diff == Vector3.zero) {
				diff.x = 1;
			}

			if (desiredOffset == Vector3.zero) {
				var angle = Random.Range(0, Mathf.PI * 2);
				desiredOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
			}

			var desiredPos = c.lastKnownPosition + desiredOffset * ((maxRange - minRange) / 2 + minRange);
			var move = desiredPos - c.position;
			move.y = 0;

			float dist = diff.magnitude;


			if (dist > minRange && dist < maxRange && c.canAttack && c.activity == Pawn.Activity.OnGround && move.magnitude < (maxRange - minRange) / 2) {
				input.look = -diff;
				var weapon = c.GetInventorySlot(0) as Weapon;
				if (weapon.CanCast()) {
					if (c.CanSee(c.gameMode.players[0].playerPawn) > 0) {
						input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
						desiredOffset = Vector3.zero;
					}
				}
			} else {
				input.movement = move.normalized;
				input.look = move.normalized;
				if (diff.y <= -enemyElevationDeltaToJump) {
					if (c.canJump && c.activity == Pawn.Activity.OnGround) {
						input.inputs[(int)InputType.Jump] = InputState.JustPressed;
					}
				} else if (c.stamina > sprintStaminaLimit) {
					input.inputs[(int)InputType.Jump] = InputState.Pressed;
				}
			}
		}

	}

	public class CritterBehaviorRangedAttack : CritterBehavior {

		float minRange;
		float maxRange;
		float fleeStaminaLimit;
		float enemyElevationDeltaToJump;
		float fleeRange;
		float sprintStaminaLimit;
		Vector3 desiredOffset;


		public CritterBehaviorRangedAttack(Critter c) : base(c) {
			minRange = 5;
			maxRange = 10;
			fleeRange = 10;
			fleeStaminaLimit = c.maxStamina * 0.2f;
			sprintStaminaLimit = c.maxStamina * 0.5f;
			enemyElevationDeltaToJump = 3;
		}

		override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {

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
								var weapon = c.GetInventorySlot(0) as Weapon;
								if (weapon.CanCast()) {
									input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
								}
							}
						}
						else {
							input.movement = move.normalized;
							input.look = move.normalized;
							if (c.stamina > sprintStaminaLimit) {
								input.inputs[(int)InputType.Jump] = InputState.Pressed;
							}
						}
					}
				}
			}

		}


	}
}
