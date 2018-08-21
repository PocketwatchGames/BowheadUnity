using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	[CreateAssetMenu(menuName = "Behaviors/Flee")]
	public class FleeData : BehaviorData<Critter.Flee> {
	}

	public partial class Critter : Pawn<Critter, CritterData> {


		public class Flee : CritterBehavior<FleeData> {

			public float destinationTolerance = 0.5f;

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
	}
}
