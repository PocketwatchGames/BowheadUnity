using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	[CreateAssetMenu(menuName = "Behaviors/Idle")]
	public class IdleData : BehaviorData<Critter.Idle> {
	}

	public partial class Critter : Pawn<Critter, CritterData> {
		public class Idle : CritterBehavior<IdleData> {

			public override EvaluationScore Evaluate() {
				if (!_critter.IsPanicked()) {
					return fail;
				}
				return new EvaluationScore(this, 1.0f);
			}

			public override void Tick(float dt, ref Pawn.Input_t input) {
				input.movement = Vector3.zero;
				if (_critter.hasLastKnownPosition) {
					var diff = _critter.lastKnownPosition - _critter.position;
					input.look = diff.normalized;
				}
			}
		}
	}
}
