using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	[CreateAssetMenu(menuName = "Behaviors/FleeAndRecover")]
	public class FleeAndRecoverData : BehaviorData<Critter.FleeAndRecover> {
		public float fleeStunLimit = 0.5f;
		public float fleeRange = 10;
		public float fleeStunPower = 2;
	}

	public partial class Critter : Pawn<Critter, CritterData> {

		public class FleeAndRecover : CritterBehavior<FleeAndRecoverData> {

			public override EvaluationScore Evaluate() {

				if (!_critter.IsPanicked()) {
					return fail;
				}

				if (_critter.stunAmount > _critter.data.maxStun * data.fleeStunLimit) {
					return new EvaluationScore(this, Mathf.Pow((_critter.stunAmount - _critter.data.maxStun * data.fleeStunLimit) / _critter.data.maxStun, data.fleeStunPower));
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

					var desiredPos = _critter.lastKnownPosition + diff.normalized * data.fleeRange;
					var move = desiredPos - _critter.position;
					move.y = 0;

					float dist = diff.magnitude;

					input.movement = move.normalized;
					input.look = -diff;
				}
			}
		}

	}
}
