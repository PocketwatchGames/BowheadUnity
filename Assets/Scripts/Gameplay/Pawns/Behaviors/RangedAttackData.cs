using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	[CreateAssetMenu(menuName = "Behaviors/RangedAttack")]
	public class RangedAttackData : BehaviorData<Critter.RangedAttack> {
		public float minRange = 5;
		public float maxRange = 10;
		public float enemyElevationDeltaToJump = 3;
		public float fleeRange = 12;
		public float fleeStunLimit = 0.5f;
	}
	public partial class Critter : Pawn<Critter, CritterData> {

		public class RangedAttack : CritterBehavior<RangedAttackData> {

			public override void Init(Critter c, BehaviorData d, float score, int weaponIndex, int attackIndex) {
				base.Init(c, d, score, weaponIndex, attackIndex);

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
						var desiredPos = _critter.lastKnownPosition + diff.normalized * data.fleeRange;
						var move = desiredPos - _critter.position;
						move.y = 0;

						float dist = diff.magnitude;

						input.movement = move.normalized;
						input.look = -diff;

					} else {

						if (diff.y <= -data.enemyElevationDeltaToJump) {
							if (_critter.canJump && _critter.activity == Pawn.Activity.OnGround) {
								input.inputs[(int)InputType.Jump] = InputState.JustPressed;
							}
						}
						diff.y = 0;
						if (diff == Vector3.zero) {
							diff.x = 1;
						}
						var desiredPos = _critter.lastKnownPosition + diff.normalized * data.maxRange;
						var move = desiredPos - _critter.position;
						move.y = 0;

						float dist = diff.magnitude;

						var player = _critter.gameMode.players[0].playerPawn;
						if (_critter.CanSee(player) > 0) {
							if (dist > data.minRange && dist < data.maxRange) {
								if (_critter.canAttack && _critter.activity == Pawn.Activity.OnGround) {
									input.look = -diff;
									var weapon = _critter.GetInventorySlot(weaponIndex) as Weapon;
									if (weapon.CanCast()) {
										input.attacks[weaponIndex] = new AttackState(weaponIndex, attackIndex, InputState.JustReleased);
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
