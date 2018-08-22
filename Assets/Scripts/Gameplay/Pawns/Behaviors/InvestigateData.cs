using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	[CreateAssetMenu(menuName = "Behaviors/Investigate")]
	public class InvestigateData : BehaviorData<Critter.Investigate> {
	}

	public partial class Critter : Pawn<Critter, CritterData> {


		public class Investigate : CritterBehavior<InvestigateData> {

			public override bool IsValid() {
				if (_critter.hasLastKnownPosition) {
					return false;
				}
				return base.IsValid();
			}
			public override EvaluationScore Evaluate() {
				if (_critter.IsPanicked() || !_critter.hasLastKnownPosition) {
					return fail;
				}
				return new EvaluationScore(this, 1.0f);
			}

			public override void Tick(float dt, ref Input_t input) {
				var move = _critter.lastKnownPosition - _critter.position;
				move.y = 0;
				input.movement = move;
				input.look = move.normalized;
			}
		}

	}
}
