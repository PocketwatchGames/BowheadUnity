using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	[CreateAssetMenu(menuName = "Behaviors/MeleeAttack")]
	public class MeleeAttackData : BehaviorData<Critter.MeleeAttack> {
		public float minRange = 2;
		public float maxRange = 5;
		public float destinationTolerance = 1.5f;
		public float enemyElevationDeltaToJump = 3;
	}

	public partial class Critter : Pawn<Critter, CritterData> {
		public class MeleeAttack : CritterBehavior<MeleeAttackData> {

			Vector3 desiredOffset;

			public MeleeAttack() {
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

				var desiredPos = _critter.lastKnownPosition + desiredOffset * ((data.maxRange - data.minRange) / 2 + data.minRange);
				var move = desiredPos - _critter.position;
				move.y = 0;

				float dist = diff.magnitude;


				if (dist > data.minRange && dist < data.maxRange && _critter.canAttack && _critter.activity == Pawn.Activity.OnGround) {
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
					if (diff.y <= -data.enemyElevationDeltaToJump) {
						if (_critter.canJump && _critter.activity == Pawn.Activity.OnGround) {
							input.inputs[(int)InputType.Jump] = InputState.JustPressed;
						}
					}
				}
			}

		}

	}
}
