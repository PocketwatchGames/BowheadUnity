﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

    public enum ECritterBehaviorType {
		Idle,
		Patrol,
        Flee,
        MeleeAttack,
		RangedAttack
    }

    abstract public class CritterBehavior {
		public CritterBehavior(Critter c) {	}

        abstract public void Tick(Critter c, float dt, ref Pawn.Input_t input);

        public static CritterBehavior Create(ECritterBehaviorType t, Critter c) {
            switch (t) {
				case ECritterBehaviorType.Idle:
					return new CritterBehaviorIdle(c);
				case ECritterBehaviorType.Patrol:
					return new CritterBehaviorPatrol(c);
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

	public class CritterBehaviorIdle : CritterBehavior {
		public CritterBehaviorIdle(Critter c) : base(c) { }
		public override void Tick(Critter c, float dt, ref Pawn.Input_t input) {
			input.movement = Vector3.zero;
			if (c.hasLastKnownPosition) {
				var diff = c.lastKnownPosition - c.position;
				input.look = diff.normalized;
			}
		}
	}

	public class CritterBehaviorPatrol : CritterBehavior {

		public float destinationTolerance = 0.5f;
		public float patrolRange = 5.0f;
		public float patrolTimeMin = 4.0f;
		public float patrolTimeMax = 8.0f;
		public float patrolSpeed = 0.25f;

		Vector3 patrolPos;
		float patrolTimer;

		public CritterBehaviorPatrol(Critter c) : base(c) { }
		public override void Tick(Critter c, float dt, ref Pawn.Input_t input) {
			if (c.hasLastKnownPosition && c.wary > 0) {
				var diff = c.lastKnownPosition - c.position;
				input.look = diff.normalized;
				input.movement = Vector3.zero;
			} else {
				patrolTimer -= dt;
				if (patrolTimer <= 0) {
					patrolTimer = Random.Range(patrolTimeMin,patrolTimeMax);
					GetNewPatrolPoint(c);
				}

				var diff = patrolPos - c.position;
				diff.y = 0;
				if (diff.magnitude > destinationTolerance) {
					input.movement = diff.normalized* patrolSpeed;
					input.look = input.movement;
				}

			}
		}

		private void GetNewPatrolPoint(Critter c) {
			var angle = Random.Range(0, Mathf.PI * 2);
			var desiredOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
			patrolPos = c.position + desiredOffset * patrolRange;
		}
	}

	public class CritterBehaviorFlee : CritterBehavior {

		public float destinationTolerance = 0.5f;

		public CritterBehaviorFlee(Critter c) : base(c) { }

        override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {
            input.inputs[(int)InputType.Jump] = InputState.Released;
            input.inputs[(int)InputType.AttackRight] = InputState.Released;
            if (c.hasLastKnownPosition) {
                var diff = c.position - c.lastKnownPosition;
                diff.y = 0;
				if (diff.magnitude > destinationTolerance) {
					input.movement = diff.normalized;
					input.look = input.movement;
				}
			}
            if (c.canJump && c.activity == Pawn.Activity.OnGround) {
                input.inputs[(int)InputType.Jump] = InputState.JustPressed;
            }

        }
    }
	public class CritterBehaviorMeleeAttack : CritterBehavior {

		float minRange;
		float maxRange;
		float destinationTolerance;
		float enemyElevationDeltaToJump;
		float fleeRange;
		float fleeStunLimit;
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
			destinationTolerance = 1.5f;

			enemyElevationDeltaToJump = 3;
			fleeRange = 10;
			fleeStunLimit = 0.5f;
		}



		override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {


			// choose sub behavior
			if (!c.hasLastKnownPosition) {
				curSubBehavior = SubBehavior.Idle;
			} else {
				if (c.stunAmount > c.data.maxStun * fleeStunLimit) {
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


			if (dist > minRange && dist < maxRange && c.canAttack && c.activity == Pawn.Activity.OnGround && move.magnitude < destinationTolerance) {
				input.look = -diff;
				var weapon = c.GetInventorySlot(0) as Weapon;
				if (weapon.CanCast()) {
					if (c.CanSee(c.gameMode.players[0].playerPawn) > 0) {
						input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
						desiredOffset = Vector3.zero;
					}
				}
			} else {
				float speed = dist > 4 ? 1.0f : 0.5f;
				input.movement = move.normalized * speed;
				input.look = -diff;
				if (diff.y <= -enemyElevationDeltaToJump) {
					if (c.canJump && c.activity == Pawn.Activity.OnGround) {
						input.inputs[(int)InputType.Jump] = InputState.JustPressed;
					}
				}
			}
		}

	}

	public class CritterBehaviorRangedAttack : CritterBehavior {

		float minRange;
		float maxRange;
		float enemyElevationDeltaToJump;
		Vector3 desiredOffset;
		float fleeRange;
		float fleeStunLimit;

		public CritterBehaviorRangedAttack(Critter c) : base(c) {
			minRange = 5;
			maxRange = 10;
			enemyElevationDeltaToJump = 3;
			fleeRange = 12;
			fleeStunLimit = 0.5f;
		}

		override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {

			input.movement = Vector3.zero;
			input.inputs[(int)InputType.Jump] = InputState.Released;
			input.inputs[(int)InputType.AttackRight] = InputState.Released;

			if (c.hasLastKnownPosition) {
				var diff = c.rigidBody.position - c.lastKnownPosition;

				if (c.stunAmount > c.data.maxStun * 0.5f) {
					var desiredPos = c.lastKnownPosition + diff.normalized * fleeRange;
					var move = desiredPos - c.position;
					move.y = 0;

					float dist = diff.magnitude;

					input.movement = move.normalized;
					input.look = -diff;

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
					var desiredPos = c.lastKnownPosition + diff.normalized * maxRange;
					var move = desiredPos - c.position;
					move.y = 0;

					float dist = diff.magnitude;

					var player = c.gameMode.players[0].playerPawn;
					if (c.CanSee(player) > 0) {
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
							input.look = input.movement;
						}
					} else {
						if (move.magnitude > 0.5f) {
							input.movement = move.normalized * 0.5f;
							input.look = input.movement;
						}
					}
				}
			}

		}


	}
}
