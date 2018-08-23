using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	[CreateAssetMenu(menuName = "Behaviors/DodgeBack")]
	public class DodgeBackData : BehaviorData<Critter.DodgeBack> {
		public float minDist;
		public float maxDist;
		public float distPower;
		public float staminaPower;
		public float waitTime;
	}

	public partial class Critter : Pawn<Critter, CritterData> {


		public class DodgeBack : CritterBehavior<DodgeBackData> {

			float timer = 0;

			public override void Start() {
				base.Start();
				timer = 0;
			}

			public override bool IsValid() {
				if (timer > data.waitTime) {
					return false;
				}
				return base.IsValid();
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
					for (int i=0;i<MaxInventorySize;i++) {
						var w = p.GetInventorySlot(i) as Weapon;
						if (w != null) {
							if (w.stunned) {
								multiplier += 5;
							} else {
								multiplier += Mathf.Pow(w.stamina, data.staminaPower);
							}
						}
					}
				}
				float distScore = (1.0f - Mathf.Pow(Mathf.Max(0, (dist - data.minDist)) / (data.maxDist - data.minDist), data.distPower));
				return new EvaluationScore(this, multiplier * distScore);
			}

			override public void Tick(float dt, ref Pawn.Input_t input) {
				if (timer == 0) {
					input.inputs[(int)InputType.Dodge] = InputState.Clicked;
					var diff = _critter.position - _critter.lastKnownPosition;
					diff.y = 0;
					input.movement = diff.normalized;
					input.look = -input.movement;
				}
				timer += dt;
			}
		}
	}
}
