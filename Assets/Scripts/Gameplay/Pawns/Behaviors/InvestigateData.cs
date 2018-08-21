using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	[CreateAssetMenu(menuName = "Behaviors/Investigate")]
	public class InvestigateData : BehaviorData<Critter.Investigate> {
	}

	public partial class Critter : Pawn<Critter, CritterData> {


		public class Investigate : CritterBehavior<InvestigateData> {

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

	}
}
