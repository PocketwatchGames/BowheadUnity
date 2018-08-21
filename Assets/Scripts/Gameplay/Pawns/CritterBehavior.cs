using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	public partial class Critter : Pawn<Critter, CritterData> {
		public enum ECritterBehaviorType {
		Idle,
		Patrol,
		Flee,
		FleeAndRecover,
		Investigate,
		MeleeAttack,
		RangedAttack
    }

		abstract public class CritterBehavior {

			protected static EvaluationScore fail = new EvaluationScore() { score = 0 };
			public struct EvaluationScore {
				public EvaluationScore(CritterBehavior b, float score=0) { this.behavior = b; this.score = score; }

				public float score;
				public CritterBehavior behavior;
			}

			protected Critter _critter;
			public CritterBehavior(Critter c) { _critter = c; }

			abstract public void Tick(float dt, ref Pawn.Input_t input);
			abstract public EvaluationScore Evaluate();

			public static CritterBehavior Create(ECritterBehaviorType t, Critter c) {
				switch (t) {
					case ECritterBehaviorType.Idle:
						return new CritterBehaviorIdle(c);
					case ECritterBehaviorType.Patrol:
						return new CritterBehaviorPatrol(c);
					case ECritterBehaviorType.Flee:
						return new CritterBehaviorFlee(c);
					case ECritterBehaviorType.Investigate:
						return new CritterBehaviorInvestigate(c);
					case ECritterBehaviorType.FleeAndRecover:
						return new CritterBehaviorFleeAndRecover(c);
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

			public override EvaluationScore Evaluate() {
				return new EvaluationScore() { score = _critter.IsPanicked() ? 0 : 1 };
			}

			public override void Tick(float dt, ref Pawn.Input_t input) {
				input.movement = Vector3.zero;
				if (_critter.hasLastKnownPosition) {
					var diff = _critter.lastKnownPosition - _critter.position;
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

			public override EvaluationScore Evaluate() {
				if (_critter.IsPanicked()) {
					return fail;
				}
				return new EvaluationScore(this, 1.0f);
			}

			public override void Tick(float dt, ref Pawn.Input_t input) {
				if (_critter.hasLastKnownPosition && _critter.wary > 0) {
					var diff = _critter.lastKnownPosition - _critter.position;
					input.look = diff.normalized;
					input.movement = Vector3.zero;
				} else {
					patrolTimer -= dt;
					if (patrolTimer <= 0) {
						patrolTimer = Random.Range(patrolTimeMin, patrolTimeMax);
						GetNewPatrolPoint(_critter);
					}

					var diff = patrolPos - _critter.position;
					diff.y = 0;
					if (diff.magnitude > destinationTolerance) {
						input.movement = diff.normalized * patrolSpeed;
						input.look = input.movement;
					}

				}
			}

			private void GetNewPatrolPoint(Critter c) {
				var angle = Random.Range(0, Mathf.PI * 2);
				var desiredOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
				patrolPos = _critter.position + desiredOffset * patrolRange;
			}
		}

		public class CritterBehaviorFlee : CritterBehavior {

			public float destinationTolerance = 0.5f;

			public CritterBehaviorFlee(Critter c) : base(c) { }

			public override EvaluationScore Evaluate() {
				if (_critter.IsPanicked()) {
					return fail;
				}
				return new EvaluationScore(this, 1.0f);
			}

			override public void Tick(float dt, ref Pawn.Input_t input) {
				input.inputs[(int)InputType.Jump] = InputState.Released;
				input.inputs[(int)InputType.AttackRight] = InputState.Released;
				if (_critter.hasLastKnownPosition) {
					var diff = _critter.position - _critter.lastKnownPosition;
					diff.y = 0;
					if (diff.magnitude > destinationTolerance) {
						input.movement = diff.normalized;
						input.look = input.movement;
					}
				}
				if (_critter.canJump && _critter.activity == Pawn.Activity.OnGround) {
					input.inputs[(int)InputType.Jump] = InputState.JustPressed;
				}

			}
		}

		public class CritterBehaviorInvestigate : CritterBehavior {
			public CritterBehaviorInvestigate(Critter c) : base(c) { }

			public override EvaluationScore Evaluate() {
				if (!_critter.IsPanicked() || _critter.hasLastKnownPosition) {
					return fail;
				}
				return new EvaluationScore(this, 1.0f);
			}

			public override void Tick(float dt, ref Input_t input) {
				input.movement = Vector3.zero;
				input.inputs[(int)InputType.Jump] = InputState.Released;
				input.inputs[(int)InputType.AttackRight] = InputState.Released;
			}
		}
		public class CritterBehaviorFleeAndRecover : CritterBehavior {
			float fleeStunLimit = 0.5f;
			float fleeRange = 10;

			public CritterBehaviorFleeAndRecover(Critter c) : base(c) { }

			public override EvaluationScore Evaluate() {

				if (!_critter.IsPanicked()) {
					return fail;
				}

				if (_critter.stunAmount > _critter.data.maxStun * fleeStunLimit) {
					return new EvaluationScore(this, 1.0f);
				}

				return fail;

			}

			public override void Tick(float dt, ref Input_t input) {

				if (!_critter.hasLastKnownPosition) {
					input.movement = Vector3.zero;
					input.inputs[(int)InputType.Jump] = InputState.Released;
					input.inputs[(int)InputType.AttackRight] = InputState.Released;
				} else {
					var diff = _critter.rigidBody.position - _critter.lastKnownPosition;

					var desiredPos = _critter.lastKnownPosition + diff.normalized * fleeRange;
					var move = desiredPos - _critter.position;
					move.y = 0;

					float dist = diff.magnitude;

					input.movement = move.normalized;
					input.look = -diff;
				}
			}
		}
		public class CritterBehaviorMeleeAttack : CritterBehavior {

			float minRange;
			float maxRange;
			float destinationTolerance;
			float enemyElevationDeltaToJump;
			Vector3 desiredOffset;

			public CritterBehaviorMeleeAttack(Critter c) : base(c) {
				minRange = 2;
				maxRange = 5;
				destinationTolerance = 1.5f;

				enemyElevationDeltaToJump = 3;
			}

			public override EvaluationScore Evaluate() {
				if (!_critter.IsPanicked() || !_critter.hasLastKnownPosition) {
					return fail;
				}
				return new EvaluationScore(this, 1.0f);
			}


			override public void Tick(float dt, ref Pawn.Input_t input) {
				var diff = _critter.rigidBody.position - _critter.lastKnownPosition;
				diff.y = 0;
				if (diff == Vector3.zero) {
					diff.x = 1;
				}

				if (desiredOffset == Vector3.zero) {
					var angle = Random.Range(0, Mathf.PI * 2);
					desiredOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
				}

				var desiredPos = _critter.lastKnownPosition + desiredOffset * ((maxRange - minRange) / 2 + minRange);
				var move = desiredPos - _critter.position;
				move.y = 0;

				float dist = diff.magnitude;


				if (dist > minRange && dist < maxRange && _critter.canAttack && _critter.activity == Pawn.Activity.OnGround) {
					input.look = -diff;
					var weapon = _critter.GetInventorySlot(0) as Weapon;
					if (weapon.CanCast()) {
						if (_critter.CanSee(_critter.gameMode.players[0].playerPawn) > 0) {
							input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
							desiredOffset = Vector3.zero;
						}
					}
				} else {
					float speed = dist > 4 ? 1.0f : 0.5f;
					input.movement = move.normalized * speed;
					input.look = -diff;
					if (diff.y <= -enemyElevationDeltaToJump) {
						if (_critter.canJump && _critter.activity == Pawn.Activity.OnGround) {
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

			public override EvaluationScore Evaluate() {
				if (!_critter.IsPanicked() || !_critter.hasLastKnownPosition) {
					return fail;
				}
				return new EvaluationScore(this, 1.0f);
			}

			override public void Tick(float dt, ref Pawn.Input_t input) {

				input.movement = Vector3.zero;
				input.inputs[(int)InputType.Jump] = InputState.Released;
				input.inputs[(int)InputType.AttackRight] = InputState.Released;

				if (_critter.hasLastKnownPosition) {
					var diff = _critter.rigidBody.position - _critter.lastKnownPosition;

					if (_critter.stunAmount > _critter.data.maxStun * 0.5f) {
						var desiredPos = _critter.lastKnownPosition + diff.normalized * fleeRange;
						var move = desiredPos - _critter.position;
						move.y = 0;

						float dist = diff.magnitude;

						input.movement = move.normalized;
						input.look = -diff;

					} else {

						if (diff.y <= -enemyElevationDeltaToJump) {
							if (_critter.canJump && _critter.activity == Pawn.Activity.OnGround) {
								input.inputs[(int)InputType.Jump] = InputState.JustPressed;
							}
						}
						diff.y = 0;
						if (diff == Vector3.zero) {
							diff.x = 1;
						}
						var desiredPos = _critter.lastKnownPosition + diff.normalized * maxRange;
						var move = desiredPos - _critter.position;
						move.y = 0;

						float dist = diff.magnitude;

						var player = _critter.gameMode.players[0].playerPawn;
						if (_critter.CanSee(player) > 0) {
							if (dist > minRange && dist < maxRange) {
								if (_critter.canAttack && _critter.activity == Pawn.Activity.OnGround) {
									input.look = -diff;
									var weapon = _critter.GetInventorySlot(0) as Weapon;
									if (weapon.CanCast()) {
										input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
									}
								}
							} else {
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
}
