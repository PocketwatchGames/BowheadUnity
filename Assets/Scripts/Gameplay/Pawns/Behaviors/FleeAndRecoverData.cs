using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	[CreateAssetMenu(menuName = "Behaviors/FleeAndRecover")]
	public class FleeAndRecoverData : BehaviorData<Critter.FleeAndRecover> {
	}

	public partial class Critter : Pawn<Critter, CritterData> {

		public class FleeAndRecover : CritterBehavior<FleeAndRecoverData> {
			float fleeStunLimit = 0.5f;
			float fleeRange = 10;

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

	}
}
