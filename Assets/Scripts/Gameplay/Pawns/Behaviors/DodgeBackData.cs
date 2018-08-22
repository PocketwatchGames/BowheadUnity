using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	[CreateAssetMenu(menuName = "Behaviors/DodgeBack")]
	public class DodgeBackData : BehaviorData<Critter.DodgeBack> {
		public float minDist;
		public float maxDist;
		public float distPower;
		public float score;
	}

	public partial class Critter : Pawn<Critter, CritterData> {


		public class DodgeBack : CritterBehavior<DodgeBackData> {

			float timer = 0;
			public override bool IsValid() {
				return timer > 0.5f;
			}

			public override void Start() {
				base.Start();
				timer = 0;
			}

			public override EvaluationScore Evaluate() {
				if (!_critter.IsPanicked() || !_critter.hasLastKnownPosition) {
					return fail;
				}

				var diff = _critter.position - _critter.lastKnownPosition;
				diff.y = 0;
				float dist = diff.magnitude;

				if (dist > data.maxDist) {
					return fail;
				}

				float multiplier = 1;
				foreach (var p in _critter.world.GetActorIterator<Player>()) {

					if (p.team == _critter.team) {
						continue;
					}
					Weapon left, right;
					p.GetEquippedWeapons(out left, out right);
					if (right != null && right.castTime > 0) {
						multiplier = 5;
					}
				}
				return new EvaluationScore(this, multiplier *data.score*(1.0f-Mathf.Pow(Mathf.Max(0,(dist - data.minDist))/(data.maxDist-data.minDist),data.distPower)));
			}

			override public void Tick(float dt, ref Pawn.Input_t input) {
				if (timer == 0) {
					input.inputs[(int)InputType.Dodge] = InputState.JustPressed;
				} else {
					input.inputs[(int)InputType.Dodge] = InputState.JustReleased;
				}
				var diff = _critter.position - _critter.lastKnownPosition;
				diff.y = 0;
				input.movement = diff.normalized;
				input.look = -input.movement;
				timer += dt;
			}
		}
	}
}
