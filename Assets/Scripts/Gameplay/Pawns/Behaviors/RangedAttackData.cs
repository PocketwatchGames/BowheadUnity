using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	[CreateAssetMenu(menuName = "Behaviors/RangedAttack")]
	public class RangedAttackData : BehaviorData<Critter.RangedAttack> {
	}
	public partial class Critter : Pawn<Critter, CritterData> {

		public class RangedAttack : CritterBehavior<RangedAttackData> {

			float minRange;
			float maxRange;
			float enemyElevationDeltaToJump;
			Vector3 desiredOffset;
			float fleeRange;
			float fleeStunLimit;

			public RangedAttack() {
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
